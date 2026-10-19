namespace VietAnExpress.Identity.Domain;

/// <summary>
/// Tài khoản con của khách cho nhân viên — 1 dòng người liên hệ của khách (<c>dbo.TCustomer_NguoiLienLac</c>, bảng hệ thống cũ)
/// có tên đăng nhập (cột TinhCach). Tài khoản chính ở dbo.TCustomer là admin: tạo / khóa / phân quyền cho tài khoản con.
/// Nhân viên thấy mọi dữ liệu của khách cha (lọc theo <see cref="CustomerId"/>), chỉ dùng được các quyền admin đã chọn.
/// </summary>
internal sealed class StaffAccount
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 50;
    /// <summary>Cột Mo_Ta nvarchar(150).</summary>
    public const int PasswordMaxLength = 150;
    public const int FullNameMaxLength = 150;
    public const int EmailMaxLength = 150;
    public const int PhoneMaxLength = 30;
    public const int PermissionsMaxLength = 1000;

    /// <summary>Tên đăng nhập: chữ, số, dấu chấm, gạch dưới, gạch ngang, @ — không khoảng trắng.</summary>
    public const string UserNamePattern = @"^[A-Za-z0-9._@-]+$";

    private StaffAccount() { } // EF Core

    public StaffAccount(long customerId, string userName, string password, string fullName, string? email, string? phone,
        IEnumerable<string> permissions, DateTime now)
    {
        CustomerId = customerId;
        UserName = userName;
        Password = password;
        CreateDate = now;
        Update(fullName, email, phone, permissions, isActive: true);
    }

    public long Id { get; private set; }
    /// <summary>Khách cha — dbo.TCustomer.CustomerID.</summary>
    public long CustomerId { get; private set; }
    // 4 cột chữ dưới đây ở bảng cũ cho phép NULL (người liên hệ nhập từ phần mềm cũ thường để trống Ten / Mo_Ta / Quyen):
    // EF đọc / ghi qua field cho phép null, code luôn thấy chuỗi rỗng thay vì lỗi khi đọc dòng NULL.
    private string? _userName;
    private string? _password;
    private string? _fullName;
    private string? _permissions;

    /// <summary>Cột TinhCach — trống = người liên hệ không có tài khoản portal.</summary>
    public string UserName { get => _userName ?? ""; private set => _userName = value; }
    /// <summary>Cột Mo_Ta — mật khẩu dạng như hệ thống cũ (tài khoản tạo trước khi chuyển bảng có thể còn mã băm PBKDF2).</summary>
    public string Password { get => _password ?? ""; private set => _password = value; }
    /// <summary>Cột Ten.</summary>
    public string FullName { get => _fullName ?? ""; private set => _fullName = value; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    /// <summary>Cột Di_Dong2 — số di động thứ 2 (không bắt buộc).</summary>
    public string? Phone2 { get; private set; }
    /// <summary>Cột Email2 — email thứ 2 (không bắt buộc).</summary>
    public string? Email2 { get; private set; }
    /// <summary>Mã quyền admin cấp, cách nhau dấu phẩy (vd "shipments.view,shipments.create") — cột Quyen portal thêm vào bảng.</summary>
    public string Permissions { get => _permissions ?? ""; private set => _permissions = value; }
    public bool IsActive { get; private set; }
    public DateTime CreateDate { get; private set; }

    public IReadOnlyList<string> PermissionList =>
        Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Update(string fullName, string? email, string? phone, IEnumerable<string> permissions, bool isActive)
    {
        FullName = fullName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Permissions = string.Join(',', permissions.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        IsActive = isActive;
    }

    /// <summary>SĐT / email thứ 2 của người liên hệ — cột Di_Dong2, Email2 của bảng cũ.</summary>
    public void SetSecondContact(string? phone2, string? email2)
    {
        Phone2 = string.IsNullOrWhiteSpace(phone2) ? null : phone2.Trim();
        Email2 = string.IsNullOrWhiteSpace(email2) ? null : email2.Trim();
    }

    public void SetPassword(string password) => Password = password;

    /// <summary>Xóa tài khoản: gỡ tên đăng nhập, mật khẩu, quyền — giữ dòng người liên hệ vì hệ thống cũ còn dùng.</summary>
    public void RemoveLogin()
    {
        UserName = "";
        Password = "";
        Permissions = "";
        IsActive = false;
    }
}
