using MediatR;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

internal sealed record ListAssignablePermissionsQuery : IRequest<Result<IReadOnlyList<AssignablePermissionDto>>>;

internal sealed class ListAssignablePermissionsHandler(SessionService sessions, ICurrentUser user)
    : IRequestHandler<ListAssignablePermissionsQuery, Result<IReadOnlyList<AssignablePermissionDto>>>
{
    public Task<Result<IReadOnlyList<AssignablePermissionDto>>> Handle(ListAssignablePermissionsQuery q, CancellationToken ct)
    {
        Result<IReadOnlyList<AssignablePermissionDto>> result = user.MainAccountCustomerId() is null
            ? IdentityErrors.AdminOnly
            : sessions.AssignablePermissions.Select(p => new AssignablePermissionDto(p.Code, p.Description)).ToList();
        return Task.FromResult(result);
    }
}
