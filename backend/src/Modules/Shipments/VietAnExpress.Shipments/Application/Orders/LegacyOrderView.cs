using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Infrastructure.Legacy;

namespace VietAnExpress.Shipments.Application.Orders;

/// <summary>
/// Bộ lọc trạng thái đơn theo dữ liệu dbo.MaVanDon (cột Status của hệ thống cũ không phân biệt trạng thái):
/// wait (chưa đi) = chưa có Ngày gửi · fly (đã đi) = có Ngày gửi (kể cả đơn đã phát) ·
/// nd (chưa phát) = chưa có POD · ok (đã phát) = có thông tin POD.
/// Các nhóm lọc có thể trùng nhau (đơn đã phát vẫn thuộc "đã đi"); chọn đủ 4 nhóm = mọi đơn.
/// Nhãn trên 1 dòng đơn (<see cref="Of"/>): có POD → ok · có Ngày gửi → fly · còn lại → wait — luôn thuộc nhóm lọc tương ứng.
/// </summary>
internal static class LegacyOrderStatus
{
    public const string Waiting = "wait";
    public const string InTransit = "fly";
    public const string NotDelivered = "nd";
    public const string Delivered = "ok";

    public static readonly string[] All = [Waiting, InTransit, NotDelivered, Delivered];

    /// <param name="today">Giữ để không đổi chữ ký gọi (bộ lọc hiện không phụ thuộc ngày).</param>
    public static Expression<Func<LegacyOrder, bool>> Is(string status, DateTime today) => status switch
    {
        Waiting => o => o.SentDate == null,
        InTransit => o => o.SentDate != null,
        NotDelivered => o => o.Pod == null || o.Pod == "",
        Delivered => o => o.Pod != null && o.Pod != "",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static string Of(LegacyOrder o, DateTime today)
    {
        if (!string.IsNullOrWhiteSpace(o.Pod)) return Delivered;
        return o.SentDate is null ? Waiting : InTransit;
    }
}

/// <summary>Đổi dòng dbo.MaVanDon sang dạng <c>Order</c> của frontend (web/src/features/orders/types.ts). Hàm thuần.</summary>
internal static partial class LegacyOrderView
{
    [GeneratedRegex(@"^\s*(\d{2}/\d{2}/\d{4})\s+(\d{2}:\d{2})[\s,]*(.*)$")]
    private static partial Regex PodPattern();

    [GeneratedRegex(@"^(delivered|signed for by:?)[\s,:]*", RegexOptions.IgnoreCase)]
    private static partial Regex PodPrefix();

    /// <summary>
    /// Mã bill hiển thị / in: số OrderNumber 7 chữ số như hệ thống cũ (vd 6179215) — không dùng mã VA_Bill (VAHCM6179215MY).
    /// Chưa có số (đơn nháp) → ID dòng. Cột VA_Bill vẫn ghi để biết chi nhánh / nước đến, tra cứu vẫn nhận cả 2 dạng.
    /// </summary>
    public static string BillOf(LegacyOrder o) =>
        o.OrderNumber?.ToString(CultureInfo.InvariantCulture) ?? o.Id.ToString(CultureInfo.InvariantCulture);

    public static OrderDto ToDto(LegacyOrder o, DateTime today)
    {
        var bill = BillOf(o);
        var connect = o.BillConnect?.Trim();
        return new OrderDto(
            Id: o.Id.ToString(CultureInfo.InvariantCulture),
            Seq: o.Id,
            Bill: bill,
            Ref: o.CustomerBill?.Trim() ?? "",
            Connect: string.IsNullOrEmpty(connect) || connect == bill ? null : connect,
            Cnee: o.ConsigneeName?.Trim() ?? "",
            Ct: o.ConsigneeCountry?.Trim() ?? "",
            Route: o.ServiceName?.Replace("|", " - ").Trim() ?? "",
            Branch: VaBillCode.BranchOf(o.VaBill) ?? "",
            Created: Date(o.CreateDate) ?? "",
            Sent: Date(o.SentDate),
            Type: IsDocument(o.GoodsName) ? "DOC" : "PACK",
            St: LegacyOrderStatus.Of(o, today),
            Pcs: $"{o.Pieces ?? 1} kiện · {(o.WeightKg ?? 0).ToString("0.##", CultureInfo.InvariantCulture)} kg",
            Content: o.GoodsName?.Trim() ?? "",
            Pod: ParsePod(o.Pod),
            Photos: 0,
            PodEstimate: Date(o.PodEstimate),
            RemoteArea: RemoteAreaText(o),
            ChargeableWeight: o.WeightKg);
    }

    /// <summary>Danh sách "Đơn hàng của tôi": kèm người nhận đầy đủ để bảng xổ chi tiết (người liên hệ, SĐT, địa chỉ) — cùng dòng đã đọc, không truy vấn thêm.</summary>
    public static OrderDto ToListDto(LegacyOrder o, DateTime today) => ToDto(o, today) with { Receiver = ReceiverOf(o) };

    /// <summary>Chi tiết đơn: thêm người gửi / người nhận đầy đủ để nhân bản đơn.</summary>
    public static OrderDto ToDetailDto(LegacyOrder o, DateTime today) => ToDto(o, today) with
    {
        Shipper = new OrderShipperDto(S(o.SenderName), S(o.SenderContactName), S(o.SenderPhone), S(o.SenderAddress), S(o.SenderTax), S(o.SenderEmail)),
        Receiver = ReceiverOf(o)
    };

    private static OrderReceiverDto ReceiverOf(LegacyOrder o) =>
        new(S(o.ConsigneeName), S(o.ConsigneeContactName), S(o.ConsigneePhone), S(o.ConsigneeCountry),
            S(o.ConsigneeCity), S(o.ConsigneePostalCode), S(o.ConsigneeState), S(o.ConsigneeAddress1), S(o.ConsigneeAddress2),
            S(o.ConsigneeAddress3), S(o.ConsigneeVatTax), S(o.ConsigneeEmail),
            S(o.ConsigneeIossNo), S(o.ConsigneeEoriNo), VaBillCode.CountryOf(o.VaBill) ?? Domain.EuCountries.CodeOf(o.ConsigneeCountry) ?? "");

    /// <summary>Chi tiết đầy đủ kèm kiện + invoice (đọc từ 2 bảng chi tiết).</summary>
    public static OrderDto ToDetailDto(LegacyOrder o, DateTime today, IEnumerable<LegacyPackageLine> packages, IEnumerable<LegacyInvoiceLine> items) =>
        ToDetailDto(o, today) with
        {
            Packages = packages.Select(ToPackageDto).ToList(),
            Invoice = new OrderInvoiceDto(S(o.Currency), S(o.ExportReason), o.ShippingFee, o.GoodsValue, items.Select(ToItemDto).ToList())
        };

    /// <summary>Cột TrongLuong là tổng cân của dòng → cân 1 kiện = TrongLuong / SoLuong.</summary>
    public static OrderPackageDto ToPackageDto(LegacyPackageLine p) =>
        new(p.Quantity, S(p.PackType), p.LengthCm, p.WidthCm, p.HeightCm,
            p.Quantity > 0 ? decimal.Round(p.WeightKg / p.Quantity, 3, MidpointRounding.AwayFromZero) : p.WeightKg,
            p.VolumetricKg, p.ChargeableKg);

    public static OrderItemDto ToItemDto(LegacyInvoiceLine i)
    {
        var qty = i.Quantity ?? 0;
        var price = i.UnitPrice ?? 0;
        return new OrderItemDto(S(i.DescriptionEn), S(i.DescriptionVi), qty, string.IsNullOrWhiteSpace(i.Unit) ? "PCS" : i.Unit.Trim(), price,
            i.Amount ?? decimal.Round(qty * price, 2, MidpointRounding.AwayFromZero), S(i.HsCode), S(i.Origin), i.Package);
    }

    private static string S(string? value) => value?.Trim() ?? "";

    /// <summary>"08/01/2026 16:28, DELIVERED LYNN TAN" → ngày, giờ, người ký. Chỉ trả khi đã giao.</summary>
    public static OrderPodDto? ParsePod(string? pod)
    {
        if (string.IsNullOrWhiteSpace(pod) || !pod.Contains("deliver", StringComparison.OrdinalIgnoreCase)) return null;
        var m = PodPattern().Match(pod);
        if (!m.Success) return new OrderPodDto("", "", "");

        // Bỏ các tiền tố "DELIVERED", "Signed for by:" để còn lại tên người ký.
        var signer = m.Groups[3].Value.Trim();
        for (var prev = ""; prev != signer;)
        {
            prev = signer;
            signer = PodPrefix().Replace(signer, "").Trim();
        }
        return new OrderPodDto(m.Groups[1].Value, m.Groups[2].Value, signer);
    }

    /// <summary>Hành trình rút từ dữ liệu cũ (mới nhất trước). <paramref name="hideSigner"/> cho trang công khai.</summary>
    public static IReadOnlyList<OrderEventDto> Events(LegacyOrder o, bool hideSigner)
    {
        var events = new List<OrderEventDto>();
        if (!string.IsNullOrWhiteSpace(o.Pod))
        {
            var m = PodPattern().Match(o.Pod);
            var time = m.Success ? $"{m.Groups[1].Value} {m.Groups[2].Value}" : "";
            var pod = ParsePod(o.Pod);
            var title = pod is null
                ? (m.Success ? m.Groups[3].Value : o.Pod).Trim()
                : hideSigner || string.IsNullOrEmpty(pod.Signer) ? "Đã giao hàng" : $"Đã giao hàng — người nhận: {pod.Signer}";
            events.Add(new OrderEventDto(time, title, null));
        }
        if (o.SentDate is { } sent) events.Add(new OrderEventDto(Date(sent)!, "Hàng đã rời kho Việt An Express", null));
        if (o.CreateDate is { } created) events.Add(new OrderEventDto(Date(created)!, "Đã tạo vận đơn", null));
        return events;
    }

    public static bool IsDocument(string? goodsName) => LegacyDocumentRule.Matches(goodsName);

    /// <summary>VSVX của mọi hãng đã ghi lúc cấp bill (3 cột FedEx · UPS · hãng khác), vd "Fedex (Tier B), UPS"; null khi không thuộc vùng.</summary>
    public static string? RemoteAreaText(LegacyOrder o)
    {
        var parts = new[] { o.RemoteAreaFedEx, o.RemoteAreaUps, o.RemoteArea }
            .Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? Date(DateTime? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
