using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Ecommerce.Stores;

internal sealed record GetStoreConnectionsQuery : IRequest<IReadOnlyList<StoreConnectionDto>>;

internal sealed class GetStoreConnectionsHandler(EcommerceDbContext db, ICurrentUser user)
    : IRequestHandler<GetStoreConnectionsQuery, IReadOnlyList<StoreConnectionDto>>
{
    private static StoreConnectionDto ToDto(StoreConnection s) => new(
        s.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), s.ChannelCode, s.ShopName, s.ShopDomain ?? s.ShopId, s.Region, s.Status,
        AsVietnam(s.ConnectedAt), s.LastSyncAt is { } sync ? AsVietnam(sync) : null, s.LastError);

    /// <summary>Cột datetime lưu giờ Việt Nam (như bảng cũ) → trả ISO kèm +07:00.</summary>
    private static DateTimeOffset AsVietnam(DateTime local) => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7));

    public async Task<IReadOnlyList<StoreConnectionDto>> Handle(GetStoreConnectionsQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return [];
        var rows = await db.StoreConnections.AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.DisconnectedAt == null)
            .OrderBy(s => s.ConnectedAt)
            .ToListAsync(ct);
        return [.. rows.Select(ToDto)];
    }
}
