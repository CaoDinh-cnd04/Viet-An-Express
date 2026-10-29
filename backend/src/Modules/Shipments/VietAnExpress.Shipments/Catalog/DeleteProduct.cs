using System.Security.Cryptography;
using System.Text;
using MediatR;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>Xóa khỏi thư viện = ẩn mặt hàng (dòng hàng trong đơn cũ giữ nguyên).</summary>
internal sealed record DeleteProductCommand(string Key) : IRequest<Result>;

internal sealed class DeleteProductHandler(ShipmentsDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<DeleteProductCommand, Result>
{
    public Task<Result> Handle(DeleteProductCommand cmd, CancellationToken ct) =>
        CatalogMarkStore.MarkProductAsync(db, user, VietnamTime.Now(clock), cmd.Key, (m, now) => m.MarkDeleted(now), ct);
}
