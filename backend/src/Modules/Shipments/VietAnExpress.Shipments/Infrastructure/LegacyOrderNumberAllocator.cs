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
/// Cấp số vận đơn (OrderNumber) cho đơn tạo trên portal từ CHÍNH bộ đếm của hệ thống cũ: dòng dbo.TConfig có
/// ConfigName = 'MaxOrderNumber' — thủ tục in bill cũ (p_MVD_Print_Bill / _Multi / _Check) cấp số bằng
/// <c>UPDATE TConfig SET ConfigValue_Int = ConfigValue_Int + 1, @so = ConfigValue_Int + 1</c>. Portal tăng đúng dòng đó
/// trong transaction ghi đơn (khóa dòng tới khi commit) nên 2 bên lấy số nối nhau, không trùng.
/// <para>
/// 23/10/2026: trước đây portal tự lấy MAX(MaVanDon) + 1, không qua bộ đếm → trùng 6180074 với bill nhân viên in.
/// Bộ đếm có thể đang thấp hơn số portal đã cấp theo cách cũ → số lấy ra mà đã có trong dbo.MaVanDon / dbo.Orders (mã 7 chữ số)
/// thì bỏ qua, lấy tiếp. Không có bảng TConfig (DB thử nghiệm) → MAX của MaVanDon + Orders. Sau commit vẫn kiểm tra trùng
/// (<see cref="ReissueTakenAsync"/>).
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

    /// <summary>Tên dòng bộ đếm số bill trong dbo.TConfig (hệ thống cũ).</summary>
    public const string CounterName = "MaxOrderNumber";

    /// <summary>Số lần lấy tiếp tối đa khi số từ bộ đếm đã có người dùng (bộ đếm thấp hơn số portal cấp trước 23/10/2026).</summary>
    public const int MaxSkips = 50;

    public async Task<IReadOnlyList<long>> NextAsync(int count, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Cấp số vận đơn phải nằm trong transaction ghi đơn.");
        if (count < 1) return [];

        for (var attempt = 1; attempt <= MaxSkips; attempt++)
        {
            var last = await TakeFromCounterAsync(count, cancellationToken);
            if (last is null) return Next(await CurrentMaxAsync(cancellationToken), count); // không có bộ đếm
            var numbers = Next(last - count, count);
            if (!await AnyUsedAsync(numbers[0], numbers[^1], cancellationToken)) return numbers;
            logger.LogWarning("Số {First}–{Last} từ bộ đếm {Counter} đã có đơn dùng, lấy tiếp", numbers[0], numbers[^1], CounterName);
        }
        throw new ConflictException("ORDER_NUMBER_COUNTER_BEHIND",
            "Bộ đếm số vận đơn đang thấp hơn số đã dùng — vui lòng liên hệ quản trị hệ thống");
    }

    /// <summary>
    /// Tăng bộ đếm TConfig.MaxOrderNumber thêm <paramref name="count"/>, trả về số cuối của dải vừa lấy — cùng câu lệnh với
    /// thủ tục in bill cũ (UPDATE 1 dòng, khóa dòng tới khi commit). Không có bảng / dòng bộ đếm → null.
    /// </summary>
    private async Task<long?> TakeFromCounterAsync(int count, CancellationToken ct) =>
        // Không ghép toán tử LINQ: câu lệnh có IF / DECLARE nên EF phải chạy nguyên văn (không bọc làm truy vấn con).
        (await db.Database.SqlQuery<long?>($"""
            DECLARE @last bigint = NULL;
            IF OBJECT_ID(N'dbo.TConfig', N'U') IS NOT NULL
                UPDATE dbo.TConfig
                SET ConfigValue_Int = ConfigValue_Int + {count}, @last = ConfigValue_Int + {count}
                WHERE ConfigName = {CounterName};
            SELECT @last AS [Value];
            """).ToListAsync(ct)).Single();

    /// <summary>Dải số [first, last] đã có dòng dbo.MaVanDon hoặc lô dbo.Orders (mã 7 chữ số) dùng chưa.</summary>
    private async Task<bool> AnyUsedAsync(long first, long last, CancellationToken ct)
    {
        var from = first.ToString(CultureInfo.InvariantCulture);
        var to = last.ToString(CultureInfo.InvariantCulture);
        return (await db.Database.SqlQuery<int>($"""
            DECLARE @used int = CASE WHEN EXISTS (SELECT 1 FROM dbo.MaVanDon WHERE OrderNumber BETWEEN {first} AND {last}) THEN 1 ELSE 0 END;
            IF @used = 0 AND OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
                SELECT @used = 1 WHERE EXISTS (
                    SELECT 1 FROM dbo.Orders
                    WHERE OrderNumber BETWEEN {from} AND {to} AND LEN(OrderNumber) = 7 AND OrderNumber NOT LIKE N'%[^0-9]%');
            SELECT @used AS [Value];
            """).ToListAsync(ct)).Single() == 1;
    }

    /// <summary>Dự phòng khi DB không có bộ đếm TConfig: số lớn nhất của MaVanDon (khóa) và Orders (mã 7 chữ số).</summary>
    private async Task<long?> CurrentMaxAsync(CancellationToken ct) =>
        (await db.Database.SqlQuery<long?>($$"""
            DECLARE @van_don bigint = (SELECT MAX(OrderNumber) FROM dbo.MaVanDon WITH (UPDLOCK, HOLDLOCK) WHERE OrderNumber < {{Limit}});
            DECLARE @lo_hang bigint = NULL;
            IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL
                -- Chỉ đọc mã lớn hơn MAX của MaVanDon (so chuỗi cùng 7 ký tự = so số) để dò ít dòng.
                SELECT @lo_hang = MAX(TRY_CAST(OrderNumber AS bigint))
                FROM dbo.Orders
                WHERE OrderNumber > CONVERT(nvarchar(20), ISNULL(@van_don, 0))
                  AND LEN(OrderNumber) = 7 AND OrderNumber NOT LIKE N'%[^0-9]%';
            SELECT CASE WHEN @lo_hang > ISNULL(@van_don, 0) THEN @lo_hang ELSE @van_don END AS [Value];
            """).ToListAsync(ct)).Single();

    /// <summary>Hàm thuần — có test: <paramref name="count"/> số liền sau <paramref name="currentMax"/>, không vượt quá 7 chữ số.</summary>
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

    /// <summary>
    /// Đơn (trong <paramref name="ids"/>) có số vận đơn mà một dòng khác của dbo.MaVanDon cũng đang dùng, hoặc một lô hàng
    /// dbo.Orders của KHÁCH KHÁC đang dùng (lô cùng khách là nhân viên đang xử lý chính đơn portal này).
    /// </summary>
    private async Task<List<long>> TakenAsync(long[] ids, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var taken = await db.LegacyOrders.IgnoreQueryFilters().AsNoTracking()
            .Where(o => ids.Contains(o.Id) && o.OrderNumber != null
                && db.LegacyOrders.IgnoreQueryFilters().Any(x => x.OrderNumber == o.OrderNumber && x.Id != o.Id))
            .Select(o => o.Id)
            .ToListAsync(ct);
        foreach (var id in ids.Except(taken))
        {
            var inOrders = (await db.Database.SqlQuery<int>($"""
                IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL SELECT 0 AS [Value];
                ELSE SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.MaVanDon m
                    JOIN dbo.Orders x ON x.OrderNumber = CONVERT(nvarchar(20), m.OrderNumber)
                    WHERE m.ID = {id} AND m.OrderNumber IS NOT NULL AND x.CustomerID <> m.CustomerID) THEN 1 ELSE 0 END AS [Value];
                """).ToListAsync(ct)).Single();
            if (inOrders == 1) taken.Add(id);
        }
        return taken;
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
                order.VaBill = null; // VA_Bill để NULL như hệ thống cũ
                order.OrderNumber = numbers[i];
                result[order.Id] = numbers[i].ToString(CultureInfo.InvariantCulture);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return result;
    }
}
