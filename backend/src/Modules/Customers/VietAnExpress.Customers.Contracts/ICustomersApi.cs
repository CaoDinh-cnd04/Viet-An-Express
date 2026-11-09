
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

    /// <summary>
    /// Mã khách (CustomerID) trong phạm vi 1 nhân viên: sale phụ trách (TCustomer.Staff_ID = USERS.ID) hoặc nhân viên được gán ở
    /// TCustomer.SubTer (CS — như CMS) — vd nhân viên chỉ xem yêu cầu / đơn của khách mình.
    /// </summary>
    Task<IReadOnlyList<long>> GetCustomerIdsOfManagerAsync(int managerId, CancellationToken cancellationToken = default);

    /// <summary>Khách có thuộc phạm vi nhân viên không (cùng quy tắc <see cref="GetCustomerIdsOfManagerAsync"/>).</summary>
    Task<bool> IsCustomerOfStaffAsync(long customerId, int staffId, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên Việt An còn hoạt động (STATUS = ACTIVE, chưa xóa) có tên đăng nhập này — để đăng nhập portal làm thay khách.</summary>
    Task<IReadOnlyList<VietAnUserLogin>> FindVietAnUsersAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên Việt An còn hoạt động theo USERS.ID; null = không còn / đã khóa.</summary>
    Task<VietAnUserLogin?> GetVietAnUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Tên đăng nhập nhân viên Việt An (USERS.USER_NAME) theo USERS.ID — vd cột "Người tạo" (MaVanDon.CreateUser). Bỏ id không có.</summary>
    Task<IReadOnlyDictionary<int, string>> GetVietAnUserNamesAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default);

    /// <summary>Tên đăng nhập tài khoản con (TCustomer_NguoiLienLac.TinhCach) theo ID — vd MaVanDon.Customer_Staff_ID. Bỏ id không có tên đăng nhập.</summary>
    Task<IReadOnlyDictionary<long, string>> GetContactLoginNamesAsync(IReadOnlyCollection<long> contactIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm khách theo mã / tên / SĐT / tên đăng nhập (<paramref name="query"/> trống = mọi khách), tối đa <paramref name="take"/> dòng.
    /// <paramref name="managerId"/> có giá trị = chỉ khách nhân viên đó phụ trách (TCustomer.Staff_ID). Khách mới đứng đầu, rồi theo mã.
    /// </summary>
    Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(string? query, int? managerId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhân viên Việt An tạo khách mới (khách lần đầu gửi): dòng dbo.TCustomer tạm, CustomerSource = "Portal - Khách mới",
    /// Staff_ID = <paramref name="staffId"/>. Dòng khách mới chưa điền tên của chính nhân viên đó được dùng lại. Trả CustomerID.
    /// </summary>
    Task<long> CreateNewCustomerAsync(int staffId, CancellationToken cancellationToken = default);

    /// <summary>Ghi thông tin người gửi vào hồ sơ khách mới (ô trống giữ giá trị cũ); khách không còn là khách mới → bỏ qua, trả false.</summary>
    Task<bool> FillNewCustomerAsync(long customerId, NewCustomerInfo info, int modifiedBy, CancellationToken cancellationToken = default);

    /// <summary>Gán nhân viên phụ trách (TCustomer.Staff_ID) cho khách mới; false = không phải khách mới / nhân viên không thuộc sales, CS.</summary>
    Task<bool> AssignNewCustomerManagerAsync(long customerId, int managerId, int modifiedBy, CancellationToken cancellationToken = default);

    /// <summary>Nhân viên còn hoạt động có vai trò Sale / CS (dbo.ACL_ROLES) — danh sách chọn người phụ trách khách mới.</summary>
    Task<IReadOnlyList<AccountManager>> GetSupportStaffAsync(CancellationToken cancellationToken = default);
}

/// <summary>Thông tin người gửi ghi vào hồ sơ khách mới.</summary>
public sealed record NewCustomerInfo(string? CompanyName, string? ContactName, string? Phone, string? Address, string? TaxCode, string? Email);

/// <summary>Nhân viên Việt An (dbo.USERS) phụ trách khách. <see cref="Id"/> = USERS.ID = TCustomer.Staff_ID.</summary>
public sealed record AccountManager(int Id, string UserName, string FullName, string? Email, string? Phone, int? BranchId, bool Active);

/// <summary>
/// Nhân viên Việt An đăng nhập portal. <see cref="ViewAllCustomers"/> = vai trò Administrator hoặc ALL (ACL_ROLES — không dùng IS_ADMIN / VIEW_CUSTOMER_ALL)
/// (dbo.ACL_ROLES_USERS, 05/11/2026) — làm thay / xem mọi khách;
/// còn lại chỉ khách có TCustomer.Staff_ID = <see cref="Id"/>. <see cref="Password"/> = USER_PASSWORD (chữ thường như hệ thống cũ).
/// </summary>
/// <param name="Email">USERS.EMAIL1 — nhận mã OTP khi đăng nhập portal; null = chưa có email.</param>
/// <param name="CanAssignManager">vai trò Administrator / CS — được gán nhân viên phụ trách cho khách mới.</param>
public sealed record VietAnUserLogin(int Id, string UserName, string FullName, bool ViewAllCustomers, string Password, string? Email = null,
    bool CanAssignManager = false);

/// <summary>
/// Thông tin khách đủ dùng cho module khác. <see cref="IsNew"/> = khách mới nhân viên Việt An tạo trên portal, CS chưa hoàn thiện
/// (TCustomer.CustomerSource = "Portal - Khách mới") — người gửi sửa được, lưu đơn thì ghi người gửi vào hồ sơ.
/// <see cref="ManagerPending"/> = khách mới chưa được quản trị / CS gán người phụ trách (Staff_ID còn là người tạo).
/// <see cref="Id"/> = CustomerID, cũng là CustomerID trong dbo.MaVanDon.</summary>
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
    string? LoginUserName = null,
    bool IsNew = false,
    bool ManagerPending = false)
{
    /// <summary>Tên công ty trên trang MyTracking: TCustomer.InvoiceCustomerName, trống thì dùng CustomerName.</summary>
    public string TrackingName => string.IsNullOrWhiteSpace(InvoiceName) ? CompanyName : InvoiceName.Trim();
}
