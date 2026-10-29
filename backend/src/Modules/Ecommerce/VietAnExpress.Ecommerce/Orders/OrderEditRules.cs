using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

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
