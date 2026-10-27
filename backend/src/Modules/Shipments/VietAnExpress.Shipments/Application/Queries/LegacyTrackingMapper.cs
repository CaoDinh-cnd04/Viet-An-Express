using System.Globalization;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Infrastructure.Carriers.HubEz;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Queries;

/// <summary>Đổi 1 dòng dbo.MaVanDon thành kết quả tra cứu công khai cùng dạng với vận đơn mới.</summary>
internal static class LegacyTrackingMapper
{
    /// <summary>Mã người dùng nhập có khớp dòng này không (so với số VA, mã hãng, AWB, bill khách).</summary>
    public static bool Matches(LegacyOrder o, string bill) =>
        (o.OrderNumber is { } number && Orders.VaBillCode.Number(bill) == number)
        || string.Equals(o.BillConnect?.Trim(), bill, StringComparison.OrdinalIgnoreCase)
        || string.Equals(o.Awb?.Trim(), bill, StringComparison.OrdinalIgnoreCase)
        || string.Equals(o.CustomerBill?.Trim(), bill, StringComparison.OrdinalIgnoreCase);

    /// <param name="carrier">Hành trình HUB-EZ khi đơn đi HUB-EZ (ghép vào hành trình cũ).</param>
    public static PublicTrackResultDto ToResult(string bill, LegacyOrder o, DateTime todayVn, HubEzTrack? carrier = null)
    {
        var destination = string.Join(", ", new[] { o.ConsigneeCity, o.ConsigneeCountry }
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrEmpty(s)));
        // "DHL|Singapore" → "DHL"
        var service = o.ServiceName?.Split('|')[0].Trim();

        return new PublicTrackResultDto(bill, Found: true,
            LegacyOrderStatus.Of(o, todayVn),
            string.IsNullOrEmpty(destination) ? null : destination,
            string.IsNullOrEmpty(service) ? null : service,
            // Trang công khai: không đưa tên người ký nhận.
            HubEzEvents.Merge(LegacyOrderView.Events(o, hideSigner: true), carrier, !string.IsNullOrWhiteSpace(o.Pod)).Select(e => new PublicTrackEventDto(e.Time, e.Title, e.Location)).ToList(),
            Origin: Origin(o),
            ShipDate: Date(o.SentDate),
            EstimatedDate: Date(o.PodEstimate),
            Pieces: o.Pieces,
            WeightKg: o.WeightKg,
            CarrierBill: CarrierBill(o));
    }


    /// <summary>Nơi gửi: Việt An chỉ nhận hàng xuất đi từ Việt Nam; mã quốc gia khác thì chưa có danh mục để đổi tên → bỏ trống.</summary>
    private static string? Origin(LegacyOrder o) =>
        o.SenderCountryId is null or Orders.LegacyOrderFactory.LegacyVietnamCountryId ? "Việt Nam" : null;

    private static string? CarrierBill(LegacyOrder o)
    {
        var connect = o.BillConnect?.Trim();
        return string.IsNullOrEmpty(connect) || connect == o.OrderNumber?.ToString(CultureInfo.InvariantCulture) || connect == o.VaBill ? null : connect;
    }

    private static string? Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
