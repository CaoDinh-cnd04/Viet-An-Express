using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Auth;

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
        if (currentUser.CustomerId is not { } customerId) return AuthErrors.SessionExpired;
        var persistent = cmd.CurrentRefreshToken is { Length: > 0 } rt && (await tokens.ReadRefreshTokenAsync(rt))?.IsPersistent == true;

        // Tài khoản con không có quyền này — admin đặt lại ở trang Tài khoản nhân viên (POST /account/staff/{id}/reset-password).
        if (!currentUser.HasPermission(IdentityPermissions.ChangePassword)) return AuthErrors.StaffPasswordManagedByAdmin;

        var login = await db.Logins.FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);
        if (login is null || !login.HasPassword) return AuthErrors.SessionExpired;
        if (!JwtTokenService.FixedTimeEquals(login.Password!, cmd.CurrentPassword)) return AuthErrors.WrongCurrentPassword;

        login.ChangePassword(cmd.NewPassword);
        await db.SaveChangesAsync(ct);

        var session = await sessions.StartAsync(login, persistent, ct);
        return session is null ? AuthErrors.SessionExpired : session;
    }
}
