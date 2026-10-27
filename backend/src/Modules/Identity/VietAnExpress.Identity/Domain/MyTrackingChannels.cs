using System.Text.Json.Nodes;

namespace VietAnExpress.Identity.Domain;

/// <summary>
/// Cổng liên lạc / mạng xã hội trên trang MyTracking ghi vào các ô có sẵn của dbo.TCustomer_MyTracking:
/// mỗi ứng dụng cố định 1 ô (số ô = vị trí trong mảng + 1), Email ghi cột Email riêng.
/// Thứ tự ô khớp trang mytracking.top cũ (thanh liên hệ nổi và social_line01…07). Trang hiển thị theo thứ tự ô.
/// </summary>
internal static class MyTrackingChannels
{
    /// <summary>CongTinNhan_01 … _11.</summary>
    public static readonly string[] ContactSlots =
        ["hotline", "zalo", "messenger", "whatsapp", "wechat", "telegram", "viber", "line", "snapchat", "kakaotalk", "instagram"];

    /// <summary>CongTruyenThong_01 … _07.</summary>
    public static readonly string[] SocialSlots = ["facebook", "instagram", "youtube", "tiktok", "twitter", "googlemaps", "pinterest"];

    public const string Email = "email";

    /// <summary>Số ô có trong bảng (CongTinNhan_01…15, CongTruyenThong_01…15) và độ dài tối đa mỗi ô / cột Email, Website, Color.</summary>
    public const int SlotCount = 15;
    public const int ValueMaxLength = 250;

    /// <summary>
    /// Mọi giá trị lưu trong cột nvarchar(250) của bảng cũ; kiểm tra sau khi ảnh được chuyển thành đường dẫn.
    /// </summary>
    public static bool HasTooLongValue(string configJson)
    {
        var root = MyTrackingJson.Object(configJson);
        bool TooLong(JsonObject? o, string key) => (MyTrackingJson.Text(o, key)?.Length ?? 0) > ValueMaxLength;
        if (TooLong(root, "title") || TooLong(root, "description") || TooLong(root, "background")) return true;
        if (root["images"] is JsonArray images && images.OfType<JsonObject>().Any(i => TooLong(i, "id") || TooLong(i, "src")
                || TooLong(i, "linkUrl") || TooLong(i, "title") || TooLong(i, "buttonText")))
            return true;
        if (root["brand"] is not JsonObject brand) return false;
        foreach (var key in new[] { "companyName", "address", "logo", "phone", "website", "primaryColor", "secondaryColor" })
            if (TooLong(brand, key)) return true;
        foreach (var list in new[] { brand["contacts"], brand["socials"] })
            if (list is JsonArray items && items.OfType<JsonObject>().Any(i => Value(i).Length > ValueMaxLength)) return true;
        return false;
    }

    public static string Type(JsonObject item) => MyTrackingJson.Text(item, "type") ?? "";

    public static string Value(JsonObject item) => MyTrackingJson.Text(item, "value")?.Trim() ?? "";

    /// <summary>Danh sách {type, value} các ô có giá trị, theo thứ tự ô.</summary>
    public static JsonArray Build(IEnumerable<(string Type, string? Value)> slots) =>
        new(slots.Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => (JsonNode)new JsonObject { ["type"] = s.Type, ["value"] = s.Value!.Trim() }).ToArray());
}
