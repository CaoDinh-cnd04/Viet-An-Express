using System.Globalization;
using VietAnExpress.Shipments.OrderData;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>
/// Kiểm tra đơn chưa in trước khi cấp bill — cùng các ô bắt buộc của form tạo đơn (web order-form/schema.ts),
/// để nút "In &amp; cấp bill" ở trang Đơn nháp không cấp bill cho đơn còn thiếu (vd chưa chọn hub). Hàm thuần — có test.
/// </summary>
internal static class PrintReadiness
{
    /// <summary>Tên các ô bắt buộc còn trống / sai; rỗng = đủ để in.</summary>
    public static IReadOnlyList<string> Missing(OrderPayload p)
    {
        var missing = new List<string>();
        void Need(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) missing.Add(label);
        }

        Need(p.Shipper.Company, "Tên người gửi");
        Need(p.Shipper.Contact, "Người liên hệ (người gửi)");
        Need(p.Shipper.Tel, "Điện thoại người gửi");
        Need(p.Shipper.Address, "Địa chỉ người gửi");
        Need(p.Service.Carrier, "Dịch vụ");
        Need(p.Service.Hub, "Hub");
        Need(p.Receiver.Company, "Tên người nhận");
        Need(p.Receiver.Contact, "Người liên hệ (người nhận)");
        Need(p.Receiver.Tel, "Điện thoại người nhận");
        Need(p.Receiver.Country, "Nước đến");
        Need(p.Receiver.City, "Thành phố");
        if (ReceiverStateRule.Required(p.Receiver.CountryCode, p.Receiver.Country)) Need(p.Receiver.State, "Tỉnh / bang (bắt buộc với Mỹ, Canada, Úc)");
        Need(p.Receiver.Addr1, "Địa chỉ 1");
        Need(p.Receiver.Addr2, "Địa chỉ 2");

        if (string.Equals(p.Shipment.Type, "DOC", StringComparison.OrdinalIgnoreCase))
        {
            if (Number(p.Shipment.Pieces) < 1) missing.Add("Số kiện");
            if (Number(p.Shipment.GrossWeight) <= 0) missing.Add("Cân nặng");
            return missing;
        }

        Need(p.Goods.Description, "Mô tả hàng hóa");
        if (p.Packages.Count == 0) missing.Add("Chi tiết kiện hàng");
        else if (p.Packages.Any(k => Number(k.Qty) < 1 || Number(k.Weight) <= 0)) missing.Add("Số lượng / cân nặng kiện");
        Need(p.Invoice.ExportType, "Hình thức xuất khẩu");
        if (p.Invoice.Items.Count == 0) missing.Add("Mặt hàng invoice");
        else if (p.Invoice.Items.Any(i => string.IsNullOrWhiteSpace(i.DescEn) || string.IsNullOrWhiteSpace(i.DescVi)
                     || string.IsNullOrWhiteSpace(i.Unit) || Number(i.Qty) <= 0 || Number(i.Price) < 0))
            missing.Add("Dòng hàng invoice (tên EN / VN, số lượng, đơn vị, đơn giá)");
        return missing;
    }

    /// <summary>Chuỗi số → số; trống / sai = -1 (không đạt mọi điều kiện ≥ 0).</summary>
    private static decimal Number(string value) =>
        decimal.TryParse(value?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : -1;
}
