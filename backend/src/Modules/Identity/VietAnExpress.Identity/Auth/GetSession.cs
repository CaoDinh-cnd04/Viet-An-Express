using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Auth;

// ---------- Khách đang đăng nhập (GET /me) ----------

internal sealed record GetSessionQuery : IRequest<Result<SessionUserDto>>;

internal sealed class GetSessionHandler(IdentityDbContext db, ICustomersApi customers, SessionService sessions, ICurrentUser currentUser)
    : IRequestHandler<GetSessionQuery, Result<SessionUserDto>>
{
    public async Task<Result<SessionUserDto>> Handle(GetSessionQuery q, CancellationToken ct)
    {
        if (currentUser.VietAnUserId is { } agentId)
        {
            var context = await VietAnScope.LoadAsync(customers, agentId, currentUser.CustomerId ?? 0, ct);
            return context is null ? AuthErrors.SessionExpired : sessions.BuildUser(context.Agent, context.Customer);
        }
        if (currentUser.CustomerId is not { } customerId) return AuthErrors.SessionExpired;
        SessionUserDto? user;
        if (currentUser.StaffId is { } staffId)
        {
            var staff = await db.StaffAccounts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId && s.CustomerId == customerId, ct);
            user = staff is null ? null : await sessions.BuildUserAsync(staff, ct);
        }
        else
        {
            var login = await db.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.CustomerId == customerId, ct);
            user = login is { HasPassword: true } ? await sessions.BuildUserAsync(login, ct) : null;
        }
        return user is null ? AuthErrors.SessionExpired : user;
    }
}
