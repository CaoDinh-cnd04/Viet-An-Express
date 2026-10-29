using System.Globalization;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>Đơn e-com → dòng hiển thị (danh sách, sau khi nhập tay / sửa).</summary>
internal static class EcomOrderMapper
{
    public static EcomOrderDto ToDto(MarketplaceOrder o)
    {
        var products = OrderData.Products(o);
        return new(
            o.Id.ToString(CultureInfo.InvariantCulture), o.Source, o.OrderName, o.Bill ?? "",
            o.Recipient.Name ?? o.Recipient.Company ?? "", o.Recipient.CountryName ?? o.Recipient.CountryCode ?? "",
            o.ItemCount, o.WeightKg ?? 0, o.Status, o.Note,
            o.ProductsJson is null ? null : products,
            (o.PlacedAt ?? o.CreateDate).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            o.TotalAmount, o.Currency,
            new EcomReceiverDto(o.Recipient.Name, o.Recipient.Company, o.Recipient.Phone, o.Recipient.Email, o.Recipient.Address1, o.Recipient.Address2,
                o.Recipient.City, o.Recipient.Province, o.Recipient.PostalCode, o.Recipient.CountryCode, o.Recipient.CountryName),
            o.Service, o.Hub, o.Branch,
            OrderData.Issues(o, products), o.Bill is null,
            o.ConfirmedAt is not null, o.ConfirmedAt?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            Deletable: o.CanDelete);
    }
}
