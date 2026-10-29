using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

internal sealed record CreateStaffCommand(
    string UserName, string Password, string FullName, string? Email, string? Phone, IReadOnlyList<string> Permissions,
    string? Phone2 = null, string? Email2 = null)
    : IRequest<Result<StaffAccountDto>>;

internal sealed class CreateStaffHandler(IdentityDbContext db, SessionService sessions, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<CreateStaffCommand, Result<StaffAccountDto>>
{
    /// <summary>Giới hạn số tài khoản con / khách — tránh tạo tràn lan.</summary>
    public const int MaxPerCustomer = 100;

    private DateTime Now => VietnamTime.Now(clock);

    /// <summary>Tên đăng nhập không được trùng tài khoản con khác hay tài khoản chính của bất kỳ khách nào.</summary>
    private async Task<bool> UserNameTakenAsync(string userName, CancellationToken ct) =>
        await db.StaffAccounts.AnyAsync(s => s.UserName == userName, ct)
        || await db.Logins.AnyAsync(l => l.UserName == userName, ct);

    public async Task<Result<StaffAccountDto>> Handle(CreateStaffCommand cmd, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var permissions = StaffAccounts.WithoutStaffAlways(cmd.Permissions);
        if (!StaffAccounts.PermissionsAllowed(permissions, sessions)) return StaffErrors.UnknownPermission;

        var userName = cmd.UserName.Trim();
        // Bảng cũ không có index duy nhất cho tên đăng nhập: kiểm tra + thêm trong cùng 1 khóa để 2 người tạo cùng lúc
        // (cùng tên, hoặc cùng lúc vượt giới hạn 100 tài khoản) phải chờ nhau thay vì cùng lọt qua.
        return await db.WithAppLockAsync<Result<StaffAccountDto>>("portal:staff-account", async () =>
        {
            if (await UserNameTakenAsync(userName, ct)) return StaffErrors.UserNameTaken;
            if (await db.StaffAccounts.CountAsync(s => s.CustomerId == customerId, ct) >= MaxPerCustomer)
                return StaffErrors.TooManyStaff;

            // Mật khẩu lưu như hệ thống cũ (cột Mo_Ta dùng chung) — giống dbo.TCustomer.Login_Password.
            var staff = new StaffAccount(customerId, userName, cmd.Password, cmd.FullName, cmd.Email, cmd.Phone, permissions, Now);
            staff.SetSecondContact(cmd.Phone2, cmd.Email2);
            db.StaffAccounts.Add(staff);
            await db.SaveChangesAsync(ct);
            return StaffAccounts.ToDto(staff, sessions);
        }, ct);
    }
}
