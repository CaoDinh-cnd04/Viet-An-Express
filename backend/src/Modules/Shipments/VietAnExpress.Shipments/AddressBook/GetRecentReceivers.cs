using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.AddressBook;

/// <summary>
/// Tên công ty người nhận chứa <paramref name="Q"/> (ô gợi ý, giống hệ thống cũ) — tối đa <see cref="SuggestLimit"/>;
/// <paramref name="Q"/> trống = sổ địa chỉ: người nhận gần nhất, tối đa <see cref="BookLimit"/>.
/// </summary>
internal sealed record GetRecentReceiversQuery(string? Q) : IRequest<IReadOnlyList<RecentReceiverDto>>
{
    public const int SuggestLimit = 20;
    public const int BookLimit = 100;
    /// <summary>Số đơn gần nhất đọc lên để lọc trùng — đủ cho số người nhận khác nhau cần trả.</summary>
    public const int ScanOrders = 300;
    public const int BookScanOrders = 2000;
}

/// <summary>Trong phạm vi đơn của khách (tài khoản con chỉ thấy đơn mình tạo), mới nhất trước, mỗi người nhận 1 dòng.</summary>
internal sealed class GetRecentReceiversHandler(ShipmentsDbContext db, OrderAccess access)
    : IRequestHandler<GetRecentReceiversQuery, IReadOnlyList<RecentReceiverDto>>
{
    public async Task<IReadOnlyList<RecentReceiverDto>> Handle(GetRecentReceiversQuery query, CancellationToken ct)
    {
        var q = query.Q?.Trim() ?? "";
        if (q.Length > 100) return [];
        var book = q.Length == 0;

        var orders = access.Apply(db.LegacyOrders.AsNoTracking(), await access.ScopeAsync(ct)).Where(o => o.ConsigneeName != null);
        if (!book) orders = orders.Where(o => o.ConsigneeName!.Contains(q));
        var rows = await orders
            .OrderByDescending(o => o.Id)
            .Take(book ? GetRecentReceiversQuery.BookScanOrders : GetRecentReceiversQuery.ScanOrders)
            .Select(o => new RecentReceiverDto(
                o.ConsigneeName!, o.ConsigneeContactName, o.ConsigneePhone, o.ConsigneePhoneCode, o.ConsigneeEmail, o.ConsigneeVatTax,
                o.ConsigneeCountry, o.ConsigneeCity, o.ConsigneeState, o.ConsigneePostalCode,
                o.ConsigneeAddress1, o.ConsigneeAddress2, o.ConsigneeAddress3, o.ConsigneeIossNo, o.ConsigneeEoriNo,
                o.CreateDate == null ? null : DateOnly.FromDateTime(o.CreateDate.Value)))
            .ToListAsync(ct);
        return RecentReceivers.Distinct(rows, book ? GetRecentReceiversQuery.BookLimit : GetRecentReceiversQuery.SuggestLimit);
    }
}
