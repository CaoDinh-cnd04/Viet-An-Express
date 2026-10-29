using System.Globalization;
using System.Text.Json;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>
/// Đơn chưa in nằm luôn trong dbo.MaVanDon với Status = 0, chưa có số bill (OrderNumber NULL; VA_Bill NULL như hệ thống cũ).
/// Kiện / dòng hàng ghi vào MaVanDon_PCS_DIM / MaVanDon_ChiTietHang như đơn đã in. Chỉ lưu thông tin có cột trong các bảng này.
/// Lớp này chỉ gồm hàm thuần (dựng dòng, kiểm tra đủ thông tin, map DTO); đọc / ghi DB ở <see cref="DraftStore"/>.
/// </summary>
internal static class DraftOrders
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Dựng dòng MaVanDon chưa in từ form. Hàm thuần — có test.</summary>
    public static LegacyOrder Build(OrderPayload p, LegacyCustomerRef customer, DateTime todayVn, int? consigneeCountryId)
    {
        var order = LegacyOrderFactory.FromPayload(p, customer, orderNumber: 0, todayVn, consigneeCountryId);
        order.OrderNumber = null;
        // VA_Bill để NULL như hệ thống cũ (23/10/2026) — không lưu được chi nhánh; mở lại nháp mặc định TP.HCM.
        order.VaBill = null;
        order.Status = LegacyOrder.DraftStatus;
        // Form chưa khai hàng: không ghi chữ mặc định "Goods" / "Documents" để mở lại nháp vẫn trống.
        var isDoc = string.Equals(p.Shipment.Type, "DOC", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(isDoc ? p.Goods.DocContent : LegacyOrderFactory.GoodsName(p))) order.GoodsName = null;
        return order;
    }

    /// <summary>
    /// "Chờ in" (ready) khi đủ thông tin để cấp bill: người gửi, người nhận (tên, nước, địa chỉ), dịch vụ, cân &gt; 0 — còn lại là "nháp".
    /// Hàm thuần — có test.
    /// </summary>
    public static bool IsReady(LegacyOrder o) =>
        !string.IsNullOrWhiteSpace(o.SenderName)
        && !string.IsNullOrWhiteSpace(o.ConsigneeName)
        && !string.IsNullOrWhiteSpace(o.ConsigneeCountry)
        && !string.IsNullOrWhiteSpace(o.ConsigneeAddress1)
        && !string.IsNullOrWhiteSpace(o.ServiceName)
        && o.WeightKg > 0;

    public static DraftDto ToDto(LegacyOrder o, IReadOnlyList<LegacyPackageLine> packages, IReadOnlyList<LegacyInvoiceLine> items)
    {
        var weight = (o.WeightKg ?? 0).ToString("0.##", CultureInfo.InvariantCulture);
        return new DraftDto(
            o.Id.ToString(CultureInfo.InvariantCulture),
            IsReady(o) ? DraftInput.StatusReady : DraftInput.StatusDraft,
            o.ConsigneeName ?? "", o.ConsigneeCountry ?? "", o.ServiceName?.Replace("|", " - ") ?? "",
            VaBillCode.BranchOfPrefix(o.VaBill) ?? "", o.CustomerBill ?? "",
            $"{o.Pieces ?? 0} kiện · {weight} kg", o.GoodsName ?? "",
            (o.ModifyDate ?? o.CreateDate)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
            JsonSerializer.SerializeToElement(OrderCopy.FromLegacy(o, packages, items), Json));
    }
}
