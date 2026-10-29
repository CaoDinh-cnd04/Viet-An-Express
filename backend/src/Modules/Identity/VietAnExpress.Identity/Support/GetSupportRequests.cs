using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

// Khách gửi và xem yêu cầu hỗ trợ & góp ý.

internal sealed record GetSupportRequestsQuery : IRequest<Result<IReadOnlyList<SupportRequestDto>>>;

internal sealed class GetSupportRequestsHandler(IdentityDbContext db, ICurrentUser user)
    : IRequestHandler<GetSupportRequestsQuery, Result<IReadOnlyList<SupportRequestDto>>>
{
    private const int ListLimit = 200;

    public async Task<Result<IReadOnlyList<SupportRequestDto>>> Handle(GetSupportRequestsQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId || customerId <= 0) return IdentityErrors.NotLoggedIn;
        var rows = await db.SupportRequests.OwnedBy(customerId, user.StaffId).AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Take(ListLimit)
            .ToListAsync(ct);
        return rows.Select(SupportRequestRules.ToDto).ToList();
    }
}
