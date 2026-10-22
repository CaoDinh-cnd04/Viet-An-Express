namespace VietAnExpress.Shipments.Application.Orders;

// Khớp form tạo đơn của frontend (web/src/features/create-order/schema.ts, CreateOrderValues).
// Giá trị số giữ dạng chuỗi đúng như frontend gửi; đổi sang số trong LegacyOrderFactory.
// Dùng class + giá trị mặc định để đọc được cả payload cũ thiếu trường.

internal sealed class OrderPayload
{
    public ShipperPart Shipper { get; init; } = new();
    public ServicePart Service { get; init; } = new();
    public ShipmentPart Shipment { get; init; } = new();
    public ReceiverPart Receiver { get; init; } = new();
    public GoodsPart Goods { get; init; } = new();
    public List<PackagePart> Packages { get; init; } = [];
    public InvoicePart Invoice { get; init; } = new();

    internal sealed class ShipperPart
    {
        public string Company { get; init; } = "";
        /// <summary>Tên shipper gốc — dành cho khách là đơn vị forwarder (ghi vào Ten_Khach_Cua_FWD).</summary>
        public string OriginalShipper { get; init; } = "";
        public string Contact { get; init; } = "";
        public string Tel { get; init; } = "";
        public string Address { get; init; } = "";
        public string TaxId { get; init; } = "";
        public string Email { get; init; } = "";
        public string Country { get; init; } = "";
        public string Branch { get; init; } = "";
    }

    internal sealed class ServicePart
    {
        public string Carrier { get; init; } = "";
        /// <summary>Vd "DHL - Singapore".</summary>
        public string Hub { get; init; } = "";
        public string Reference { get; init; } = "";
    }

    internal sealed class ShipmentPart
    {
        /// <summary>DOC | PACK.</summary>
        public string Type { get; init; } = "PACK";
        public string Pieces { get; init; } = "";
        public string GrossWeight { get; init; } = "";
    }

    internal sealed class ReceiverPart
    {
        public string CountryCode { get; init; } = "";
        public string IossNo { get; init; } = "";
        public string EoriNo { get; init; } = "";
        public string Country { get; init; } = "";
        public string City { get; init; } = "";
        public string Company { get; init; } = "";
        public string Contact { get; init; } = "";
        public string Tel { get; init; } = "";
        /// <summary>Mã điện thoại quốc gia, vd "+1" (tự điền theo nước đến).</summary>
        public string PhoneCode { get; init; } = "";
        public string TaxId { get; init; } = "";
        public string Email { get; init; } = "";
        public string Postal { get; init; } = "";
        public string State { get; init; } = "";
        public string Addr1 { get; init; } = "";
        public string Addr2 { get; init; } = "";
        public string Addr3 { get; init; } = "";
    }

    internal sealed class GoodsPart
    {
        public string Category { get; init; } = "";
        public string Description { get; init; } = "";
        public string DocContent { get; init; } = "";
    }

    internal sealed class PackagePart
    {
        /// <summary>Loại bao bì dạng mã (CARTON, BAG…) — file Excel (Pack_Type_n).</summary>
        public string Type { get; init; } = "";
        /// <summary>Loại bao bì chọn trên form tạo đơn: "Thùng carton", "Bao / túi", "Pallet", "Kiện gỗ".</summary>
        public string Packaging { get; init; } = "";
        public string Qty { get; init; } = "";
        public string Length { get; init; } = "";
        public string Width { get; init; } = "";
        public string Height { get; init; } = "";
        /// <summary>Cân 1 kiện (kg).</summary>
        public string Weight { get; init; } = "";
        /// <summary>Nhóm hàng hóa của dòng kiện (dbo.NhomHangHoa) — form tạo đơn.</summary>
        public string Category { get; init; } = "";
        /// <summary>Mô tả mặt hàng — chỉ có khi nhóm là "Nhiều loại hàng".</summary>
        public string Description { get; init; } = "";
    }

    internal sealed class InvoicePart
    {
        /// <summary>Lý do xuất hàng / loại hình: GIFT, SAMPLE, Kinh doanh…</summary>
        public string ExportType { get; init; } = "";
        /// <summary>Hình thức chịu thuế: DDU (người nhận chịu thuế) | DDP (người gửi chịu thuế).</summary>
        public string DutyTerms { get; init; } = "";
        public string Currency { get; init; } = "USD";
        public string ShippingFee { get; init; } = "";
        /// <summary>Tổng giá trị khai báo khi không có dòng hàng (vd chứng từ nhập từ Excel, cột Invoice_Value).</summary>
        public string DeclaredValue { get; init; } = "";
        public List<InvoiceItemPart> Items { get; init; } = [];
    }

    internal sealed class InvoiceItemPart
    {
        public string DescEn { get; init; } = "";
        public string DescVi { get; init; } = "";
        public string Manufacturer { get; init; } = "";
        public string Origin { get; init; } = "";
        public string Hs { get; init; } = "";
        public string Qty { get; init; } = "";
        public string Unit { get; init; } = "";
        public string Price { get; init; } = "";
        /// <summary>Kiện số mấy (1..tổng số kiện) khi tuyến khai invoice riêng từng kiện; "" = invoice chung.</summary>
        public string Package { get; init; } = "";
    }
}
