using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>
/// Cấu hình HUB-EZ (section "HubEz") — chỉ lấy hành trình (tracking) theo mã hãng (MaVanDon.Bill_Connect).
/// Tài khoản nằm ở User Secrets khi dev / biến môi trường HubEz__UserName, HubEz__Password khi chạy thật.
/// Thử nghiệm: TrackingBaseUrl = https://test.hub-ez.com (UAT bắt buộc https — http bị chuyển hướng làm POST thành GET),
/// tài khoản test / 123456 (UAT công khai của HUB-EZ). PRD http://APITracking.hub-ez.com chạy http (https của máy này lỗi 500).
/// </summary>
internal sealed class HubEzOptions
{
    public const string Section = "HubEz";

    /// <summary>Máy chủ tracking (PRD tách riêng máy chủ đặt đơn): token và tracking đều lấy ở đây.</summary>
    [Required, Url]
    public string TrackingBaseUrl { get; set; } = "http://APITracking.hub-ez.com";

    /// <summary>Để trống thì tắt tracking HUB-EZ (chỉ hiện hành trình của hệ thống cũ).</summary>
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>
    /// Hub đi HUB-EZ, cách nhau dấu ";" — dạng như ô Hub của portal, vd "Chuyên tuyến - EU; Chuyên tuyến - UK";
    /// "Hãng - *" = mọi hub của hãng. Để trống = không đơn nào lấy tracking HUB-EZ.
    /// </summary>
    public string Hubs { get; set; } = "";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>Giữ kết quả tracking mỗi mã trong bao lâu trước khi hỏi lại HUB-EZ.</summary>
    [Range(1, 1440)]
    public int CacheMinutes { get; set; } = 10;
}
