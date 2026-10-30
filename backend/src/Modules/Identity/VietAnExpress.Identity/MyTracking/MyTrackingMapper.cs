using System.Globalization;
using System.Text.Json.Nodes;
using VietAnExpress.Customers.Contracts;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Dựng cấu hình MyTracking như frontend dùng (features/mytracking/schema.ts): phần mở rộng từ TCustomer_MyTracking (có thể chưa có dòng),
/// thương hiệu + cổng liên lạc từ TCustomer (<see cref="MyTrackingBrand"/>); ảnh đổi sang link hiển thị, tên công ty theo hồ sơ khách.
/// </summary>
internal static class MyTrackingMapper
{
    /// <summary>Mã khách dùng trong link /tracking/{mã khách}/...; khách thiếu mã thì dùng CustomerID.</summary>
    public static string CustomerCode(long customerId, CustomerSummary? customer)
    {
        var code = customer?.Code?.Trim();
        return string.IsNullOrEmpty(code) ? customerId.ToString(CultureInfo.InvariantCulture) : code;
    }

    public static string ConfigJson(MyTrackingPage? page, MyTrackingBrandView brand, string? companyName) => MyTrackingJson.WithCompanyName(new JsonObject
    {
        ["title"] = page?.Title ?? "",
        ["description"] = page?.Description ?? "",
        ["background"] = page?.Background ?? "",
        ["brand"] = new JsonObject
        {
            ["companyName"] = "",
            ["address"] = brand.Brand?.Address?.Trim() ?? "",
            ["phone"] = brand.Brand?.Phone?.Trim() ?? "",
            ["logo"] = brand.Logo ?? "",
            ["website"] = brand.Brand?.Website?.Trim() ?? "",
            ["primaryColor"] = brand.Brand?.Color01?.Trim() ?? "",
            ["secondaryColor"] = brand.Brand?.Color02?.Trim() ?? "",
            ["contacts"] = brand.Brand?.Contacts() ?? [],
            ["socials"] = page?.Socials() ?? []
        },
        ["images"] = page?.Images() ?? []
    }.ToJsonString(), companyName);

    public static MyTrackingDto ToDto(MyTrackingPage? page, MyTrackingBrandView brand, long customerId, CustomerSummary? customer, MyTrackingImageStore images) =>
        new(new RawJson(images.ForDisplay(ConfigJson(page, brand, customer?.TrackingName))),
            CustomerCode(customerId, customer), customer?.TrackingName ?? "");
}
