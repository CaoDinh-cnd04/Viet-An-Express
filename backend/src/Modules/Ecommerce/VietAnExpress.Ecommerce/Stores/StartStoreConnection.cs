using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Ecommerce.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Ecommerce.Stores;

internal sealed record StartStoreConnectionCommand(string? Platform) : IRequest<Result>;

internal sealed class StartStoreConnectionHandler(EcommerceDbContext db, ICurrentUser user)
    : IRequestHandler<StartStoreConnectionCommand, Result>
{
    public async Task<Result> Handle(StartStoreConnectionCommand c, CancellationToken ct)
    {
        if (user.CustomerId is null) return StoreErrors.NotLoggedIn;
        var code = c.Platform?.Trim().ToLowerInvariant();
        var channel = await db.SalesChannels.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (channel is null) return Error.Validation("ECOM_CHANNEL_INVALID", "Kênh bán không hợp lệ");
        // Chưa có adapter kênh nào — viết adapter của kênh rồi mới mở kết nối ở đây.
        return StoreErrors.ChannelUnavailable(channel.Name);
    }
}
