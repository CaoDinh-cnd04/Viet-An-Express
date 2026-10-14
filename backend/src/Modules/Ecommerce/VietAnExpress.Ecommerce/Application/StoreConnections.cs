using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Domain;
using VietAnExpress.Ecommerce.Infrastructure;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Application;

/// <summary>Khớp <c>StoreConnection</c> của frontend (API_CONTRACT.md §5.1). Không bao giờ chứa token.</summary>
internal sealed record StoreConnectionDto(
    string Id, string Platform, string ShopName, string? ShopDomain, string? Region, string Status,
    DateTimeOffset ConnectedAt, DateTimeOffset? LastSyncAt, string? LastError);

internal sealed record GetStoreConnectionsQuery : IRequest<IReadOnlyList<StoreConnectionDto>>;

internal sealed record StartStoreConnectionCommand(string? Platform) : IRequest<Result>;

internal sealed record DisconnectStoreCommand(long Id) : IRequest<Result>;

internal static class StoreErrors
{
    public static readonly Error NotLoggedIn = Error.Unauthorized("AUTH_REQUIRED", "Vui lòng đăng nhập");
    public static readonly Error NotFound = Error.NotFound("ECOM_STORE_NOT_FOUND", "Không tìm thấy cửa hàng");
    public static Error ChannelUnavailable(string name) => Error.BusinessRule("ECOM_CHANNEL_UNAVAILABLE", $"Chưa hỗ trợ kết nối {name}, Việt An đang hoàn thiện");
}

internal sealed class StoreConnectionHandlers(EcommerceDbContext db, ICurrentUser user, TimeProvider clock) :
    IRequestHandler<GetStoreConnectionsQuery, IReadOnlyList<StoreConnectionDto>>,
    IRequestHandler<StartStoreConnectionCommand, Result>,
    IRequestHandler<DisconnectStoreCommand, Result>
{
    public async Task<IReadOnlyList<StoreConnectionDto>> Handle(GetStoreConnectionsQuery q, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return [];
        var rows = await db.StoreConnections.AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.DisconnectedAt == null)
            .OrderBy(s => s.ConnectedAt)
            .ToListAsync(ct);
        return [.. rows.Select(ToDto)];
    }

    public async Task<Result> Handle(StartStoreConnectionCommand c, CancellationToken ct)
    {
        if (user.CustomerId is null) return StoreErrors.NotLoggedIn;
        var code = c.Platform?.Trim().ToLowerInvariant();
        var channel = await db.SalesChannels.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (channel is null) return Error.Validation("ECOM_CHANNEL_INVALID", "Kênh bán không hợp lệ");
        // Chưa có adapter kênh nào — viết adapter của kênh rồi mới mở kết nối ở đây.
        return StoreErrors.ChannelUnavailable(channel.Name);
    }

    public async Task<Result> Handle(DisconnectStoreCommand c, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return StoreErrors.NotLoggedIn;
        var connection = await db.StoreConnections.FirstOrDefaultAsync(s => s.Id == c.Id && s.CustomerId == customerId && s.DisconnectedAt == null, ct);
        if (connection is null) return StoreErrors.NotFound;

        connection.Disconnect(VietnamTime.Now(clock));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static StoreConnectionDto ToDto(StoreConnection s) => new(
        s.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), s.ChannelCode, s.ShopName, s.ShopDomain ?? s.ShopId, s.Region, s.Status,
        AsVietnam(s.ConnectedAt), s.LastSyncAt is { } sync ? AsVietnam(sync) : null, s.LastError);

    /// <summary>Cột datetime lưu giờ Việt Nam (như bảng cũ) → trả ISO kèm +07:00.</summary>
    private static DateTimeOffset AsVietnam(DateTime local) => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
}
