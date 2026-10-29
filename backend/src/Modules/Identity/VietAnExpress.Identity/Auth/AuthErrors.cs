using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Auth;

/// <summary>Lỗi đăng nhập / phiên / đổi mật khẩu.</summary>
internal static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("INVALID_CREDENTIALS", "Sai tên đăng nhập hoặc mật khẩu");

    /// <summary>Tên đăng nhập bị khóa tạm do nhập sai mật khẩu nhiều lần (<see cref="LoginAttemptGuard"/>).</summary>
    public static Error LoginLocked(TimeSpan remaining) =>
        Error.Unauthorized("LOGIN_LOCKED", $"Nhập sai mật khẩu nhiều lần — tài khoản tạm khóa, vui lòng thử lại sau {Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} phút");

    public static readonly Error SessionExpired =
        Error.Unauthorized("SESSION_EXPIRED", "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");

    public static readonly Error WrongCurrentPassword =
        Error.Validation("WRONG_CURRENT_PASSWORD", "Mật khẩu hiện tại không đúng");

    public static readonly Error StaffDisabled =
        Error.Unauthorized("ACCOUNT_DISABLED", "Tài khoản đã bị khóa, vui lòng liên hệ quản trị viên của công ty");

    public static readonly Error StaffPasswordManagedByAdmin =
        Error.Forbidden("STAFF_PASSWORD_ADMIN_ONLY", "Tài khoản nhân viên không tự đổi mật khẩu được — vui lòng liên hệ quản trị viên tài khoản công ty để đặt lại");
}
