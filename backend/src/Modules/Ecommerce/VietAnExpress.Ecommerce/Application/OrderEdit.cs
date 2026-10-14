using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.Ecommerce.Domain;
using VietAnExpress.Ecommerce.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Application;

internal sealed record EcomReceiverInput(
    string? Name, string? Company, string? Phone, string? Email, string? Address1, string? Address2,
    string? City, string? State, string? Postal, string? CountryCode);

/// <summary>Khớp <c>EcomOrderUpdate</c> của frontend.</summary>
internal sealed record EcomOrderEditInput(
    EcomReceiverInput? Receiver, decimal? Kg, IReadOnlyList<ManualProductInput>? Products,
    string? Service, string? Hub, string? Branch, string? Note);

internal sealed record UpdateEcomOrderCommand(long Id, EcomOrderEditInput Input) : IRequest<Result<EcomOrderDto>>;

/// <summary>Kiểm tra dữ liệu khách sửa. Hàm thuần — có test.</summary>
internal static class OrderEditRules
{
    public const int MaxProducts = 50;

    public static Result<(MarketplaceRecipient Recipient, List<EcomProductDto> Products, decimal? WeightKg)> Build(EcomOrderEditInput i)
    {
        var r = i.Receiver;
        if (string.IsNullOrWhiteSpace(r?.Name)) return Invalid("Nhập tên người nhận");
        if (string.IsNullOrWhiteSpace(r.Address1)) return Invalid("Nhập địa chỉ người nhận");
        var cc = r.CountryCode?.Trim().ToUpperInvariant();
        if (cc is not { Length: 2 } || !cc.All(char.IsAsciiLetter)) return Invalid("Chọn nước đến");
        if (i.Kg is < 0 or > 10000) return Invalid("Cân nặng không hợp lệ");

        var products = (i.Products ?? []).ToList();
        if (products.Count is 0 or > MaxProducts) return Invalid($"Đơn có từ 1 đến {MaxProducts} sản phẩm");
        if (products.Any(p => string.IsNullOrWhiteSpace(p.Name))) return Invalid("Nhập tên hàng cho mọi sản phẩm");
        if (products.Any(p => p.Qty < 1 || p.FobPrice < 0 || p.SellingPrice < 0)) return Invalid("Số lượng ≥ 1, giá không được âm");
        if (products.Any(p => !string.IsNullOrWhiteSpace(p.HsCode) && !IsHsCode(p.HsCode))) return Invalid("Mã HS gồm 6–10 chữ số");

        var recipient = new MarketplaceRecipient(
            Cut(r.Name, MarketplaceOrder.NameMaxLength), Cut(r.Company, MarketplaceOrder.NameMaxLength),
            Cut(OrderData.NormalizePhone(r.Phone, cc), MarketplaceOrder.PhoneMaxLength), Cut(r.Email, MarketplaceOrder.EmailMaxLength),
            Cut(r.Address1, MarketplaceOrder.AddressMaxLength), Cut(r.Address2, MarketplaceOrder.AddressMaxLength),
            Cut(r.City, MarketplaceOrder.CityMaxLength), Cut(r.State, MarketplaceOrder.CityMaxLength),
            Cut(OrderData.NormalizePostal(r.Postal), MarketplaceOrder.PostalMaxLength),
            cc, Cut(OrderData.CountryName(cc), MarketplaceOrder.CountryNameMaxLength));
        var lines = products.Select(p => new EcomProductDto(p.Name!.Trim(), p.Sku?.Trim() ?? "", p.Qty, p.FobPrice, p.SellingPrice,
            string.IsNullOrWhiteSpace(p.HsCode) ? null : new string(p.HsCode.Where(char.IsDigit).ToArray()))).ToList();
        return (recipient, lines, i.Kg is > 0 ? Math.Round(i.Kg.Value, 3, MidpointRounding.AwayFromZero) : null);
    }

    /// <summary>Mã HS 6–10 số, cho phép dấu chấm / khoảng trắng (6109.10.00).</summary>
    public static bool IsHsCode(string s)
    {
        var digits = s.Where(char.IsDigit).Count();
        return digits is >= 6 and <= 10 && s.All(c => char.IsDigit(c) || c is '.' or ' ');
    }

    private static Error Invalid(string message) => Error.Validation("ECOM_ORDER_INVALID", message);

    private static string? Cut(string? s, int max) => s?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;
}

internal sealed class OrderEditHandlers(EcommerceDbContext db, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<UpdateEcomOrderCommand, Result<EcomOrderDto>>
{
    private DateTime Now => VietnamTime.Now(clock);

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
        return EcomOrderHandlers.ToDto(order);
    }

    private static string? Trim(string? s, int max) => s?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;
}
