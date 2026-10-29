using MediatR;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

internal sealed record ResetStaffPasswordCommand(long Id, string NewPassword) : IRequest<Result>;

internal sealed class ResetStaffPasswordHandler(IdentityDbContext db, ICurrentUser user)
    : IRequestHandler<ResetStaffPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetStaffPasswordCommand cmd, CancellationToken ct)
    {
        var found = await db.FindStaffOfMainAccountAsync(user, cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var staff = found.Value;
        // Đổi mật khẩu → dấu mật khẩu trong refresh token đổi → nhân viên phải đăng nhập lại.
        staff.SetPassword(cmd.NewPassword);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
