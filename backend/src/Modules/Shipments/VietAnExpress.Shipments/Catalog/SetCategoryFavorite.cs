using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

/// <summary>Đánh dấu / bỏ yêu thích — được cả nhóm chung (lưu riêng cho khách trong dbo.MatHangKhachHang).</summary>
internal sealed record SetCategoryFavoriteCommand(long Id, bool IsFavorite) : IRequest<Result>;

internal sealed class SetCategoryFavoriteHandler(ShipmentsDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<SetCategoryFavoriteCommand, Result>
{
    public async Task<Result> Handle(SetCategoryFavoriteCommand cmd, CancellationToken ct)
    {
        if (user.CustomerId is not { } customerId) return OrderErrors.CustomerRequired;
        var category = await db.VisibleCategories(user).FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (category is null) return GoodsCategoryErrors.NotFound;

        var now = VietnamTime.Now(clock);
        if (category.CustomerId is null)
        {
            var key = category.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await CatalogMarkStore.UpsertAsync(db, customerId, CatalogMark.Category, key, m => m.SetFavorite(cmd.IsFavorite, now), now, ct);
        }
        else
        {
            category.Update(category.Name, cmd.IsFavorite, now);
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}
