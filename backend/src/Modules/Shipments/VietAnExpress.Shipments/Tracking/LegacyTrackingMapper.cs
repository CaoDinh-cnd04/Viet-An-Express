using System.Globalization;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>Đổi 1 dòng dbo.MaVanDon thành kết quả tra cứu công khai cùng dạng với vận đơn mới.</summary>
internal static class LegacyTrackingMapper
{
    /// <summary>Mã người dùng nhập có khớp dòng này không (so với số VA, mã hãng, AWB, bill khách).</summary>
    public static bool Matches(LegacyOrder o, string bill) =>
        (o.OrderNumber is { } number && VaBillCode.Number(bill) == number)
        || string.Equals(o.BillConnect?.Trim(), bill, StringComparison.OrdinalIgnoreCase)
        || string.Equals(o.Awb?.Trim(), bill, StringComparison.OrdinalIgnoreCase)
        || string.Equals(o.CustomerBill?.Trim(), bill, StringComparison.OrdinalIgnoreCase);

    /// <param name="carrier">Hành trình HUB-EZ khi đơn đi HUB-EZ (ghép vào hành trình cũ).</param>
    /// <param name="legacy">Vận đơn đã khai thác ở dbo.Orders + hành trình dbo.Order_Tracking (như trang tracking cũ).</param>
    public static PublicTrackResultDto ToResult(string bill, LegacyOrder o, DateTime todayVn, HubEzTrack? carrier = null, OrderTrack? legacy = null)
    {
        var destination = string.Join(", ", new[] { o.ConsigneeCity, o.ConsigneeCountry }
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrEmpty(s)));
        // "DHL|Singapore" → "DHL"
        var service = o.ServiceName?.Split('|')[0].Trim();

        return new PublicTrackResultDto(bill, Found: true,
            Status(LegacyOrderStatus.Of(o, todayVn), legacy),
            string.IsNullOrEmpty(destination) ? legacy?.Destination : destination,
            string.IsNullOrEmpty(service) ? null : service,
            // Trang công khai: không đưa tên người ký nhận.
            HubEzEvents.Merge(LegacyOrderView.Events(o, hideSigner: true), Carrier(carrier, legacy), !string.IsNullOrWhiteSpace(o.Pod)).Select(e => new PublicTrackEventDto(e.Time, e.Title, e.Location)).ToList(),
            Origin: Origin(o),
            ShipDate: Date(o.SentDate ?? legacy?.ShipDate),
            EstimatedDate: Date(o.PodEstimate ?? legacy?.EstimateDate),
            Pieces: o.Pieces,
            WeightKg: o.WeightKg,
            CarrierBill: CarrierBill(o));
    }


    /// <summary>Vận đơn chỉ có ở dbo.Orders (nhân viên tạo thẳng ở hệ thống cũ, không có dòng MaVanDon) — trang tracking cũ vẫn tra được.</summary>
    public static PublicTrackResultDto FromOrder(string bill, OrderTrack legacy) =>
        new(bill, Found: true,
            legacy.Delivered ? LegacyOrderStatus.Delivered : LegacyOrderStatus.InTransit,
            legacy.Destination,
            legacy.Service,
            (legacy.Track?.Events ?? []).Select(e => new PublicTrackEventDto(
                e.Time?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "", e.Description, e.Location)).ToList(),
            Origin: "Việt Nam",
            ShipDate: Date(legacy.ShipDate),
            EstimatedDate: Date(legacy.EstimateDate));

    /// <summary>Hành trình hãng: HUB-EZ nếu có sự kiện, không thì hành trình hệ thống cũ (Order_Tracking).</summary>
    public static HubEzTrack? Carrier(HubEzTrack? hubEz, OrderTrack? legacy) =>
        hubEz is { Events.Count: > 0 } ? hubEz : legacy?.Track ?? hubEz;

    /// <summary>Đã có ở dbo.Orders = Việt An đã nhận / khai thác hàng → không còn "chờ gửi"; Order_Tracking báo đã giao → đã giao.</summary>
    private static string Status(string status, OrderTrack? legacy) => legacy switch
    {
        null => status,
        { Delivered: true } => LegacyOrderStatus.Delivered,
        _ when status == LegacyOrderStatus.Waiting => LegacyOrderStatus.InTransit,
        _ => status
    };

    /// <summary>Nơi gửi: Việt An chỉ nhận hàng xuất đi từ Việt Nam; mã quốc gia khác thì chưa có danh mục để đổi tên → bỏ trống.</summary>
    private static string? Origin(LegacyOrder o) =>
        o.SenderCountryId is null or LegacyOrderFactory.LegacyVietnamCountryId ? "Việt Nam" : null;

    private static string? CarrierBill(LegacyOrder o)
    {
        var connect = o.BillConnect?.Trim();
        return string.IsNullOrEmpty(connect) || connect == o.OrderNumber?.ToString(CultureInfo.InvariantCulture) || connect == o.VaBill ? null : connect;
    }

    private static string? Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
