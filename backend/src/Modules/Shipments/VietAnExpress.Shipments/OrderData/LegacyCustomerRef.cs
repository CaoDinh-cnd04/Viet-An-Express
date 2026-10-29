using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.OrderData;

/// <summary>Khách sở hữu đơn, theo mã của hệ thống cũ (dbo.TCustomer).</summary>
internal sealed record LegacyCustomerRef(long LegacyCustomerId, string CompanyName, string? ContactName, string? Phone, string? Email);
