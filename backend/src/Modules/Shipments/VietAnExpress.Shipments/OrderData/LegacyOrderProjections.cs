using System.Linq.Expressions;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>Các tập cột chỉ đọc; Select chạy tại SQL trước khi tải dữ liệu qua mạng.</summary>
internal static class LegacyOrderProjections
{
    public static readonly Expression<Func<LegacyOrder, LegacyOrder>> List = o => new LegacyOrder
    {
        Id = o.Id, OrderNumber = o.OrderNumber, VaBill = o.VaBill, CustomerBill = o.CustomerBill, BillConnect = o.BillConnect,
        CustomerId = o.CustomerId, CustomerName = o.CustomerName, CreateUser = o.CreateUser, CustomerStaffId = o.CustomerStaffId,
        SenderName = o.SenderName,
        ConsigneeName = o.ConsigneeName, ConsigneeContactName = o.ConsigneeContactName,
        ConsigneePhone = o.ConsigneePhone, ConsigneeCountry = o.ConsigneeCountry,
        ConsigneeCity = o.ConsigneeCity, ConsigneePostalCode = o.ConsigneePostalCode,
        ConsigneeState = o.ConsigneeState, ConsigneeAddress1 = o.ConsigneeAddress1,
        ConsigneeAddress2 = o.ConsigneeAddress2, ConsigneeAddress3 = o.ConsigneeAddress3,
        ConsigneeVatTax = o.ConsigneeVatTax, ConsigneeEmail = o.ConsigneeEmail,
        ConsigneeIossNo = o.ConsigneeIossNo, ConsigneeEoriNo = o.ConsigneeEoriNo,
        ServiceName = o.ServiceName, CreateDate = o.CreateDate, SentDate = o.SentDate,
        GoodsName = o.GoodsName, Pieces = o.Pieces, WeightKg = o.WeightKg, Pod = o.Pod, PodEstimate = o.PodEstimate,
        RemoteAreaFedEx = o.RemoteAreaFedEx, RemoteAreaUps = o.RemoteAreaUps, RemoteArea = o.RemoteArea
    };

    /// <summary>Bảng kê gửi hàng (Printing/OrderSheet) — chỉ cột in ra, không kéo cả dòng MaVanDon (hàng chục cột) khi xuất tới 10.000 đơn.</summary>
    public static readonly Expression<Func<LegacyOrder, LegacyOrder>> Export = o => new LegacyOrder
    {
        Id = o.Id, OrderNumber = o.OrderNumber, CustomerBill = o.CustomerBill, BillConnect = o.BillConnect,
        CustomerId = o.CustomerId, CustomerName = o.CustomerName,
        ConsigneeName = o.ConsigneeName, ConsigneeContactName = o.ConsigneeContactName, ConsigneePhone = o.ConsigneePhone,
        ConsigneeAddress1 = o.ConsigneeAddress1, ConsigneeAddress2 = o.ConsigneeAddress2, ConsigneeAddress3 = o.ConsigneeAddress3,
        ConsigneeCity = o.ConsigneeCity, ConsigneeCountry = o.ConsigneeCountry, ServiceName = o.ServiceName,
        CreateDate = o.CreateDate, SentDate = o.SentDate, Pod = o.Pod,
        Pieces = o.Pieces, WeightKg = o.WeightKg, GoodsName = o.GoodsName, GoodsValue = o.GoodsValue, Currency = o.Currency
    };

    public static readonly Expression<Func<LegacyOrder, LegacyOrder>> Tracking = o => new LegacyOrder
    {
        Id = o.Id, OrderNumber = o.OrderNumber, VaBill = o.VaBill, BillConnect = o.BillConnect, Awb = o.Awb, CustomerBill = o.CustomerBill,
        ConsigneeCity = o.ConsigneeCity, ConsigneeCountry = o.ConsigneeCountry, ServiceName = o.ServiceName,
        Pod = o.Pod, PodEstimate = o.PodEstimate, SentDate = o.SentDate, CreateDate = o.CreateDate,
        Pieces = o.Pieces, WeightKg = o.WeightKg, SenderCountryId = o.SenderCountryId
    };

    public static readonly Expression<Func<LegacyOrder, LegacyOrder>> Events = o => new LegacyOrder
    {
        Id = o.Id, Pod = o.Pod, SentDate = o.SentDate, CreateDate = o.CreateDate, ServiceName = o.ServiceName, BillConnect = o.BillConnect
    };

    public static readonly Expression<Func<LegacyOrder, LegacyOrder>> InvoiceHeader = o => new LegacyOrder
    {
        Id = o.Id, OrderNumber = o.OrderNumber, VaBill = o.VaBill, ConsigneeName = o.ConsigneeName, CreateDate = o.CreateDate, Currency = o.Currency
    };
}
