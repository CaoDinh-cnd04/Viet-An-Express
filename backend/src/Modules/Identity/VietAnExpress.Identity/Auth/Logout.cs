using MediatR;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Auth;

// ---------- Đăng xuất ----------

/// <summary>
/// Thu hồi token của phiên đang đăng xuất (không cần bảng DB — <see cref="RevokedTokenStore"/>): refresh token không đổi được phiên mới,
/// access token bị từ chối ngay dù còn hạn. Token sai / hết hạn thì bỏ qua (đăng xuất vẫn thành công).
/// </summary>
/// <param name="AccessTokenId">jti của access token gửi kèm request (đã được xác thực).</param>
internal sealed record LogoutCommand(string? RefreshToken, string? AccessTokenId, DateTimeOffset? AccessTokenExpiresAt) : IRequest<Result>;

internal sealed class LogoutHandler(ITokenService tokens, RevokedTokenStore revoked) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        if (cmd.AccessTokenExpiresAt is { } accessExpires) revoked.Revoke(cmd.AccessTokenId, accessExpires);
        if (cmd.RefreshToken is { Length: > 0 } rt && await tokens.ReadRefreshTokenAsync(rt) is { } claims)
            revoked.Revoke(claims.Id, claims.ExpiresAt);
        return Result.Success();
    }
}
