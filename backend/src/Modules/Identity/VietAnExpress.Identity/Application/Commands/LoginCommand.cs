using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Application.Dtos;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

// ---------- Đăng nhập ----------

/// <summary>
/// Đăng nhập bằng Login_UserName / Login_Password của dbo.TCustomer (đúng tài khoản khách đang dùng ở hệ thống cũ — admin),
/// không khớp thì thử tài khoản con của nhân viên (dbo.TCustomer_NguoiLienLac: TinhCach / Mo_Ta).
/// </summary>
internal sealed record LoginCommand(string UserName, string Password, bool Remember) : IRequest<Result<AuthSession>>;

internal sealed class LoginHandler(IdentityDbContext db, SessionService sessions) : IRequestHandler<LoginCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var userName = cmd.UserName.Trim();
        var candidates = await db.Logins.AsNoTracking()
            .Where(l => l.UserName == userName && l.Password != null && l.Password != "")
            .ToListAsync(ct);

        var login = candidates.FirstOrDefault(l => JwtTokenService.FixedTimeEquals(l.Password!, cmd.Password));
        if (login is not null)
        {
            var session = await sessions.StartAsync(login, cmd.Remember, ct);
            return session is null ? IdentityErrors.InvalidCredentials : session;
        }

        // Bảng người liên hệ của hệ thống cũ không khóa trùng tên đăng nhập → thử mọi dòng cùng tên.
        var staffCandidates = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.UserName == userName && s.Password != null && s.Password != "")
            .ToListAsync(ct);
        var staff = staffCandidates.FirstOrDefault(s => StaffPasswordHasher.Matches(s.Password, cmd.Password));
        if (staff is null) return IdentityErrors.InvalidCredentials;
        if (!staff.IsActive) return IdentityErrors.StaffDisabled;

        var staffSession = await sessions.StartAsync(staff, cmd.Remember, ct);
        return staffSession is null ? IdentityErrors.InvalidCredentials : staffSession;
    }
}

// ---------- Làm mới phiên ----------

/// <summary>
/// Đổi refresh token lấy cặp token mới. Refresh token không lưu DB: hợp lệ khi đúng chữ ký, còn hạn
/// và mật khẩu (dbo.TCustomer, hoặc dbo.TCustomer_NguoiLienLac với tài khoản con) chưa đổi kể từ lúc cấp; tài khoản con còn hoạt động.
/// </summary>
internal sealed record RefreshSessionCommand(string RefreshToken) : IRequest<Result<AuthSession>>;

internal sealed class RefreshSessionHandler(
    IdentityDbContext db, ITokenService tokens, SessionService sessions, ILogger<RefreshSessionHandler> logger)
    : IRequestHandler<RefreshSessionCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(RefreshSessionCommand cmd, CancellationToken ct)
    {
        var claims = await tokens.ReadRefreshTokenAsync(cmd.RefreshToken);
        if (claims is null) return Rejected("refresh token hết hạn hoặc sai chữ ký (Jwt:Secret khác?)", null, null);

        if (claims.StaffId is { } staffId)
        {
            var staff = await db.StaffAccounts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId && s.CustomerId == claims.CustomerId, ct);
            if (staff is null) return Rejected("tài khoản con không còn", claims.CustomerId, staffId);
            if (!staff.IsActive) return Rejected("tài khoản con đã bị khóa", claims.CustomerId, staffId);
            if (!JwtTokenService.FixedTimeEquals(tokens.PasswordStamp(staff.CustomerId, staff.Password, staff.Id), claims.PasswordStamp))
                return Rejected("mật khẩu tài khoản con đã đổi", claims.CustomerId, staffId);
            var staffSession = await sessions.StartAsync(staff, claims.IsPersistent, ct);
            return staffSession is null ? Rejected("hồ sơ khách cha không còn", claims.CustomerId, staffId) : staffSession;
        }

        var login = await db.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.CustomerId == claims.CustomerId, ct);
        if (login is null || !login.HasPassword) return Rejected("tài khoản khách không còn / chưa có mật khẩu", claims.CustomerId, null);
        if (!JwtTokenService.FixedTimeEquals(tokens.PasswordStamp(login.CustomerId, login.Password!), claims.PasswordStamp))
            return Rejected("mật khẩu tài khoản chính đã đổi (dbo.TCustomer.Login_Password)", claims.CustomerId, null);

        var session = await sessions.StartAsync(login, claims.IsPersistent, ct);
        return session is null ? Rejected("hồ sơ khách không còn", claims.CustomerId, null) : session;
    }

    /// <summary>Phiên bị từ chối → khách phải đăng nhập lại; ghi lý do để tra cứu khi khách báo "tự bị đăng xuất".</summary>
    private Error Rejected(string reason, long? customerId, long? staffId)
    {
        logger.LogWarning("Không làm mới được phiên (khách {CustomerId}, tài khoản con {StaffId}): {Reason}", customerId, staffId, reason);
        return IdentityErrors.SessionExpired;
    }
}

// ---------- Đổi mật khẩu ----------

/// <summary>
/// Ghi mật khẩu mới vào dbo.TCustomer.Login_Password (dạng chữ thường như hệ thống cũ — 2 hệ thống dùng chung);
/// tài khoản con không được tự đổi (admin đặt lại).
/// Mọi phiên cũ hết hiệu lực; thiết bị đang dùng nhận phiên mới ngay, giữ kiểu phiên (ghi nhớ hay không) theo refresh token hiện tại.
/// </summary>
internal sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, string? CurrentRefreshToken) : IRequest<Result<AuthSession>>;

internal sealed class ChangePasswordHandler(IdentityDbContext db, ITokenService tokens, SessionService sessions, ICurrentUser currentUser)
    : IRequestHandler<ChangePasswordCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        if (currentUser.CustomerId is not { } customerId) return IdentityErrors.SessionExpired;
        var persistent = cmd.CurrentRefreshToken is { Length: > 0 } rt && (await tokens.ReadRefreshTokenAsync(rt))?.IsPersistent == true;

        // Tài khoản con không có quyền này — admin đặt lại ở trang Tài khoản nhân viên (POST /account/staff/{id}/reset-password).
        if (!currentUser.HasPermission(IdentityPermissions.ChangePassword)) return IdentityErrors.StaffPasswordManagedByAdmin;

        var login = await db.Logins.FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);
        if (login is null || !login.HasPassword) return IdentityErrors.SessionExpired;
        if (!JwtTokenService.FixedTimeEquals(login.Password!, cmd.CurrentPassword)) return IdentityErrors.WrongCurrentPassword;

        login.ChangePassword(cmd.NewPassword);
        await db.SaveChangesAsync(ct);

        var session = await sessions.StartAsync(login, persistent, ct);
        return session is null ? IdentityErrors.SessionExpired : session;
    }
}

// ---------- Khách đang đăng nhập (GET /me) ----------

internal sealed record GetSessionQuery : IRequest<Result<SessionUserDto>>;

internal sealed class GetSessionHandler(IdentityDbContext db, SessionService sessions, ICurrentUser currentUser)
    : IRequestHandler<GetSessionQuery, Result<SessionUserDto>>
{
    public async Task<Result<SessionUserDto>> Handle(GetSessionQuery q, CancellationToken ct)
    {
        if (currentUser.CustomerId is not { } customerId) return IdentityErrors.SessionExpired;
        SessionUserDto? user;
        if (currentUser.StaffId is { } staffId)
        {
            var staff = await db.StaffAccounts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId && s.CustomerId == customerId, ct);
            user = staff is null ? null : await sessions.BuildUserAsync(staff, ct);
        }
        else
        {
            var login = await db.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);
            user = login is { HasPassword: true } ? await sessions.BuildUserAsync(login, ct) : null;
        }
        return user is null ? IdentityErrors.SessionExpired : user;
    }
}
