using System.Linq.Expressions;

namespace VietAnExpress.Customers.Profiles;

/// <summary>
/// Phạm vi khách của 1 nhân viên Việt An (không phải Administrator): sale phụ trách (TCustomer.Staff_ID) hoặc nhân viên được gán ở
/// TCustomer.SubTer — CS được CMS gán khách qua cột này, Staff_ID là sale (06/11/2026: CS Khuê Lê ID 1157 có 0 khách theo Staff_ID
/// nhưng 291 khách theo SubTer). SubTer dạng "1158|1157|1159|" (có / không dấu "|" cuối, có thể có khoảng trắng) — khớp đúng cả mã.
/// </summary>
internal static class CustomerScope
{
    /// <param name="includeSubTer">Chỉ vai trò CS tính thêm SubTer; sale chỉ theo Staff_ID (08/11/2026).</param>
    public static Expression<Func<LegacyCustomer, bool>> OfStaff(int staffId, bool includeSubTer)
    {
        if (!includeSubTer) return c => c.AccountManagerId == staffId;
        var token = $"|{staffId}|";
        return c => c.AccountManagerId == staffId
            || ("|" + (c.SubTer ?? "").Replace(" ", "") + "|").Contains(token);
    }

    /// <summary>Cùng quy tắc, tính trên bộ nhớ. Hàm thuần — có test.</summary>
    public static bool Matches(int accountManagerId, string? subTer, int staffId, bool includeSubTer) =>
        accountManagerId == staffId || (includeSubTer && ("|" + (subTer ?? "").Replace(" ", "") + "|").Contains($"|{staffId}|", StringComparison.Ordinal));
}
