namespace VietAnExpress.Ecommerce.Orders;

/// <summary>Thông tin vận chuyển khách chọn khi nhập tay.</summary>
internal sealed record ManualShipping(string? Service, string? Hub, string? Branch, string? CustomsJson);

/// <summary>Đơn đọc từ sàn, đã chuẩn hóa (giờ Việt Nam, kg, cắt độ dài theo cột).</summary>
internal sealed record ImportedOrder(
    string PlatformOrderId, string OrderName, MarketplaceRecipient Recipient,
    int ItemCount, decimal? WeightKg, string? Currency, decimal? TotalAmount,
    string? ProductsJson, string? Note, DateTime? PlacedAt);
