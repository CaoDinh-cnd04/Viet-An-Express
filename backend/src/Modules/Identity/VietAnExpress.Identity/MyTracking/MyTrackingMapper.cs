using System.Globalization;
using System.Text.Json.Nodes;
using VietAnExpress.Customers.Contracts;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Dựng cấu hình MyTracking như frontend dùng (features/mytracking/schema.ts): phần mở rộng từ TCustomer_MyTracking (có thể chưa có dòng),
/// thương hiệu + cổng liên lạc từ TCustomer (<see cref="MyTrackingBrand"/>); ảnh đổi sang link hiển thị.
/// Tên công ty / địa chỉ / điện thoại: khách đã sửa (TCustomer_MyTracking) thì dùng, không thì theo hồ sơ khách.
/// </summary>
internal static class MyTrackingMapper
{
    /// <summary>Mã khách dùng trong link /tracking/{mã khách}/...; khách thiếu mã thì dùng CustomerID.</summary>
    public static string CustomerCode(long customerId, CustomerSummary? customer)
    {
        var code = customer?.Code?.Trim();
        return string.IsNullOrEmpty(code) ? customerId.ToString(CultureInfo.InvariantCulture) : code;
    }

    public static string ConfigJson(MyTrackingPage? page, MyTrackingBrandView brand, string? companyName)
    {
        var profile = MyTrackingProfile.From(brand, companyName);
        return new JsonObject
        {
            ["title"] = page?.Title ?? "",
            ["description"] = page?.Description ?? "",
            ["background"] = page?.Background ?? "",
            ["brand"] = new JsonObject
            {
                ["companyName"] = page?.CompanyName ?? profile.CompanyName,
                ["address"] = page?.Address ?? profile.Address,
                ["phone"] = page?.Phone ?? profile.Phone,
                ["logo"] = brand.Logo ?? "",
                ["website"] = brand.Brand?.Website?.Trim() ?? "",
                ["primaryColor"] = brand.Brand?.Color01?.Trim() ?? "",
                ["secondaryColor"] = brand.Brand?.Color02?.Trim() ?? "",
                ["contacts"] = brand.Brand?.Contacts() ?? [],
                ["socials"] = page?.Socials() ?? []
            },
            ["images"] = page?.Images() ?? []
        }.ToJsonString();
    }

    public static MyTrackingDto ToDto(MyTrackingPage? page, MyTrackingBrandView brand, long customerId, CustomerSummary? customer, MyTrackingImageStore images) =>
        new(new RawJson(images.ForDisplay(ConfigJson(page, brand, customer?.TrackingName))),
            CustomerCode(customerId, customer), customer?.TrackingName ?? "");
}
