using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Stores;

internal sealed record DisconnectStoreCommand(long Id) : IRequest<Result>;

internal sealed class DisconnectStoreHandler(EcommerceDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<DisconnectStoreCommand, Result>
{
    public async Task<Result> Handle(DisconnectStoreCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return StoreErrors.NotLoggedIn;
        var connection = await db.StoreConnections.FirstOrDefaultAsync(s => s.Id == c.Id && s.CustomerId == customerId && s.DisconnectedAt == null, ct);
        if (connection is null) return StoreErrors.NotFound;

        connection.Disconnect(VietnamTime.Now(clock));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
