namespace VietAnExpress.Ecommerce.Contracts;

/// <summary>Mã kênh bán (dbo.KenhTMDT.Ma_Kenh) — trùng giá trị <c>platform</c> / <c>src</c> của frontend.</summary>
public static class SalesChannelCodes
{
    public const string Shopify = "shopify";
    public const string TikTok = "tiktok";
    public const string Amazon = "amazon";
    public const string Ebay = "ebay";
    public const string Etsy = "etsy";
    public const string WooCommerce = "woocommerce";
    public const string Shopee = "shopee";
    public const string Lazada = "lazada";
}

public static class EcommercePermissions
{
    /// <summary>Xem cửa hàng đã kết nối và danh sách đơn E-commerce.</summary>
    public const string View = "ecommerce.view";
    /// <summary>Tạo & xử lý đơn: nhập tay, nhập CSV, sửa, xác nhận / trả lại, xóa, đồng bộ từ sàn.</summary>
    public const string Orders = "ecommerce.orders";
    /// <summary>Kết nối / ngắt kết nối cửa hàng trên sàn.</summary>
    public const string Connect = "ecommerce.connect";
}
