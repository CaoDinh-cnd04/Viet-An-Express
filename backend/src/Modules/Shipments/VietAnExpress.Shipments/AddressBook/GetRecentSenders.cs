using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.AddressBook;

/// <summary>Tên công ty hoặc người liên hệ của người gửi chứa <paramref name="Q"/> — tối đa <see cref="SuggestLimit"/>.</summary>
internal sealed record GetRecentSendersQuery(string? Q) : IRequest<IReadOnlyList<RecentSenderDto>>
{
    public const int SuggestLimit = 20;
    /// <summary>Số đơn gần nhất đọc lên để lọc trùng.</summary>
    public const int ScanOrders = 300;
}

/// <summary>Trong phạm vi đơn của khách (tài khoản con chỉ thấy đơn mình tạo), mới nhất trước, mỗi người gửi 1 dòng.</summary>
internal sealed class GetRecentSendersHandler(ShipmentsDbContext db, OrderAccess access)
    : IRequestHandler<GetRecentSendersQuery, IReadOnlyList<RecentSenderDto>>
{
    public async Task<IReadOnlyList<RecentSenderDto>> Handle(GetRecentSendersQuery query, CancellationToken ct)
    {
        var q = query.Q?.Trim() ?? "";
        if (q.Length is 0 or > 100) return [];

        var rows = await access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct))
            .Where(o => o.SenderName != null && (o.SenderName.Contains(q) || o.SenderContactName!.Contains(q)))
            .OrderByDescending(o => o.Id)
            .Take(GetRecentSendersQuery.ScanOrders)
            .Select(o => new RecentSenderDto(
                o.SenderName!, o.SenderContactName, o.SenderPhone, o.SenderAddress, o.SenderTax, o.SenderEmail, o.ForwarderShipperName,
                o.CreateDate == null ? null : DateOnly.FromDateTime(o.CreateDate.Value)))
            .ToListAsync(ct);
        return RecentSenders.Distinct(rows, GetRecentSendersQuery.SuggestLimit);
    }
}
