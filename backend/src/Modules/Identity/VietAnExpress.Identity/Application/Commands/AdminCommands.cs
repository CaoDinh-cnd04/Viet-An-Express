using MediatR;
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

internal sealed class AdminLoginHandler(ITokenService tokens) : IRequestHandler<AdminLoginCommand, Result<AdminSessionDto>>
{
    public Task<Result<AdminSessionDto>> Handle(AdminLoginCommand c, CancellationToken ct)
    {
        if (!VietAnAdminAccount.Verify(c.UserName, c.Password)) return Task.FromResult<Result<AdminSessionDto>>(IdentityErrors.InvalidCredentials);
        // CustomerID 0: không phải khách nào — chỉ có quyền quản trị, mọi API của khách đều bị chặn.
        var token = tokens.CreateAccessToken(0, null, VietAnAdminAccount.UserName, [SystemRoles.VietAnAdmin],
            [IdentityPermissions.AdminNotifications, IdentityPermissions.AdminTroubles, IdentityPermissions.AdminAnalytics]);
        return Task.FromResult<Result<AdminSessionDto>>(new AdminSessionDto(token.Token, token.ExpiresAt, VietAnAdminAccount.UserName));
    }
}
