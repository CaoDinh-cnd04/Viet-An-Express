using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Shared;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Trang MyTracking của khách — tài khoản chính cấu hình; khách có dòng cấu hình thì ai có link /tracking/{mã khách}/{số bill}?ref=...
/// cũng xem được. Chỉ vào trang cấu hình được khi CMS bật TCustomer.ViewLinkTracking = 1.
/// </summary>
internal static class MyTrackingAccess
{
    /// <summary>CMS cho phép (TCustomer.ViewLinkTracking = 1) — kiểm tra lại mỗi lần để CMS tắt là chặn ngay, không đợi token hết hạn.</summary>
    public static Task<bool> IsMyTrackingAllowedAsync(this IdentityDbContext db, long customerId, CancellationToken ct) =>
        db.Logins.AsNoTracking().AnyAsync(l => l.CustomerId == customerId && l.ViewLinkTracking == 1, ct);
}
