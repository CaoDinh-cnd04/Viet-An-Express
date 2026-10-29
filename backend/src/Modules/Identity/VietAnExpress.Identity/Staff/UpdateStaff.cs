using MediatR;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

internal sealed record UpdateStaffCommand(
    long Id, string FullName, string? Email, string? Phone, IReadOnlyList<string> Permissions, bool Active,
    string? Phone2 = null, string? Email2 = null)
    : IRequest<Result<StaffAccountDto>>;

internal sealed class UpdateStaffHandler(IdentityDbContext db, SessionService sessions, ICurrentUser user)
    : IRequestHandler<UpdateStaffCommand, Result<StaffAccountDto>>
{
    public async Task<Result<StaffAccountDto>> Handle(UpdateStaffCommand cmd, CancellationToken ct)
    {
        var found = await db.FindStaffOfMainAccountAsync(user, cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var permissions = StaffAccounts.WithoutStaffAlways(cmd.Permissions);
        if (!StaffAccounts.PermissionsAllowed(permissions, sessions)) return StaffErrors.UnknownPermission;
        var staff = found.Value;

        // Khóa tài khoản: phiên đang mở hết hiệu lực khi access token hết hạn (refresh bị từ chối).
        staff.Update(cmd.FullName, cmd.Email, cmd.Phone, permissions, cmd.Active);
        staff.SetSecondContact(cmd.Phone2, cmd.Email2);
        await db.SaveChangesAsync(ct);
        return StaffAccounts.ToDto(staff, sessions);
    }
}
