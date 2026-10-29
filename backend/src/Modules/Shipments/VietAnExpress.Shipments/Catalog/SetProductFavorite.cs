using System.Security.Cryptography;
using System.Text;
using MediatR;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal sealed record SetProductFavoriteCommand(string Key, bool IsFavorite) : IRequest<Result>;

internal sealed class SetProductFavoriteHandler(ShipmentsDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<SetProductFavoriteCommand, Result>
{
    public Task<Result> Handle(SetProductFavoriteCommand cmd, CancellationToken ct) =>
        CatalogMarkStore.MarkProductAsync(db, user, VietnamTime.Now(clock), cmd.Key, (m, now) => m.SetFavorite(cmd.IsFavorite, now), ct);
}
