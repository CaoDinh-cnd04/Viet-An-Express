using VietAnExpress.Shipments.Geo;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>
/// Ghi kết quả VSVX vào 3 cột của dbo.MaVanDon lúc cấp bill, cùng định dạng hệ thống cũ:
/// Remote_Area_FedEx = "Fedex (Tier B)", Remote_Area_UPS = "UPS", Remote_Area = hãng khác (Au-Post, CT-VA…). Hàm thuần — có test.
/// </summary>
internal static class RemoteAreaColumns
{
    /// <summary>Mã nước ISO của người nhận: lấy mã form gửi, thiếu (nháp cũ) thì đổi từ tên nước theo danh sách nước của form.</summary>
    public static string CountryCode(OrderPayload p, IReadOnlyList<CountryInfo> countries)
    {
        if (p.Receiver.CountryCode.Trim() is { Length: 2 } cc) return cc.ToUpperInvariant();
        var name = p.Receiver.Country.Trim();
        return countries.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Code
            ?? EuCountries.CodeOf(name) ?? "";
    }

    public static void Apply(LegacyOrder order, IReadOnlyList<RemoteAreaHit> hits)
    {
        static string Label(RemoteAreaHit h) => h.Tier is null ? h.Carrier : $"{h.Carrier} ({h.Tier})";
        static bool Is(RemoteAreaHit h, string carrier) => h.Carrier.Equals(carrier, StringComparison.OrdinalIgnoreCase);

        order.RemoteAreaFedEx = Clip(string.Join(", ", hits.Where(h => Is(h, "Fedex")).Select(Label)), 200);
        order.RemoteAreaUps = Clip(string.Join(", ", hits.Where(h => Is(h, "UPS")).Select(Label)), 200);
        order.RemoteArea = Clip(string.Join(", ", hits.Where(h => !Is(h, "Fedex") && !Is(h, "UPS")).Select(Label)), 50);
    }

    private static string? Clip(string value, int max) => value.Length == 0 ? null : value.Length <= max ? value : value[..max];
}
