using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal sealed record DeleteGoodsCategoryCommand(long Id) : IRequest<Result>;

internal sealed class DeleteGoodsCategoryHandler(ShipmentsDbContext db, ICurrentUser user)
    : IRequestHandler<DeleteGoodsCategoryCommand, Result>
{
    public async Task<Result> Handle(DeleteGoodsCategoryCommand cmd, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return OrderErrors.CustomerRequired;
        var deleted = await db.GoodsCategories.Where(c => c.Id == cmd.Id && c.CustomerId == customerId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? GoodsCategoryErrors.NotFound : Result.Success();
    }
}
