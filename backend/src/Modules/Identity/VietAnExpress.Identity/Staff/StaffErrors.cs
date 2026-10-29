using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

/// <summary>Lỗi quản lý tài khoản con.</summary>
internal static class StaffErrors
{
    public static readonly Error TooManyStaff =
        Error.BusinessRule("TOO_MANY_STAFF", "Đã đạt số tài khoản nhân viên tối đa (100)");

    public static readonly Error StaffNotFound =
        Error.NotFound("STAFF_NOT_FOUND", "Không tìm thấy tài khoản nhân viên");

    public static readonly Error UserNameTaken =
        Error.Conflict("USERNAME_TAKEN", "Tên đăng nhập đã được sử dụng, vui lòng chọn tên khác");

    public static readonly Error UnknownPermission =
        Error.Validation("UNKNOWN_PERMISSION", "Có quyền không hợp lệ hoặc không được cấp cho nhân viên");
}
