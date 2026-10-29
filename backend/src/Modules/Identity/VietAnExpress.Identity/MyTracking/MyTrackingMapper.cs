using System.Globalization;
using VietAnExpress.Customers.Contracts;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>Dựng phản hồi cấu hình MyTracking: ảnh đổi sang link hiển thị, tên công ty luôn theo hồ sơ khách.</summary>
internal static class MyTrackingMapper
{
    /// <summary>Mã khách dùng trong link /tracking/{mã khách}/...; khách thiếu mã thì dùng CustomerID.</summary>
    public static string CustomerCode(long customerId, CustomerSummary? customer)
    {
        var code = customer?.Code?.Trim();
        return string.IsNullOrEmpty(code) ? customerId.ToString(CultureInfo.InvariantCulture) : code;
    }

    public static MyTrackingDto ToDto(MyTrackingPage p, long customerId, CustomerSummary? customer, MyTrackingImageStore images) =>
        new(new RawJson(images.ForDisplay(MyTrackingJson.WithCompanyName(p.ConfigJson, customer?.TrackingName))),
            CustomerCode(customerId, customer), customer?.TrackingName ?? "");
}
