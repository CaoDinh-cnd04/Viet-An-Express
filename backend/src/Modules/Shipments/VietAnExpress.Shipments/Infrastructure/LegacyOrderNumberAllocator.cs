using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VietAnExpress.SharedKernel.Exceptions;
using VietAnExpress.Shipments.Application.Orders;

namespace VietAnExpress.Shipments.Infrastructure;

internal interface ILegacyOrderNumberAllocator
{
    /// <summary>Cấp <paramref name="count"/> số liên tiếp. Phải gọi TRONG transaction ghi đơn (khóa giữ tới khi commit).</summary>
    Task<IReadOnlyList<long>> NextAsync(int count, CancellationToken cancellationToken);

    /// <summary>
    /// Gọi SAU khi commit, trước khi trả mã bill cho khách: đơn nào của portal bị hệ thống cũ ghi trùng số thì cấp số mới cho đơn portal.
    /// Trả về mã bill mới theo ID đơn (chỉ những đơn đã đổi số).
    /// </summary>
    Task<IReadOnlyDictionary<long, string>> ReissueTakenAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken);
}

/// <summary>
/// Cấp số vận đơn (OrderNumber) cho đơn tạo trên portal, NỐI TIẾP dãy 7 chữ số của hệ thống cũ (cùng bảng dbo.MaVanDon).
/// Đọc MAX với UPDLOCK + HOLDLOCK trong transaction ghi đơn nên 2 lần cấp cùng lúc trên portal phải chờ nhau, không trùng.
/// <para>
/// Phần mềm cũ cũng cấp MAX + 1: khi nó lấy số có khóa thì 2 bên chờ nhau; không khóa thì in cùng lúc vẫn có thể trùng —
/// portal kiểm tra lại sau commit (<see cref="ReissueTakenAsync"/>) và cấp số mới cho đơn portal nếu bị trùng.
/// </para>
/// Bỏ qua số 8 chữ số 90.000.001+ (dải riêng portal dùng trước đây).
/// </summary>
internal sealed class LegacyOrderNumberAllocator(ShipmentsDbContext db, TimeProvider time, ILogger<LegacyOrderNumberAllocator> logger)
    : ILegacyOrderNumberAllocator
{
    /// <summary>Dải 8 số portal dùng trước đây — chỉ giữ để không phải xoá sequence đã tạo trong database.</summary>
    public const string SequenceName = "LegacyOrderNumberSequence";
    public const long FirstNumber = 90_000_001;

    /// <summary>Số vận đơn 7 chữ số như hệ thống cũ.</summary>
    public const long Limit = 10_000_000;

    /// <summary>Chờ sau commit để lệnh chèn của hệ thống cũ (đang bị khóa giữ lại) kịp ghi xuống rồi mới kiểm tra trùng.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>Số lần kiểm tra lại tối đa (mỗi lần đổi số xong lại có thể bị chèn trùng tiếp).</summary>
    public const int MaxRounds = 3;

    public async Task<IReadOnlyList<long>> NextAsync(int count, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cấp số vận đơn phải nằm trong transaction ghi đơn.");

        var max = await db.Database
            .SqlQuery<long?>($"SELECT MAX(OrderNumber) AS [Value] FROM dbo.MaVanDon WITH (UPDLOCK, HOLDLOCK) WHERE OrderNumber < {Limit}")
            .SingleAsync(cancellationToken);
        return Next(max, count);
    }

    /// <summary>Hàm thuần — có test: số kế tiếp sau số lớn nhất hiện có (MAX + 1), không vượt quá 7 chữ số.</summary>
    public static IReadOnlyList<long> Next(long? currentMax, int count)
    {
        var first = (currentMax ?? 0) + 1;
        if (count < 1) return [];
        if (first + count - 1 >= Limit)
            throw new ConflictException("ORDER_NUMBER_EXHAUSTED", "Đã hết số vận đơn 7 chữ số, vui lòng liên hệ quản trị hệ thống");
        return [.. Enumerable.Range(0, count).Select(i => first + i)];
    }

    public async Task<IReadOnlyDictionary<long, string>> ReissueTakenAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken)
    {
        var changed = new Dictionary<long, string>();
        if (orderIds.Count == 0) return changed;
        var ids = orderIds.ToArray();

        for (var round = 1; round <= MaxRounds; round++)
        {
            await Task.Delay(SettleDelay, time, cancellationToken);
            var taken = await TakenAsync(ids, cancellationToken);
            if (taken.Count == 0) return changed;
            if (round == MaxRounds)
            {
                logger.LogError("Vận đơn portal {OrderIds} vẫn trùng số với đơn khác sau {Rounds} lần cấp lại", taken, MaxRounds);
                return changed;
            }

            foreach (var (id, code) in await RenumberAsync(taken, cancellationToken))
            {
                changed[id] = code;
                logger.LogWarning("Vận đơn portal {OrderId} trùng số với hệ thống cũ, đã cấp lại {BillCode}", id, code);
            }
        }
        return changed;
    }

    /// <summary>Đơn (trong <paramref name="ids"/>) có số vận đơn mà một dòng khác của dbo.MaVanDon cũng đang dùng.</summary>
    private async Task<List<long>> TakenAsync(long[] ids, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        return await db.LegacyOrders.IgnoreQueryFilters().AsNoTracking()
            .Where(o => ids.Contains(o.Id) && o.OrderNumber != null
                && db.LegacyOrders.IgnoreQueryFilters().Any(x => x.OrderNumber == o.OrderNumber && x.Id != o.Id))
            .Select(o => o.Id)
            .ToListAsync(ct);
    }

    /// <summary>Cấp số mới cho các đơn portal bị trùng; giữ chi nhánh và mã nước trong mã bill.</summary>
    private async Task<Dictionary<long, string>> RenumberAsync(List<long> ids, CancellationToken ct)
    {
        var result = new Dictionary<long, string>();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            result.Clear();
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var numbers = await NextAsync(ids.Count, ct);
            var orders = await db.LegacyOrders.IgnoreQueryFilters().Where(o => ids.Contains(o.Id)).OrderBy(o => o.Id).ToListAsync(ct);
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                order.VaBill = VaBillCode.Format(VaBillCode.BranchOf(order.VaBill), numbers[i], VaBillCode.CountryOf(order.VaBill));
                order.OrderNumber = numbers[i];
                result[order.Id] = numbers[i].ToString(CultureInfo.InvariantCulture);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return result;
    }
}
