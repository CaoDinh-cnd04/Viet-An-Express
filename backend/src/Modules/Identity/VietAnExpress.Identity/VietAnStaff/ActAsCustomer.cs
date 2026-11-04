using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Nhân viên Việt An chọn / đổi khách để làm thay → cấp lại phiên gắn khách đó (giữ "ghi nhớ" và mốc nhập mật khẩu của phiên đang dùng).
/// Mật khẩu của khách không cần và không đổi.
/// </summary>
internal sealed record ActAsCustomerCommand(long CustomerId, string? RefreshToken) : IRequest<Result<AuthSession>>;

internal sealed class ActAsCustomerHandler(ICustomersApi customers, SessionService sessions, ITokenService tokens, ICurrentUser user)
    : IRequestHandler<ActAsCustomerCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(ActAsCustomerCommand cmd, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId) return VietAnStaffErrors.VietAnOnly;
        var current = string.IsNullOrEmpty(cmd.RefreshToken) ? null : await tokens.ReadRefreshTokenAsync(cmd.RefreshToken);
        if (current?.VietAnUserId != agentId) return AuthErrors.SessionExpired;

        var context = await VietAnScope.LoadAsync(customers, agentId, cmd.CustomerId, ct);
        if (context is null) return AuthErrors.SessionExpired;
        // Máy đã bị đá ra (nhân viên đăng nhập ở thiết bị khác) / mật khẩu đã đổi → không cho chọn khách để lấy phiên mới.
        if (!JwtTokenService.FixedTimeEquals(sessions.VietAnStamp(context.Agent), current.PasswordStamp)) return AuthErrors.SessionExpired;
        if (context.Customer is null) return VietAnStaffErrors.CustomerOutOfScope;
        return sessions.Start(context.Agent, context.Customer, current.IsPersistent, current.AuthTime);
    }
}
