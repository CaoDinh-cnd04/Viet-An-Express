namespace VietAnExpress.Identity.Support;

/// <summary>Yêu cầu khách đang đăng nhập được xem / trả lời.</summary>
internal static class SupportRequestScope
{
    /// <summary>Tài khoản chính thấy mọi yêu cầu của công ty; tài khoản con chỉ thấy yêu cầu mình gửi.</summary>
    public static IQueryable<SupportRequest> OwnedBy(this IQueryable<SupportRequest> query, long customerId, long? staffId)
    {
        query = query.Where(t => t.CustomerId == customerId);
        return staffId is { } id ? query.Where(t => t.StaffId == id) : query;
    }
}
