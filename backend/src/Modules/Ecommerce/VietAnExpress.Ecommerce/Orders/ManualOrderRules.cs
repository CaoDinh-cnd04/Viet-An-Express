using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>Kiểm tra và chuẩn hóa đơn nhập tay. Hàm thuần — có test.</summary>
internal static class ManualOrderRules
{
    public const int MaxProducts = 5;

    /// <summary>Nguồn khách chọn được khi nhập tay: tự nhập hoặc đơn của 1 kênh bán chưa kết nối.</summary>
    public static readonly string[] Sources =
    [
        MarketplaceOrder.ManualSource, SalesChannelCodes.TikTok, SalesChannelCodes.Shopee,
        SalesChannelCodes.Lazada, SalesChannelCodes.Amazon, SalesChannelCodes.Ebay, SalesChannelCodes.Etsy, SalesChannelCodes.WooCommerce
    ];

    public static Result<(string Source, ImportedOrder Order, ManualShipping Shipping)> Build(ManualOrderInput i, DateTime now)
    {
        var reference = i.Ref?.Trim();
        if (string.IsNullOrEmpty(reference)) return Invalid("Nhập mã đơn của shop");
        if (reference.Length > MarketplaceOrder.OrderNameMaxLength) return Invalid($"Mã đơn tối đa {MarketplaceOrder.OrderNameMaxLength} ký tự");
        var source = string.IsNullOrWhiteSpace(i.Source) ? MarketplaceOrder.ManualSource : i.Source.Trim().ToLowerInvariant();
        if (!Sources.Contains(source)) return Invalid("Nguồn đơn không hợp lệ");
        if (string.IsNullOrWhiteSpace(i.Cnee)) return Invalid("Nhập tên người nhận");
        if (string.IsNullOrWhiteSpace(i.Ct)) return Invalid("Nhập nước đến");
        if (string.IsNullOrWhiteSpace(i.Address)) return Invalid("Nhập địa chỉ người nhận");
        if (string.IsNullOrWhiteSpace(i.Phone)) return Invalid("Nhập số điện thoại người nhận");
        if (i.Kg < 0) return Invalid("Cân nặng không hợp lệ");

        var products = (i.Products ?? []).ToList();
        if (products.Count is 0 or > MaxProducts) return Invalid($"Đơn có từ 1 đến {MaxProducts} sản phẩm");
        if (products.Any(p => string.IsNullOrWhiteSpace(p.Name))) return Invalid("Nhập tên hàng cho mọi sản phẩm");
        if (products.Any(p => p.Qty < 1 || p.FobPrice < 0 || p.SellingPrice < 0)) return Invalid("Số lượng ≥ 1, giá không được âm");

        var lines = products.Select(p => new EcomProductDto(p.Name!.Trim(), p.Sku?.Trim() ?? "", p.Qty, p.FobPrice, p.SellingPrice,
            string.IsNullOrWhiteSpace(p.HsCode) ? null : p.HsCode.Trim())).ToList();
        var customs = i.Customs?.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value!.Trim());

        var countryCode = i.CountryCode?.Trim().ToUpperInvariant() is { Length: 2 } code ? code : null;
        var recipient = new MarketplaceRecipient(
            Cut(i.Cnee, MarketplaceOrder.NameMaxLength), null,
            Cut(OrderData.NormalizePhone(i.Phone, countryCode), MarketplaceOrder.PhoneMaxLength), Cut(i.Email, MarketplaceOrder.EmailMaxLength),
            Cut(i.Address, MarketplaceOrder.AddressMaxLength), null,
            Cut(i.City, MarketplaceOrder.CityMaxLength), Cut(i.State, MarketplaceOrder.CityMaxLength),
            Cut(i.Postal, MarketplaceOrder.PostalMaxLength),
            countryCode,
            Cut(i.Ct, MarketplaceOrder.CountryNameMaxLength));

        var order = new ImportedOrder(
            PlatformOrderId: "", OrderName: reference, Recipient: recipient,
            ItemCount: lines.Sum(l => l.Qty), WeightKg: i.Kg > 0 ? Math.Round(i.Kg, 3, MidpointRounding.AwayFromZero) : null,
            Currency: null, TotalAmount: lines.Sum(l => l.SellingPrice * l.Qty),
            ProductsJson: JsonSerializer.Serialize(lines, OrderData.Json), Note: null, PlacedAt: now);
        var shipping = new ManualShipping(
            Cut(i.Service, MarketplaceOrder.ServiceMaxLength), Cut(i.Hub, MarketplaceOrder.HubMaxLength), Cut(i.Branch, MarketplaceOrder.BranchMaxLength),
            customs is { Count: > 0 } ? JsonSerializer.Serialize(customs, OrderData.Json) : null);
        return (source, order, shipping);
    }

    private static Error Invalid(string message) => Error.Validation("ECOM_MANUAL_INVALID", message);

    private static string? Cut(string? s, int max) => s?.Trim() is { Length: > 0 } t ? (t.Length <= max ? t : t[..max]) : null;
}
