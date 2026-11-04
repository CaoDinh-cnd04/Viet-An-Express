using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Auth;

// ---------- Làm mới phiên ----------

/// <summary>
/// Đổi refresh token lấy cặp token mới. Refresh token không lưu DB: hợp lệ khi đúng chữ ký, còn hạn
/// và mật khẩu (dbo.TCustomer, dbo.TCustomer_NguoiLienLac với tài khoản con, dbo.USERS với nhân viên Việt An) chưa đổi kể từ lúc cấp;
/// tài khoản con / nhân viên Việt An còn hoạt động.
/// </summary>
internal sealed record RefreshSessionCommand(string RefreshToken) : IRequest<Result<AuthSession>>;

internal sealed class RefreshSessionHandler(
    IdentityDbContext db, ICustomersApi customers, ITokenService tokens, SessionService sessions, RevokedTokenStore revoked, TimeProvider clock,
    ILogger<RefreshSessionHandler> logger)
    : IRequestHandler<RefreshSessionCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(RefreshSessionCommand cmd, CancellationToken ct)
    {
        var claims = await tokens.ReadRefreshTokenAsync(cmd.RefreshToken);
        if (claims is null) return Rejected("refresh token hết hạn, quá giới hạn phiên (Jwt:MaxSessionDays) hoặc sai chữ ký (Jwt:Secret khác?)", null, null);
        if (revoked.IsRevoked(claims.Id)) return Rejected("refresh token đã thu hồi khi đăng xuất", claims.CustomerId, claims.StaffId);
        // Token cấp trước khi có giới hạn phiên: tính mốc từ lần gia hạn này (không đăng xuất đột ngột).
        var authTime = claims.AuthTime ?? clock.GetUtcNow();

        if (claims.VietAnUserId is { } agentId)
        {
            var context = await VietAnScope.LoadAsync(customers, agentId, claims.CustomerId, ct);
            if (context is null) return Rejected("nhân viên Việt An đã khóa / xóa", claims.CustomerId, null);
            if (!JwtTokenService.FixedTimeEquals(sessions.VietAnStamp(context.Agent), claims.PasswordStamp))
                return Rejected("mật khẩu nhân viên Việt An đã đổi (dbo.USERS.USER_PASSWORD) hoặc đã đăng nhập ở thiết bị khác", claims.CustomerId, null);
            return sessions.Start(context.Agent, context.Customer, claims.IsPersistent, authTime);
        }

        if (claims.StaffId is { } staffId)
        {
            var staff = await db.StaffAccounts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId && s.CustomerId == claims.CustomerId, ct);
            if (staff is null) return Rejected("tài khoản con không còn", claims.CustomerId, staffId);
            if (!staff.IsActive) return Rejected("tài khoản con đã bị khóa", claims.CustomerId, staffId);
            if (!JwtTokenService.FixedTimeEquals(tokens.PasswordStamp(staff.CustomerId, staff.Password, staff.Id), claims.PasswordStamp))
                return Rejected("mật khẩu tài khoản con đã đổi", claims.CustomerId, staffId);
            var staffSession = await sessions.StartAsync(staff, claims.IsPersistent, ct, authTime);
            return staffSession is null ? Rejected("hồ sơ khách cha không còn", claims.CustomerId, staffId) : staffSession;
        }

        var login = await db.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.CustomerId == claims.CustomerId, ct);
        if (login is null || !login.HasPassword) return Rejected("tài khoản khách không còn / chưa có mật khẩu", claims.CustomerId, null);
        if (!JwtTokenService.FixedTimeEquals(tokens.PasswordStamp(login.CustomerId, login.Password!), claims.PasswordStamp))
            return Rejected("mật khẩu tài khoản chính đã đổi (dbo.TCustomer.Login_Password)", claims.CustomerId, null);

        var session = await sessions.StartAsync(login, claims.IsPersistent, ct, authTime);
        return session is null ? Rejected("hồ sơ khách không còn", claims.CustomerId, null) : session;
    }

    /// <summary>Phiên bị từ chối → khách phải đăng nhập lại; ghi lý do để tra cứu khi khách báo "tự bị đăng xuất".</summary>
    private Error Rejected(string reason, long? customerId, long? staffId)
    {
        logger.LogWarning("Không làm mới được phiên (khách {CustomerId}, tài khoản con {StaffId}): {Reason}", customerId, staffId, reason);
        return AuthErrors.SessionExpired;
    }
}
