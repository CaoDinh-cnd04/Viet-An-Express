using VietAnExpress.Customers.Contracts;

namespace VietAnExpress.Customers.NewCustomers;

/// <summary>
/// Khách mới nhân viên Việt An tạo trên portal (khách lần đầu gửi, chưa có hồ sơ): dòng dbo.TCustomer tạm,
/// đánh dấu bằng cột có sẵn <c>CustomerSource</c> = <see cref="Marker"/> (người dùng chốt 06/11/2026, không thêm cột).
/// CS hoàn thiện hồ sơ ở CMS và đổi nguồn khách → hết là "khách mới", portal thôi ghi đè thông tin người gửi.
/// Hàm thuần — có test.
/// </summary>
internal static class NewCustomerRules
{
    public const string Marker = "Portal - Khách mới";

    /// <summary>Vai trò (dbo.ACL_ROLES.NAME) được gán nhân viên phụ trách cho khách mới.</summary>
    public static readonly IReadOnlySet<string> AssignerRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Administrator", "CS" };

    /// <summary>Vai trò nhân viên được chọn làm người phụ trách (sales / CS).</summary>
    public static readonly IReadOnlySet<string> SupportRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sale", "Sales", "CS" };

    public static bool IsNew(string? source) => string.Equals(source?.Trim(), Marker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Khách mới chưa được gán người phụ trách: lúc tạo Staff_ID tạm = người tạo (Reg_ID, cột NOT NULL) — còn bằng nhau là chưa gán.
    /// (Quản trị / CS tự gán chính mình cho khách mình tạo cũng hiện "chưa gán".)
    /// </summary>
    public static bool ManagerPending(string? source, int managerId, int? createdBy) =>
        IsNew(source) && (managerId <= 0 || managerId == createdBy);

    public static bool CanAssign(IEnumerable<string?> roleNames) =>
        roleNames.Any(n => n is not null && AssignerRoles.Contains(n.Trim()));

    public static bool IsSupportStaff(IEnumerable<string?> roleNames) =>
        roleNames.Any(n => n is not null && SupportRoles.Contains(n.Trim()));

    /// <summary>Thông tin người gửi → giá trị ghi vào TCustomer: bỏ khoảng trắng thừa, cắt theo độ dài cột, ô trống = NULL.</summary>
    public static NewCustomerInfo Clean(NewCustomerInfo info) => new(
        Clip(info.CompanyName, 250), Clip(info.ContactName, 150), Clip(info.Phone, 150),
        Clip(info.Address, 250), Clip(info.TaxCode, 150), Clip(info.Email, 150));

    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }
}
