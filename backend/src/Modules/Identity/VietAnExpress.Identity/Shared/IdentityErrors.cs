using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Shared;

/// <summary>Lỗi dùng chung cho mọi chức năng của module (chưa đăng nhập, chỉ tài khoản chính). Lỗi riêng: AuthErrors, StaffErrors, MyTrackingErrors…</summary>
internal static class IdentityErrors
{
    public static readonly Error NotLoggedIn = Error.Unauthorized("NOT_LOGGED_IN", "Vui lòng đăng nhập");

    public static readonly Error AdminOnly =
        Error.Forbidden("ADMIN_ONLY", "Chỉ tài khoản chính (quản trị) của công ty được thực hiện thao tác này");
}
