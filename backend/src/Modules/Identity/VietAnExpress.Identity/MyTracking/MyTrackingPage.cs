using System.Text.Json.Nodes;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Phần mở rộng của trang MyTracking như trang mytracking.top cũ: ưu đãi, ảnh nền, mạng xã hội ở dbo.TCustomer_MyTracking,
/// ảnh quảng cáo ở dbo.TCustomer_MyTracking_Banner (mạng xã hội: xem <see cref="MyTrackingChannels"/>).
/// Thương hiệu + cổng liên lạc nằm ở hồ sơ khách dbo.TCustomer (<see cref="MyTrackingBrand"/>). Tên công ty / địa chỉ / điện thoại
/// mặc định theo hồ sơ khách; khách sửa ở MyTracking thì lưu riêng vào cột CompanyName / Address / Tel của bảng này (TCustomer không đổi).
/// Các cột thương hiệu khác của bảng (Logo, Website, Color_*, Email, CongTinNhan_*) hệ thống cũ không dùng, portal không ghi.
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
    /// <summary>Tên công ty / địa chỉ / điện thoại khách tự sửa cho trang tracking; null = theo hồ sơ khách.</summary>
    public string? CompanyName { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
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

    /// <summary>
    /// Ghi tên công ty / địa chỉ / điện thoại khách nhập; ô trống hoặc giống hồ sơ khách thì để NULL — trang tiếp tục theo hồ sơ
    /// (CS đổi hồ sơ sau này vẫn hiện đúng).
    /// </summary>
    public void SetContactInfo(JsonObject? brand, MyTrackingProfile profile)
    {
        CompanyName = Override(MyTrackingJson.Text(brand, "companyName"), profile.CompanyName);
        Address = Override(MyTrackingJson.Text(brand, "address"), profile.Address);
        Phone = Override(MyTrackingJson.Text(brand, "phone"), profile.Phone);
    }

    private static string? Override(string? value, string profile) => Blank(value) is { } v && v != profile ? v : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Tên công ty / địa chỉ / điện thoại theo hồ sơ khách (TCustomer) — giá trị mặc định của trang tracking.</summary>
internal sealed record MyTrackingProfile(string CompanyName, string Address, string Phone)
{
    public static MyTrackingProfile From(MyTrackingBrandView brand, string? companyName)
    {
        var name = companyName?.Trim() ?? "";
        // Cột CompanyName của bảng cũ chỉ 250 ký tự; InvoiceCustomerName có thể dài tới 500.
        if (name.Length > MyTrackingChannels.ValueMaxLength) name = name[..MyTrackingChannels.ValueMaxLength].TrimEnd();
        return new(name, brand.Brand?.Address?.Trim() ?? "", brand.Brand?.Phone?.Trim() ?? "");
    }
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
}
