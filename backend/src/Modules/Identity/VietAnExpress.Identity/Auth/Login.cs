using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Auth;

// ---------- Đăng nhập ----------

/// <summary>
/// Đăng nhập bằng Login_UserName / Login_Password của dbo.TCustomer (đúng tài khoản khách đang dùng ở hệ thống cũ — admin),
/// không khớp thì thử tài khoản con của nhân viên (dbo.TCustomer_NguoiLienLac: TinhCach / Mo_Ta).
/// </summary>
internal sealed record LoginCommand(string UserName, string Password, bool Remember) : IRequest<Result<AuthSession>>;

internal sealed class LoginHandler(IdentityDbContext db, SessionService sessions, LoginAttemptGuard guard)
    : IRequestHandler<LoginCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var userName = cmd.UserName.Trim();
        if (guard.LockedFor(userName) is { } locked) return AuthErrors.LoginLocked(locked);
        var result = await TryLoginAsync(userName, cmd, ct);
        if (result.IsSuccess) guard.Succeeded(userName);
        else if (result.Error == AuthErrors.InvalidCredentials) guard.Failed(userName);
        return result;
    }

    private async Task<Result<AuthSession>> TryLoginAsync(string userName, LoginCommand cmd, CancellationToken ct)
    {
        var candidates = await db.Logins.AsNoTracking()
            .Where(l => l.UserName == userName && l.Password != null && l.Password != "")
            .ToListAsync(ct);

        var login = candidates.FirstOrDefault(l => JwtTokenService.FixedTimeEquals(l.Password!, cmd.Password));
        if (login is not null)
        {
            var session = await sessions.StartAsync(login, cmd.Remember, ct);
            return session is null ? AuthErrors.InvalidCredentials : session;
        }

        // Bảng người liên hệ của hệ thống cũ không khóa trùng tên đăng nhập → thử mọi dòng cùng tên.
        var staffCandidates = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.UserName == userName && s.Password != null && s.Password != "")
            .ToListAsync(ct);
        var staff = staffCandidates.FirstOrDefault(s => StaffPasswordHasher.Matches(s.Password, cmd.Password));
        if (staff is null) return AuthErrors.InvalidCredentials;
        if (!staff.IsActive) return AuthErrors.StaffDisabled;

        var staffSession = await sessions.StartAsync(staff, cmd.Remember, ct);
        return staffSession is null ? AuthErrors.InvalidCredentials : staffSession;
    }
}
