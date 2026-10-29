using VietAnExpress.Ecommerce.Contracts;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Ecommerce.Orders;

/// <summary>
/// Phạm vi đơn E-commerce: đơn của khách (dbo.TCustomer.CustomerID). Tài khoản con không có quyền
/// <see cref="EcommercePermissions.ViewAll"/> chỉ thấy đơn mình nhập tay / nhập CSV — đơn đồng bộ từ sàn (không có người tạo)
/// chỉ tài khoản chính và người có quyền xem toàn bộ thấy.
/// </summary>
internal static class EcomOrderScope
{
    public static IQueryable<MarketplaceOrder> VisibleTo(this IQueryable<MarketplaceOrder> query, ICurrentUser user, long customerId)
    {
        query = query.Where(o => o.CustomerId == customerId);
        return user.RestrictedStaffId(EcommercePermissions.ViewAll) is { } staffId ? query.Where(o => o.CreatedByStaffId == staffId) : query;
    }
}
