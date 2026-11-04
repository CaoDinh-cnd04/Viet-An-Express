
namespace VietAnExpress.Identity.Staff;

/// <summary>
/// Tài khoản con của khách cho nhân viên — 1 dòng người liên hệ của khách (<c>dbo.TCustomer_NguoiLienLac</c>, bảng hệ thống cũ)
/// có tên đăng nhập (cột TinhCach). Tài khoản chính ở dbo.TCustomer là admin: tạo / khóa / đặt lại mật khẩu tài khoản con.
/// Nhân viên dùng mọi chức năng của khách cha trừ quản lý tài khoản con, MyTracking, tự đổi mật khẩu; chỉ thấy đơn mình tạo.
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

    /// <summary>Tên đăng nhập: chữ, số, dấu chấm, gạch dưới, gạch ngang, @ — không khoảng trắng.</summary>
    public const string UserNamePattern = @"^[A-Za-z0-9._@-]+$";

    private StaffAccount() { } // EF Core

    public StaffAccount(long customerId, string userName, string password, string fullName, string? email, string? phone, DateTime now)
    {
        CustomerId = customerId;
        UserName = userName;
        Password = password;
        CreateDate = now;
        Update(fullName, email, phone, isActive: true);
    }

    public long Id { get; private set; }
    /// <summary>Khách cha — dbo.TCustomer.CustomerID.</summary>
    public long CustomerId { get; private set; }
    // 3 cột chữ dưới đây ở bảng cũ cho phép NULL (người liên hệ nhập từ phần mềm cũ thường để trống Ten / Mo_Ta):
    // EF đọc / ghi qua field cho phép null, code luôn thấy chuỗi rỗng thay vì lỗi khi đọc dòng NULL.
    private string? _userName;
    private string? _password;
    private string? _fullName;

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
    public bool IsActive { get; private set; }
    public DateTime CreateDate { get; private set; }

    public void Update(string fullName, string? email, string? phone, bool isActive)
    {
        FullName = fullName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        IsActive = isActive;
    }

    /// <summary>SĐT / email thứ 2 của người liên hệ — cột Di_Dong2, Email2 của bảng cũ.</summary>
    public void SetSecondContact(string? phone2, string? email2)
    {
        Phone2 = string.IsNullOrWhiteSpace(phone2) ? null : phone2.Trim();
        Email2 = string.IsNullOrWhiteSpace(email2) ? null : email2.Trim();
    }

    public void SetPassword(string password) => Password = password;

    /// <summary>Email nhận OTP tài khoản con vừa xác nhận — ghi đè cột Email.</summary>
    public void SetEmail(string email) => Email = email.Trim();

    /// <summary>Xóa tài khoản: gỡ tên đăng nhập, mật khẩu — giữ dòng người liên hệ vì hệ thống cũ còn dùng.</summary>
    public void RemoveLogin()
    {
        UserName = "";
        Password = "";
        IsActive = false;
    }
}
