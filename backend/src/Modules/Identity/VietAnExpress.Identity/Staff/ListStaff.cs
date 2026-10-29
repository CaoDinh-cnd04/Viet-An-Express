using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Staff;

/// <summary>Danh sách tài khoản con của khách (chỉ tài khoản chính xem được).</summary>
internal sealed record ListStaffQuery : IRequest<Result<IReadOnlyList<StaffAccountDto>>>;

internal sealed class ListStaffHandler(IdentityDbContext db, SessionService sessions, ICurrentUser user)
    : IRequestHandler<ListStaffQuery, Result<IReadOnlyList<StaffAccountDto>>>
{
    public async Task<Result<IReadOnlyList<StaffAccountDto>>> Handle(ListStaffQuery q, CancellationToken ct)
    {
        if (user.MainAccountCustomerId() is not { } customerId) return IdentityErrors.AdminOnly;
        var staff = await db.StaffAccounts.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderBy(s => s.UserName)
            .ToListAsync(ct);
        return staff.Select(s => StaffAccounts.ToDto(s, sessions)).ToList();
    }
}
