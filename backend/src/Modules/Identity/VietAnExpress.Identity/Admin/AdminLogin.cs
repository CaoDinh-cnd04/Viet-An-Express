using MediatR;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Admin;

internal sealed record AdminLoginCommand(string? UserName, string? Password) : IRequest<Result<AdminSessionDto>>;

internal sealed class AdminLoginHandler(ITokenService tokens, IOptions<JwtOptions> jwt, LoginAttemptGuard guard)
    : IRequestHandler<AdminLoginCommand, Result<AdminSessionDto>>
{
    public Task<Result<AdminSessionDto>> Handle(AdminLoginCommand c, CancellationToken ct)
    {
        // Khóa tạm riêng cho trang quản trị (tiền tố admin:) — không lẫn với tên đăng nhập của khách.
        var key = "admin:" + (c.UserName ?? "");
        if (guard.LockedFor(key) is { } locked) return Task.FromResult<Result<AdminSessionDto>>(AuthErrors.LoginLocked(locked));
        if (!VietAnAdminAccount.Verify(c.UserName, c.Password))
        {
            guard.Failed(key);
            return Task.FromResult<Result<AdminSessionDto>>(AuthErrors.InvalidCredentials);
        }
        guard.Succeeded(key);
        // CustomerID 0: không phải khách nào — chỉ có quyền quản trị, mọi API của khách đều bị chặn.
        var token = tokens.CreateAccessToken(0, null, VietAnAdminAccount.UserName, [SystemRoles.VietAnAdmin],
            [IdentityPermissions.AdminNotifications, IdentityPermissions.AdminSupportRequests],
            TimeSpan.FromMinutes(jwt.Value.AdminTokenMinutes));
        return Task.FromResult<Result<AdminSessionDto>>(new AdminSessionDto(token.Token, token.ExpiresAt, VietAnAdminAccount.UserName));
    }
}

/// <summary>Token quản trị: trang /admin giữ trong sessionStorage và gửi header Authorization (không đụng cookie phiên của khách).</summary>
internal sealed record AdminSessionDto(string Token, DateTimeOffset ExpiresAt, string UserName);
