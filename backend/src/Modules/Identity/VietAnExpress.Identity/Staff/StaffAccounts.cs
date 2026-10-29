using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

/// <summary>
/// Phần dùng chung của các use case tài khoản con: tìm tài khoản, kiểm tra quyền được cấp, map DTO.
/// Tài khoản con chỉ tài khoản chính (admin) của khách quản lý: controller chặn bằng quyền account.staff,
/// handler kiểm tra thêm StaffId == null (<see cref="FindStaffOfMainAccountAsync"/>) để tài khoản con không bao giờ tự cấp quyền.
/// </summary>
internal static class StaffAccounts
{
    /// <summary>Tài khoản con của khách đang đăng nhập: chỉ tài khoản chính được thao tác (AdminOnly), không thấy thì StaffNotFound.</summary>
    public static async Task<Result<StaffAccount>> FindStaffOfMainAccountAsync(this IdentityDbContext db, ICurrentUser user, long id, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var staff = await db.StaffAccounts.FirstOrDefaultAsync(s => s.Id == id && s.CustomerId == customerId, ct);
        return staff is null ? StaffErrors.StaffNotFound : staff;
    }

    /// <summary>Mọi quyền gửi lên đều thuộc danh sách tài khoản chính được cấp cho tài khoản con.</summary>
    public static bool PermissionsAllowed(IEnumerable<string> permissions, SessionService sessions) => permissions.All(sessions.IsAssignable);

    /// <summary>Quyền luôn có (vd hỗ trợ & sự cố) không cần lưu — bỏ qua nếu client gửi kèm thay vì báo quyền không hợp lệ.</summary>
    public static List<string> WithoutStaffAlways(IEnumerable<string> permissions) =>
        permissions.Where(p => !IdentityPermissions.StaffAlways.Contains(p.Trim())).ToList();

    public static StaffAccountDto ToDto(StaffAccount s, SessionService sessions) => new(
        s.Id, s.UserName, s.FullName, s.Email, s.Phone, sessions.GrantedPermissions(s), s.IsActive, s.CreateDate, s.Phone2, s.Email2);
}
