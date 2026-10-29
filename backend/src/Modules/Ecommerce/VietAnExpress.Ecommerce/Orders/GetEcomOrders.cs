using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Ecommerce.Orders;

/// <param name="Source">tiktok / manual / … — trống = mọi nguồn.</param>
/// <param name="Search">Tìm theo mã đơn, người nhận, bill.</param>
/// <param name="Scope">"inbox" = tab Đơn hàng (chưa xác nhận), "mine" = Đơn hàng của tôi (đã xác nhận gửi), trống = tất cả.</param>
internal sealed record GetEcomOrdersQuery(string? Source, string? Search, string? Scope = null) : IRequest<IReadOnlyList<EcomOrderDto>>;

internal sealed class GetEcomOrdersHandler(EcommerceDbContext db, ICurrentUser user)
    : IRequestHandler<GetEcomOrdersQuery, IReadOnlyList<EcomOrderDto>>
{
    private const int MaxListed = 500;

    public async Task<IReadOnlyList<EcomOrderDto>> Handle(GetEcomOrdersQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return [];
        var orders = db.MarketplaceOrders.AsNoTracking().VisibleTo(user, customerId).Where(o => o.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(q.Source)) orders = orders.Where(o => o.Source == q.Source);
        if (q.Scope == "inbox") orders = orders.Where(o => o.ConfirmedAt == null);
        else if (q.Scope == "mine") orders = orders.Where(o => o.ConfirmedAt != null);
        if (q.Search?.Trim() is { Length: > 0 } s)
            orders = orders.Where(o => o.OrderName.Contains(s) || (o.Bill != null && o.Bill.Contains(s)) || (o.Recipient.Name != null && o.Recipient.Name.Contains(s)));

        var rows = await orders.OrderByDescending(o => o.PlacedAt ?? o.CreateDate).Take(MaxListed).ToListAsync(ct);
        return [.. rows.Select(EcomOrderMapper.ToDto)];
    }
}
