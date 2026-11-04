namespace VietAnExpress.Customers.Contracts;

/// <summary>
/// Cổng gọi đồng bộ vào module Customers cho module khác (hồ sơ khách ở dbo.TCustomer).
/// Module khác KHÔNG đọc dbo.TCustomer trực tiếp — chỉ qua interface này.
/// </summary>
public interface ICustomersApi
{
    /// <param name="customerId">dbo.TCustomer.CustomerID.</param>
    Task<CustomerSummary?> GetByIdAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>Khách theo mã khách (TCustomer.CustomerCode, không phân biệt hoa thường) — vd link MyTracking /tracking/{mã khách}/....</summary>
    Task<CustomerSummary?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Nhiều khách một lần (bỏ mã không tồn tại) — vd lấy email để gửi thông báo.</summary>
    Task<IReadOnlyList<CustomerSummary>> GetByIdsAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhân viên Việt An phụ trách khách (TCustomer.Staff_ID → USERS) — null khi chưa phân công hoặc nhân viên đã xóa.
    /// Chuẩn bị cho CMS / luồng "nhân viên phụ trách" (backend/docs/CMS_NHAN_VIEN_PHU_TRACH.md); portal chưa hiển thị.
    /// </summary>
    Task<AccountManager?> GetAccountManagerAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên phụ trách của nhiều khách một lần (bỏ khách chưa phân công) — vd danh sách yêu cầu ở trang quản trị.</summary>
    Task<IReadOnlyDictionary<long, AccountManager>> GetAccountManagersAsync(IReadOnlyCollection<long> customerIds, CancellationToken cancellationToken = default);

    /// <summary>Mã khách (CustomerID) do 1 nhân viên phụ trách (TCustomer.Staff_ID = USERS.ID) — vd nhân viên chỉ xem yêu cầu của khách mình.</summary>
    Task<IReadOnlyList<long>> GetCustomerIdsOfManagerAsync(int managerId, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên Việt An còn hoạt động (STATUS = ACTIVE, chưa xóa) có tên đăng nhập này — để đăng nhập portal làm thay khách.</summary>
    Task<IReadOnlyList<VietAnUserLogin>> FindVietAnUsersAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên Việt An còn hoạt động theo USERS.ID; null = không còn / đã khóa.</summary>
    Task<VietAnUserLogin?> GetVietAnUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm khách theo mã / tên / SĐT / tên đăng nhập (<paramref name="query"/> trống = mọi khách), tối đa <paramref name="take"/> dòng.
    /// <paramref name="managerId"/> có giá trị = chỉ khách nhân viên đó phụ trách (TCustomer.Staff_ID).
    /// </summary>
    Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(string? query, int? managerId, int take, CancellationToken cancellationToken = default);
}

/// <summary>Nhân viên Việt An (dbo.USERS) phụ trách khách. <see cref="Id"/> = USERS.ID = TCustomer.Staff_ID.</summary>
public sealed record AccountManager(int Id, string UserName, string FullName, string? Email, string? Phone, int? BranchId, bool Active);

/// <summary>
/// Nhân viên Việt An đăng nhập portal. <see cref="ViewAllCustomers"/> = IS_ADMIN hoặc VIEW_CUSTOMER_ALL (làm thay mọi khách);
/// còn lại chỉ khách có TCustomer.Staff_ID = <see cref="Id"/>. <see cref="Password"/> = USER_PASSWORD (chữ thường như hệ thống cũ).
/// </summary>
/// <param name="Email">USERS.EMAIL1 — nhận mã OTP khi đăng nhập portal; null = chưa có email.</param>
public sealed record VietAnUserLogin(int Id, string UserName, string FullName, bool ViewAllCustomers, string Password, string? Email = null);

/// <summary>Thông tin khách đủ dùng cho module khác. <see cref="Id"/> = CustomerID, cũng là CustomerID trong dbo.MaVanDon.</summary>
public sealed record CustomerSummary(
    long Id,
    string Code,
    string CompanyName,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxCode,
    string? InvoiceName = null,
    string? LoginUserName = null)
{
    /// <summary>Tên công ty trên trang MyTracking: TCustomer.InvoiceCustomerName, trống thì dùng CustomerName.</summary>
    public string TrackingName => string.IsNullOrWhiteSpace(InvoiceName) ? CompanyName : InvoiceName.Trim();
}
