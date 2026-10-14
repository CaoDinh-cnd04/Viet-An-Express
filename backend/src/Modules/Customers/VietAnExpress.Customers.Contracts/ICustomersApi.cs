using System.Data.Common;

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

    /// <summary>
    /// Ghi thông tin người gửi vào dbo.TCustomer — chỉ gọi khi quản trị Việt An đã duyệt yêu cầu sửa của khách.
    /// Trả về hồ sơ sau khi ghi; null = không có khách này.
    /// </summary>
    Task<CustomerSummary?> UpdateSenderInfoAsync(long customerId, SenderInfoUpdate update, CancellationToken cancellationToken = default);
    /// <summary>Join the review transaction; lock the profile and compare only fields being changed before writing.</summary>
    Task<SenderInfoWriteResult> UpdateSenderInfoInTransactionAsync(long customerId, SenderInfoUpdate update, SenderInfoUpdate expected,
        DbTransaction transaction, CancellationToken cancellationToken = default);
}

public sealed record SenderInfoWriteResult(CustomerSummary? Current, bool Outdated);

/// <summary>
/// Trường cần ghi: null = giữ nguyên, chuỗi rỗng = xóa trắng. Ghi vào CustomerName, ContactName, ContactPhone, ContactEmail, Address1, TaxCode
/// (đúng các cột <see cref="CustomerSummary"/> ưu tiên đọc).
/// </summary>
public sealed record SenderInfoUpdate(
    string? CompanyName = null,
    string? ContactName = null,
    string? Phone = null,
    string? Email = null,
    string? Address = null,
    string? TaxCode = null);

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
