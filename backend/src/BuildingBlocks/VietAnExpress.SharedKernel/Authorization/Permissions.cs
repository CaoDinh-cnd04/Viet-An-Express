namespace VietAnExpress.SharedKernel.Authorization;

/// <summary>Tên claim trong JWT — Identity phát, host và module đọc.</summary>
public static class VaClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string Permission = "permission";
    public const string CustomerId = "customer_id";
    /// <summary>Có khi người đăng nhập là tài khoản con (nhân viên) của khách — dbo.TaiKhoanNhanVien.ID.</summary>
    public const string StaffId = "staff_id";
}

/// <summary>
/// Vai trò. Controller luôn kiểm tra theo permission, không theo vai trò.
/// Tài khoản chính của khách (dbo.TCustomer) là admin: nhận mọi quyền có vai trò <see cref="Customer"/>;
/// tài khoản con (nhân viên) chỉ nhận các quyền admin đã chọn.
/// </summary>
public static class SystemRoles
{
    public const string Customer = "customer";
    public const string CustomerStaff = "customer_staff";
    /// <summary>Quản trị Việt An (trang /admin) — tài khoản cố định trong code, không phải khách.</summary>
    public const string VietAnAdmin = "va_admin";
}

/// <summary>1 quyền và các vai trò được cấp quyền này (khai báo trong code, không lưu DB).</summary>
public sealed record PermissionDefinition(string Code, string Description, params string[] DefaultRoles);

/// <summary>Mỗi module khai báo danh sách quyền của mình; Identity gom lại để cấp vào token.</summary>
public interface IPermissionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}
