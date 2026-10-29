using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

internal sealed record CreateManualOrderCommand(ManualOrderInput Input) : IRequest<Result<EcomOrderDto>>;

internal sealed class ManualOrderHandler(EcommerceDbContext db, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<CreateManualOrderCommand, Result<EcomOrderDto>>
{
    public async Task<Result<EcomOrderDto>> Handle(CreateManualOrderCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return StoreErrors.NotLoggedIn;
        var now = VietnamTime.Now(clock);
        var built = ManualOrderRules.Build(c.Input, now);
        if (built.IsFailure) return built.Error;

        var (source, imported, shipping) = built.Value;
        if (await db.MarketplaceOrders.AnyAsync(o => o.CustomerId == customerId && o.Source == source && o.OrderName == imported.OrderName && o.DeletedAt == null, ct))
            return Error.Conflict("ECOM_ORDER_EXISTS", $"Mã đơn {imported.OrderName} đã có trong danh sách");

        var order = MarketplaceOrder.CreateManual(customerId, source, imported, shipping, now, user.StaffId);
        db.MarketplaceOrders.Add(order);
        await db.SaveChangesAsync(ct);
        return EcomOrderMapper.ToDto(order);
    }
}

internal sealed record ManualProductInput(string? Name, string? Sku, int Qty, decimal FobPrice, decimal SellingPrice, string? HsCode);

/// <summary>Khớp <c>NewManualEcomOrder</c> của frontend (API_CONTRACT.md §5).</summary>
internal sealed record ManualOrderInput(
    string? Ref, string? Source, string? Branch, string? Cnee, string? Phone, string? Email, string? Ct, string? CountryCode,
    string? Postal, string? City, string? State, string? Address, string? Service, string? Hub, decimal Kg,
    IReadOnlyList<ManualProductInput>? Products, Dictionary<string, string?>? Customs);
