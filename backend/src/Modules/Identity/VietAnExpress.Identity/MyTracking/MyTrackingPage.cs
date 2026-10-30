using System.Text.Json.Nodes;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Phần mở rộng của trang MyTracking như trang mytracking.top cũ: ưu đãi, ảnh nền, mạng xã hội ở dbo.TCustomer_MyTracking,
/// ảnh quảng cáo ở dbo.TCustomer_MyTracking_Banner (mạng xã hội: xem <see cref="MyTrackingChannels"/>).
/// Thương hiệu + cổng liên lạc nằm ở hồ sơ khách dbo.TCustomer (<see cref="MyTrackingBrand"/>) — các cột thương hiệu
/// của bảng này (CompanyName, Address, Tel, Logo, Website, Color_*, Email, CongTinNhan_*) hệ thống cũ không dùng, portal không ghi.
/// </summary>
internal sealed partial class MyTrackingPage
{
    /// <summary>Tối đa 5 ảnh quảng cáo + logo + nền, mỗi ảnh ≤ 200 KB (base64 ~ 270 KB) → giới hạn 2 MB.</summary>
    public const int ConfigMaxBytes = 2 * 1024 * 1024;

    private MyTrackingPage() { } // EF Core

    public MyTrackingPage(long customerId, string configJson)
    {
        CustomerId = customerId;
        Update(configJson);
    }

    public int Id { get; private set; }
    /// <summary>dbo.TCustomer.CustomerID — mỗi khách có một trang.</summary>
    public long CustomerId { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    /// <summary>Đường dẫn ảnh nền/banner, tối đa 250 ký tự (Str1, vd "/Upload/Banner/…jpg").</summary>
    public string? Background { get; private set; }
    public List<MyTrackingBanner> Banners { get; private set; } = [];

    /// <summary>Mạng xã hội theo thứ tự ô (social_line01…07 của trang cũ).</summary>
    public JsonArray Socials() => MyTrackingChannels.Build(MyTrackingChannels.SocialSlots.Select((type, i) => (type, Slot(true, i + 1))));

    public JsonArray Images() => new(Banners.Where(b => !string.IsNullOrWhiteSpace(b.Source))
        .OrderBy(b => b.SortOrder).ThenBy(b => b.Id).Select(b => (JsonNode)b.Config()).ToArray());

    public void Update(string configJson)
    {
        var root = MyTrackingJson.Object(configJson);
        Title = MyTrackingJson.Text(root, "title");
        Description = MyTrackingJson.Text(root, "description");
        Background = MyTrackingJson.Text(root, "background");
        var brand = root["brand"] as JsonObject;
        if (brand?["socials"] is JsonArray socials)
        {
            for (var slot = 1; slot <= MyTrackingChannels.SlotCount; slot++) SetSlot(true, slot, null);
            foreach (var item in socials.OfType<JsonObject>())
                if (Array.IndexOf(MyTrackingChannels.SocialSlots, MyTrackingChannels.Type(item)) is var i and >= 0)
                    SetSlot(true, i + 1, Blank(MyTrackingChannels.Value(item)));
        }
        Banners.Clear();
        if (root["images"] is JsonArray images)
            foreach (var image in images.OfType<JsonObject>())
                Banners.Add(new MyTrackingBanner(CustomerId, Banners.Count, image));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>1 ảnh quảng cáo: Str1 = mã ảnh, Url_Hinh = ảnh, Link = link đích, Str2 = tiêu đề, Str3 = chữ trên nút.</summary>
internal sealed class MyTrackingBanner
{
    private MyTrackingBanner() { }

    public MyTrackingBanner(long customerId, int sortOrder, JsonObject image)
    {
        CustomerId = customerId;
        SortOrder = sortOrder;
        ImageKey = MyTrackingJson.Text(image, "id");
        Source = MyTrackingJson.Text(image, "src");
        Link = MyTrackingJson.Text(image, "linkUrl");
        Title = MyTrackingJson.Text(image, "title");
        ButtonText = MyTrackingJson.Text(image, "buttonText");
    }

    public int Id { get; private set; }
    public int? MyTrackingId { get; private set; }
    public long? CustomerId { get; private set; }
    public int? SortOrder { get; private set; }
    public string? ImageKey { get; private set; }
    public string? Source { get; private set; }
    public string? Link { get; private set; }
    public string? Title { get; private set; }
    public string? ButtonText { get; private set; }

    public JsonObject Config() => new()
    {
        ["id"] = ImageKey ?? Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["src"] = Source ?? "",
        ["linkUrl"] = Link ?? "",
        ["title"] = Title ?? "",
        ["buttonText"] = ButtonText ?? ""
    };
}

internal static class MyTrackingJson
{
    public static JsonObject Object(string? json) => string.IsNullOrWhiteSpace(json) ? new() : JsonNode.Parse(json) as JsonObject ?? new();

    /// <summary>Giá trị chuỗi của khóa; không có / không phải chuỗi → null.</summary>
    public static string? Text(JsonObject? root, string key) =>
        root?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>
    /// Tên công ty trên trang MyTracking luôn theo hồ sơ khách (TCustomer.InvoiceCustomerName, trống thì CustomerName), khách không tự sửa:
    /// ghi đè <c>brand.companyName</c>; tên rỗng thì giữ nguyên JSON.
    /// </summary>
    public static string WithCompanyName(string json, string? companyName)
    {
        var name = companyName?.Trim();
        if (string.IsNullOrEmpty(name)) return json;
        // Cột CompanyName của bảng cũ chỉ 250 ký tự; InvoiceCustomerName có thể dài tới 500.
        if (name.Length > MyTrackingChannels.ValueMaxLength) name = name[..MyTrackingChannels.ValueMaxLength].TrimEnd();
        var root = Object(json);
        if (root["brand"] is not JsonObject brand) root["brand"] = brand = new JsonObject();
        brand["companyName"] = name;
        return root.ToJsonString();
    }
}
