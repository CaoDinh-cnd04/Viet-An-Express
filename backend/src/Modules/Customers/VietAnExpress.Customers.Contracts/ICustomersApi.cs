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
    /// Nhân viên Việt An phụ trách khách (TCustomer.Staff_ID → USERS) — null khi chưa phân công hoặc nhân viên đã xóa.
    /// Chuẩn bị cho CMS / luồng "nhân viên phụ trách" (backend/docs/CMS_NHAN_VIEN_PHU_TRACH.md); portal chưa hiển thị.
    /// </summary>
    Task<AccountManager?> GetAccountManagerAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên phụ trách của nhiều khách một lần (bỏ khách chưa phân công) — vd danh sách yêu cầu ở trang quản trị.</summary>
    Task<IReadOnlyDictionary<long, AccountManager>> GetAccountManagersAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken = default);
}

/// <summary>Nhân viên Việt An (dbo.USERS) phụ trách khách. <see cref="Id"/> = USERS.ID = TCustomer.Staff_ID.</summary>
public sealed record AccountManager(int Id, string UserName, string FullName, string? Email, string? Phone, int? BranchId, bool Active);

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
