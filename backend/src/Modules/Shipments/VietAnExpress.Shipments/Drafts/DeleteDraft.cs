using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>Xoá hẳn đơn chưa in cùng kiện / dòng hàng (như hệ thống cũ cho xoá đơn chưa in).</summary>
internal sealed record DeleteDraftCommand(long Id) : IRequest<Result>;

internal sealed class DeleteDraftHandler(ShipmentsDbContext db, OrderAccess access) : IRequestHandler<DeleteDraftCommand, Result>
{
    public async Task<Result> Handle(DeleteDraftCommand cmd, CancellationToken ct)
    {
        var scope = await access.ScopeAsync(ct);
        if (!await access.Apply(db.Drafts(), scope).AnyAsync(o => o.Id == cmd.Id, ct)) return OrderErrors.DraftNotFound;

        // Đơn chưa in xoá hẳn khỏi dbo.MaVanDon cùng chi tiết (như hệ thống cũ cho xoá đơn chưa in).
        // Khóa dòng trước: đơn vừa được in ở nơi khác thì không xoá mất vận đơn đã cấp bill.
        var deleted = false;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            deleted = await DraftStore.LockAsync(db, cmd.Id, ct);
            if (!deleted) return;
            await DraftStore.RemoveLinesAsync(db, cmd.Id, ct);
            db.LegacyOrders.Remove(await db.Drafts().FirstAsync(o => o.Id == cmd.Id, ct));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return deleted ? Result.Success() : OrderErrors.DraftChanged;
    }
}
