using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Nút "Tạo khách mới" (khách lần đầu gửi, chưa có hồ sơ): tạo dòng dbo.TCustomer tạm đánh dấu khách mới
/// (CustomerSource "Portal - Khách mới", Staff_ID = nhân viên đang làm) rồi cấp phiên làm thay khách đó như "Chọn khách".
/// Người gửi trên form tạo đơn để trống, sửa được; lưu nháp / in thì ghi vào hồ sơ khách (SaveDraft → FillNewCustomerAsync).
/// </summary>
internal sealed record CreateNewCustomerCommand(string? RefreshToken) : IRequest<Result<AuthSession>>;

internal sealed class CreateNewCustomerHandler(ICustomersApi customers, SessionService sessions, ITokenService tokens, ICurrentUser user)
    : IRequestHandler<CreateNewCustomerCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(CreateNewCustomerCommand cmd, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId) return VietAnStaffErrors.VietAnOnly;
        var current = string.IsNullOrEmpty(cmd.RefreshToken) ? null : await tokens.ReadRefreshTokenAsync(cmd.RefreshToken);
        if (current?.VietAnUserId != agentId) return AuthErrors.SessionExpired;
        if (await customers.GetVietAnUserAsync(agentId, ct) is not { } agent) return AuthErrors.SessionExpired;
        if (!JwtTokenService.FixedTimeEquals(sessions.VietAnStamp(agent), current.PasswordStamp)) return AuthErrors.SessionExpired;

        var id = await customers.CreateNewCustomerAsync(agent.Id, ct);
        if (await customers.GetByIdAsync(id, ct) is not { } customer) return VietAnStaffErrors.CustomerOutOfScope;
        return sessions.Start(agent, customer, current.IsPersistent, current.AuthTime);
    }
}
