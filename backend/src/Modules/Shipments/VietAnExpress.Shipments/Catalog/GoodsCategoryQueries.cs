using VietAnExpress.SharedKernel.Application;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Catalog;

internal static class GoodsCategoryQueries
{
    /// <summary>Nhóm chung của Việt An + nhóm riêng của khách đang đăng nhập.</summary>
    public static IQueryable<GoodsCategory> VisibleCategories(this ShipmentsDbContext db, ICurrentUser user) =>
        user.CustomerId is { } customerId
            ? db.GoodsCategories.Where(c => c.CustomerId == null || c.CustomerId == customerId)
            : db.GoodsCategories.Where(c => c.CustomerId == null);
}
