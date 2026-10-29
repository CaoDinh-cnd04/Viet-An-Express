using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;

namespace VietAnExpress.Identity.Admin;

/// <summary>
/// Tài khoản quản trị cố định trong code (người dùng chọn): chỉ lưu mã băm PBKDF2 của mật khẩu, không lưu mật khẩu gốc.
/// Đổi mật khẩu: tạo mã băm mới (StaffPasswordHasher.Hash) rồi thay <see cref="PasswordHash"/>.
/// </summary>
internal static class VietAnAdminAccount
{
    public const string UserName = "vietan.admin";
    public const string PasswordHash = "pbkdf2$100000$sNhiFgiUmRCckGhZ9PWSTQ==$IHgm2D99iAeymFyKwqAQDJayQYKFGN5lPJTpNeCMA3U=";

    public static bool Verify(string? userName, string? password) =>
        string.Equals(userName?.Trim(), UserName, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(password)
        && StaffPasswordHasher.Verify(PasswordHash, password);
}
