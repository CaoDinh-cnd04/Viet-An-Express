using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

internal sealed record UpdateEcomOrderCommand(long Id, EcomOrderEditInput Input) : IRequest<Result<EcomOrderDto>>;

internal sealed class UpdateEcomOrderHandler(EcommerceDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<UpdateEcomOrderCommand, Result<EcomOrderDto>>
{
    private DateTime Now => VietnamTime.Now(clock);

    private static string? Trim(string? s, int max) => s?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;

    public async Task<Result<EcomOrderDto>> Handle(UpdateEcomOrderCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return StoreErrors.NotLoggedIn;
        var order = await db.MarketplaceOrders.VisibleTo(user, customerId).FirstOrDefaultAsync(o => o.Id == c.Id && o.DeletedAt == null, ct);
        if (order is null) return Error.NotFound("ECOM_ORDER_NOT_FOUND", "Không tìm thấy đơn");
        if (order.Bill is not null) return Error.BusinessRule("ECOM_ORDER_BILLED", "Đơn đã có bill, không sửa được");

        var built = OrderEditRules.Build(c.Input);
        if (built.IsFailure) return built.Error;
        var (recipient, products, weight) = built.Value;
        var i = c.Input;
        order.Edit(recipient, weight, JsonSerializer.Serialize(products, OrderData.Json), products.Sum(p => p.Qty),
            // Đơn nhập tay: giá trị = tổng tiền hàng; đơn từ sàn giữ tổng của sàn (gồm phí ship khách trả).
            order.Source == MarketplaceOrder.ManualSource ? products.Sum(p => p.SellingPrice * p.Qty) : null,
            Trim(i.Service, MarketplaceOrder.ServiceMaxLength), Trim(i.Hub, MarketplaceOrder.HubMaxLength),
            Trim(i.Branch, MarketplaceOrder.BranchMaxLength), Trim(i.Note, MarketplaceOrder.NoteMaxLength), Now);
        await db.SaveChangesAsync(ct);
        return EcomOrderMapper.ToDto(order);
    }
}

internal sealed record EcomReceiverInput(
    string? Name, string? Company, string? Phone, string? Email, string? Address1, string? Address2,
    string? City, string? State, string? Postal, string? CountryCode);

/// <summary>Khớp <c>EcomOrderUpdate</c> của frontend.</summary>
internal sealed record EcomOrderEditInput(
    EcomReceiverInput? Receiver, decimal? Kg, IReadOnlyList<ManualProductInput>? Products,
    string? Service, string? Hub, string? Branch, string? Note);
