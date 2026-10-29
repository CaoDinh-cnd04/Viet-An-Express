using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.Ecommerce.Stores;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Orders;

internal sealed record SyncStoreCommand(long StoreId) : IRequest<Result<SyncResult>>;

internal sealed class SyncStoreHandler(EcommerceDbContext db, ICurrentUser user) : IRequestHandler<SyncStoreCommand, Result<SyncResult>>
{
    public async Task<Result<SyncResult>> Handle(SyncStoreCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return StoreErrors.NotLoggedIn;
        var store = await db.StoreConnections.FirstOrDefaultAsync(s => s.Id == c.StoreId && s.CustomerId == customerId && s.DisconnectedAt == null, ct);
        if (store is null) return StoreErrors.NotFound;
        // Chưa có adapter kênh nào để kéo đơn.
        return StoreErrors.ChannelUnavailable(store.ChannelCode);
    }
}

/// <summary>Frontend đọc thẳng <c>message</c>, <c>importedCount</c> (không bọc trong data).</summary>
internal sealed record SyncResult(string Message, int ImportedCount);
