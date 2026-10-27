using MediatR;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Application.Commands;

// ---------------- Quản trị Việt An: đăng nhập trang /admin ----------------

/// <summary>
/// Tài khoản quản trị cố định trong code (người dùng chọn): chỉ lưu mã băm PBKDF2 của mật khẩu, không lưu mật khẩu gốc.
/// Đổi mật khẩu: tạo mã băm mới (StaffPasswordHasher.Hash) rồi thay <see cref="PasswordHash"/>.
/// </summary>
internal static class VietAnAdminAccount
{
    public const string UserName = "vietan.admin";
    public const string PasswordHash = "pbkdf2$100000$sNhiFgiUmRCckGhZ9PWSTQ==$IHgm2D99iAeymFyKwqAQDJayQYKFGN5lPJTpNeCMA3U=";

    public static bool Verify(string? userName, string? password) =>
        string.Equals(userName?.Trim(), UserName, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(password)
        && StaffPasswordHasher.Verify(PasswordHash, password);
}

internal sealed record AdminLoginCommand(string? UserName, string? Password) : IRequest<Result<AdminSessionDto>>;

/// <summary>Token quản trị: trang /admin giữ trong sessionStorage và gửi header Authorization (không đụng cookie phiên của khách).</summary>
internal sealed record AdminSessionDto(string Token, DateTimeOffset ExpiresAt, string UserName);

internal sealed class AdminLoginHandler(ITokenService tokens, IOptions<JwtOptions> jwt, LoginAttemptGuard guard) : IRequestHandler<AdminLoginCommand, Result<AdminSessionDto>>
{
    public Task<Result<AdminSessionDto>> Handle(AdminLoginCommand c, CancellationToken ct)
    {
        // Khóa tạm riêng cho trang quản trị (tiền tố admin:) — không lẫn với tên đăng nhập của khách.
        var key = "admin:" + (c.UserName ?? "");
        if (guard.LockedFor(key) is { } locked) return Task.FromResult<Result<AdminSessionDto>>(IdentityErrors.LoginLocked(locked));
        if (!VietAnAdminAccount.Verify(c.UserName, c.Password))
        {
            guard.Failed(key);
            return Task.FromResult<Result<AdminSessionDto>>(IdentityErrors.InvalidCredentials);
        }
        guard.Succeeded(key);
        // CustomerID 0: không phải khách nào — chỉ có quyền quản trị, mọi API của khách đều bị chặn.
        var token = tokens.CreateAccessToken(0, null, VietAnAdminAccount.UserName, [SystemRoles.VietAnAdmin],
            [IdentityPermissions.AdminNotifications, IdentityPermissions.AdminTroubles],
            TimeSpan.FromMinutes(jwt.Value.AdminTokenMinutes));
        return Task.FromResult<Result<AdminSessionDto>>(new AdminSessionDto(token.Token, token.ExpiresAt, VietAnAdminAccount.UserName));
    }
}
