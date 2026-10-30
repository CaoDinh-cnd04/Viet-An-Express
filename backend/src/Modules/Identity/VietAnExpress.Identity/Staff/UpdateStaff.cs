using MediatR;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

internal sealed record UpdateStaffCommand(
    long Id, string FullName, string? Email, string? Phone, bool Active,
    string? Phone2 = null, string? Email2 = null)
    : IRequest<Result<StaffAccountDto>>;

internal sealed class UpdateStaffHandler(IdentityDbContext db, ICurrentUser user)
    : IRequestHandler<UpdateStaffCommand, Result<StaffAccountDto>>
{
    public async Task<Result<StaffAccountDto>> Handle(UpdateStaffCommand cmd, CancellationToken ct)
    {
        var found = await db.FindStaffOfMainAccountAsync(user, cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var staff = found.Value;

        // Khóa tài khoản: phiên đang mở hết hiệu lực khi access token hết hạn (refresh bị từ chối).
        staff.Update(cmd.FullName, cmd.Email, cmd.Phone, cmd.Active);
        staff.SetSecondContact(cmd.Phone2, cmd.Email2);
        await db.SaveChangesAsync(ct);
        return StaffAccounts.ToDto(staff);
    }
}
