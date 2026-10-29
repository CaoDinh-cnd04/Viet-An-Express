using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.AddressBook;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal sealed record GetGoodsCategoriesQuery : IRequest<IReadOnlyList<GoodsCategoryDto>>;

internal sealed class GetGoodsCategoriesHandler(ShipmentsDbContext db, ICurrentUser user)
    : IRequestHandler<GetGoodsCategoriesQuery, IReadOnlyList<GoodsCategoryDto>>
{
    public async Task<IReadOnlyList<GoodsCategoryDto>> Handle(GetGoodsCategoriesQuery q, CancellationToken ct)
    {
        var categories = await db.VisibleCategories(user).AsNoTracking().ToListAsync(ct);
        if (user.CustomerId is not { } customerId) return GoodsCategoryRules.Order(categories);

        var keys = await db.CatalogMarks.AsNoTracking()
            .Where(m => m.CustomerId == customerId && m.Kind == CatalogMark.Category && m.IsFavorite)
            .Select(m => m.Key)
            .ToListAsync(ct);
        var favorites = keys.Select(k => long.TryParse(k, out var id) ? id : 0).Where(id => id > 0).ToHashSet();
        return GoodsCategoryRules.Order(categories, favorites);
    }
}
