namespace VietAnExpress.Customers.Contracts;

/// <summary>
/// Cổng gọi đồng bộ vào module Customers cho module khác (hồ sơ khách ở dbo.TCustomer).
/// Module khác KHÔNG đọc dbo.TCustomer trực tiếp — chỉ qua interface này.
/// </summary>
public interface ICustomersApi
{
    /// <param name="customerId">dbo.TCustomer.CustomerID.</param>
    Task<CustomerSummary?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>Nhiều khách một lần (bỏ mã không tồn tại) — vd lấy email để gửi thông báo.</summary>
    Task<IReadOnlyList<CustomerSummary>> GetByIdsAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken = default);
}

/// <summary>Thông tin khách đủ dùng cho module khác. <see cref="Id"/> = CustomerID, cũng là CustomerID trong dbo.MaVanDon.</summary>
public sealed record CustomerSummary(
    long Id,
    string Code,
    string CompanyName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxCode);
