using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Quản trị / CS (vai trò Administrator hoặc CS) gán nhân viên phụ trách — sales hoặc CS — cho khách mới
/// (TCustomer.Staff_ID). Chỉ khách còn đánh dấu khách mới; khách thường đổi người phụ trách ở CMS.
/// </summary>
internal sealed record AssignCustomerManagerCommand(long CustomerId, int StaffId) : IRequest<Result>;

internal sealed class AssignCustomerManagerHandler(ICustomersApi customers, ICurrentUser user)
    : IRequestHandler<AssignCustomerManagerCommand, Result>
{
    public async Task<Result> Handle(AssignCustomerManagerCommand cmd, CancellationToken ct)
    {
        if (user.VietAnUserId is not { } agentId || await customers.GetVietAnUserAsync(agentId, ct) is not { } agent) return VietAnStaffErrors.VietAnOnly;
        if (!agent.CanAssignManager) return VietAnStaffErrors.CannotAssignManager;
        if (await customers.CustomerInScopeAsync(agent, cmd.CustomerId, ct) is null) return VietAnStaffErrors.CustomerOutOfScope;
        return await customers.AssignNewCustomerManagerAsync(cmd.CustomerId, cmd.StaffId, agent.Id, ct)
            ? Result.Success()
            : VietAnStaffErrors.AssignFailed;
    }
}
