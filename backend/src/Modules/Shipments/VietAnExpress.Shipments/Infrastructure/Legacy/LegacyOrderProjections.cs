using System.Linq.Expressions;

namespace VietAnExpress.Shipments.Infrastructure.Legacy;

/// <summary>Các tập cột chỉ đọc; Select chạy tại SQL trước khi tải dữ liệu qua mạng.</summary>
internal static class LegacyOrderProjections
{
    public static readonly Expression<Func<LegacyOrder, LegacyOrder>> List = o => new LegacyOrder
    {
        Id = o.Id, OrderNumber = o.OrderNumber, VaBill = o.VaBill, CustomerBill = o.CustomerBill, BillConnect = o.BillConnect,
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
