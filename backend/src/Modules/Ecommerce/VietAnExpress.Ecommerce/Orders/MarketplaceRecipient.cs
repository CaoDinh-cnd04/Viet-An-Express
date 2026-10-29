namespace VietAnExpress.Ecommerce.Orders;

/// <summary>Người nhận (owned type — các cột Nguoi_Nhan… của dbo.DonTMDT).</summary>
internal sealed record MarketplaceRecipient(
    string? Name, string? Company, string? Phone, string? Email,
    string? Address1, string? Address2, string? City, string? Province, string? PostalCode,
    string? CountryCode, string? CountryName)
{
    public static readonly MarketplaceRecipient Empty = new(null, null, null, null, null, null, null, null, null, null, null);
}
