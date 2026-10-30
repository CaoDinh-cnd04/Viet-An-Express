using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

/// <summary>
/// Phần dùng chung của các use case tài khoản con: tìm tài khoản, map DTO.
/// Tài khoản con chỉ tài khoản chính (admin) của khách quản lý: controller chặn bằng quyền account.staff,
/// handler kiểm tra thêm StaffId == null (<see cref="FindStaffOfMainAccountAsync"/>).
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

    public static StaffAccountDto ToDto(StaffAccount s) => new(
        s.Id, s.UserName, s.FullName, s.Email, s.Phone, s.IsActive, s.CreateDate, s.Phone2, s.Email2);
}
