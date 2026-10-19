using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VietAnExpress.Identity.Domain;

/// <summary>
/// Trang MyTracking dùng dbo.TCustomer_MyTracking và các ảnh trong dbo.TCustomer_MyTracking_Banner.
/// Các trường có sẵn lưu vào cột tương ứng; phần JSON bổ sung giữ những thuộc tính chưa có cột.
/// Đã xuất bản thì ai có link <c>/t/{slug}</c> cũng xem và tra cứu vận đơn được.
/// </summary>
internal sealed partial class MyTrackingPage
{
    public const int SlugMinLength = 3;
    public const int SlugMaxLength = 60;
    /// <summary>Tối đa 5 ảnh quảng cáo + logo + nền, mỗi ảnh ≤ 200 KB (base64 ~ 270 KB) → giới hạn 2 MB.</summary>
    public const int ConfigMaxBytes = 2 * 1024 * 1024;

    private MyTrackingPage() { } // EF Core

    public MyTrackingPage(long customerId, string slug, string configJson, bool isPublished, DateTime now)
    {
        CustomerId = customerId;
        CreateDate = now;
        Update(slug, configJson, isPublished, now);
        ModifyDate = null;
    }

    public int Id { get; private set; }
    /// <summary>dbo.TCustomer.CustomerID — mỗi khách có một trang.</summary>
    public long CustomerId { get; private set; }
    /// <summary>Đường dẫn công khai /t/{slug}: chữ thường, số, gạch ngang.</summary>
    public string? Slug { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public string? CompanyName { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Logo { get; private set; }
    public string? Facebook { get; private set; }
    public string? Instagram { get; private set; }
    public string? X { get; private set; }
    public string? Zalo { get; private set; }
    public string? WhatsApp { get; private set; }
    public string? Background { get; private set; }
    public string? ExtraConfigJson { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime? CreateDate { get; private set; }
    public DateTime? ModifyDate { get; private set; }
    public List<MyTrackingBanner> Banners { get; private set; } = [];

    public DateTime? UpdatedAt => ModifyDate ?? CreateDate;

    public string ConfigJson
    {
        get
        {
            var root = MyTrackingJson.Object(ExtraConfigJson);
            MyTrackingJson.Put(root, "title", Title);
            MyTrackingJson.Put(root, "description", Description);
            MyTrackingJson.Put(root, "background", Background);
            if (root["brand"] is JsonObject || new[] { CompanyName, Address, Phone, Logo, Facebook, Instagram, X, Zalo, WhatsApp }.Any(v => v is not null))
            {
                var brand = root["brand"] as JsonObject ?? new JsonObject();
                MyTrackingJson.Put(brand, "companyName", CompanyName);
                MyTrackingJson.Put(brand, "address", Address);
                MyTrackingJson.Put(brand, "phone", Phone);
                MyTrackingJson.Put(brand, "logo", Logo);
                MyTrackingJson.Put(brand, "facebook", Facebook);
                MyTrackingJson.Put(brand, "instagram", Instagram);
                MyTrackingJson.Put(brand, "x", X);
                MyTrackingJson.Put(brand, "zalo", Zalo);
                MyTrackingJson.Put(brand, "whatsapp", WhatsApp);
                // Bản ghi do phần mềm cũ tạo có thể chỉ có một vài trường thương hiệu.
                foreach (var key in new[] { "companyName", "address", "phone", "logo", "facebook", "instagram", "x", "zalo", "whatsapp" })
                    if (!brand.ContainsKey(key)) brand[key] = "";
                if (root["brand"] is not JsonObject) root["brand"] = brand;
            }
            if (Banners.Count > 0 || root["images"] is JsonArray { Count: 0 })
                root["images"] = new JsonArray(Banners.OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
                    .Select(b => (JsonNode)b.Config()).ToArray());
            return root.ToJsonString();
        }
    }

    public void Update(string slug, string configJson, bool isPublished, DateTime now)
    {
        Slug = slug;
        var root = MyTrackingJson.Object(configJson);
        Title = MyTrackingJson.Take(root, "title");
        Description = MyTrackingJson.Take(root, "description");
        Background = MyTrackingJson.Take(root, "background");
        var brand = root["brand"] as JsonObject;
        CompanyName = MyTrackingJson.Take(brand, "companyName");
        Address = MyTrackingJson.Take(brand, "address");
        Phone = MyTrackingJson.Take(brand, "phone");
        Logo = MyTrackingJson.Take(brand, "logo");
        Facebook = MyTrackingJson.Take(brand, "facebook");
        Instagram = MyTrackingJson.Take(brand, "instagram");
        X = MyTrackingJson.Take(brand, "x");
        Zalo = MyTrackingJson.Take(brand, "zalo");
        WhatsApp = MyTrackingJson.Take(brand, "whatsapp");
        Banners.Clear();
        if (root["images"] is JsonArray images && images.All(i => i is JsonObject))
        {
            foreach (var image in images.Cast<JsonObject>())
                Banners.Add(new MyTrackingBanner(CustomerId, Banners.Count, image));
            root["images"] = new JsonArray();
        }
        ExtraConfigJson = root.ToJsonString();
        IsPublished = isPublished;
        ModifyDate = now;
    }

    public static bool IsValidSlug(string slug) =>
        slug.Length is >= SlugMinLength and <= SlugMaxLength && SlugRegex().IsMatch(slug);

    /// <summary>Gợi ý đường dẫn từ mã khách: "SaigonbayHN" → "saigonbayhn"; ký tự khác chữ / số thành "-".</summary>
    public static string SuggestSlug(string customerCode)
    {
        var slug = NonSlugChars().Replace(customerCode.Trim().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > SlugMaxLength) slug = slug[..SlugMaxLength].TrimEnd('-');
        return slug.Length >= SlugMinLength ? slug : $"shop-{slug}".TrimEnd('-');
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}

internal sealed class MyTrackingBanner
{
    private MyTrackingBanner() { }

    public MyTrackingBanner(long customerId, int sortOrder, JsonObject image)
    {
        CustomerId = customerId;
        SortOrder = sortOrder;
        var root = (JsonObject)image.DeepClone();
        ImageKey = MyTrackingJson.Take(root, "id");
        Source = MyTrackingJson.Take(root, "src");
        Link = MyTrackingJson.Take(root, "linkUrl");
        Title = MyTrackingJson.Take(root, "title");
        ButtonText = MyTrackingJson.Take(root, "buttonText");
        ExtraConfigJson = root.ToJsonString();
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
    public string? ExtraConfigJson { get; private set; }

    public JsonObject Config()
    {
        var root = MyTrackingJson.Object(ExtraConfigJson);
        MyTrackingJson.Put(root, "id", ImageKey ?? (ExtraConfigJson is null ? Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : null));
        MyTrackingJson.Put(root, "src", Source);
        MyTrackingJson.Put(root, "linkUrl", Link);
        MyTrackingJson.Put(root, "title", Title);
        MyTrackingJson.Put(root, "buttonText", ButtonText);
        return root;
    }
}

internal static class MyTrackingJson
{
    public static JsonObject Object(string? json) => string.IsNullOrWhiteSpace(json) ? new() : JsonNode.Parse(json) as JsonObject ?? new();

    public static string? Take(JsonObject? root, string key)
    {
        if (root?[key] is not JsonValue value || !value.TryGetValue<string>(out var text)) return null;
        root.Remove(key);
        return text;
    }

    public static void Put(JsonObject root, string key, string? value)
    {
        if (value is not null) root[key] = value;
    }
}
