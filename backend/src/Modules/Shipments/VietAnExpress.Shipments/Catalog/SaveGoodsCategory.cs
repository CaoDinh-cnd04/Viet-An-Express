using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal sealed record SaveGoodsCategoryCommand(long? Id, GoodsCategoryInput Input) : IRequest<Result<GoodsCategoryDto>>;

internal sealed class SaveGoodsCategoryHandler(ShipmentsDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<SaveGoodsCategoryCommand, Result<GoodsCategoryDto>>
{
    public async Task<Result<GoodsCategoryDto>> Handle(SaveGoodsCategoryCommand cmd, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return OrderErrors.CustomerRequired;
        var name = GoodsCategoryRules.ValidateName(cmd.Input.Name);
        if (name.IsFailure) return name.Error;

        // Không trùng tên (không phân biệt hoa thường — theo collation của DB) với nhóm chung / nhóm khác của khách.
        var duplicate = await db.VisibleCategories(user).AnyAsync(c => c.Name == name.Value && (cmd.Id == null || c.Id != cmd.Id), ct);
        if (duplicate) return GoodsCategoryErrors.Duplicate(name.Value);

        var now = VietnamTime.Now(clock);
        GoodsCategory? category;
        if (cmd.Id is { } id)
        {
            category = await db.GoodsCategories.FirstOrDefaultAsync(c => c.Id == id && c.CustomerId == customerId, ct);
            if (category is null) return GoodsCategoryErrors.NotFound;
            category.Update(name.Value, cmd.Input.IsFavorite, now);
        }
        else
        {
            category = GoodsCategory.Create(customerId, name.Value, cmd.Input.IsFavorite, now);
            db.GoodsCategories.Add(category);
        }
        await db.SaveChangesAsync(ct);
        return GoodsCategoryRules.ToDto(category);
    }
}
