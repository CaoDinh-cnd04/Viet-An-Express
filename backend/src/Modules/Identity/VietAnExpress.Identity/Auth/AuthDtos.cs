
namespace VietAnExpress.Identity.Auth;

/// <summary>
/// Khách đang đăng nhập — GET /me. Khớp <c>SessionUser</c> của frontend (web/src/features/auth/types.ts):
/// customerCode, companyName, contactName, email, avatarUrl, defaultBranch; kèm quyền để UI ẩn/hiện chức năng.
/// Tài khoản con (accountType = "staff", isAdmin = false): fullName / userName của nhân viên, quyền do admin chọn, hồ sơ công ty của khách cha.
/// Hồ sơ lấy từ dbo.TCustomer — companyName, contactName, phone, address, taxCode, email dùng để điền sẵn người gửi khi tạo đơn.
/// </summary>
internal sealed record SessionUserDto(
    long CustomerId,
    string UserName,
    string FullName,
    string AccountType,
    string CustomerCode,
    string CompanyName,
    string? ContactName,
    string? Email,
    string? AvatarUrl,
    string? DefaultBranch,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? Phone = null,
    string? Address = null,
    string? TaxCode = null,
    bool IsAdmin = false);

/// <summary>Loại tài khoản: "customer" = tài khoản chính của khách (admin), "staff" = tài khoản con của nhân viên.</summary>
internal static class AccountTypes
{
    public const string Customer = "customer";
    public const string Staff = "staff";
}

/// <summary>Kết quả đăng nhập / làm mới phiên — controller quyết định trả qua cookie hay body.</summary>
internal sealed record AuthSession(
    SessionUserDto User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsPersistent);
