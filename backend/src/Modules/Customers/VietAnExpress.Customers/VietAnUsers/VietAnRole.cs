using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Customers.VietAnUsers;

/// <summary>Vai trò nhân viên của hệ thống cũ / CMS — <c>dbo.ACL_ROLES</c> (Administrator, ALL, Sale, CS, Accounting…); portal chỉ đọc.</summary>
internal sealed class VietAnRole
{
    public int Id { get; init; }
    public bool? Deleted { get; init; }
    public string? Name { get; init; }
}

/// <summary>Nhân viên ↔ vai trò — <c>dbo.ACL_ROLES_USERS</c> (ROLE_ID → ACL_ROLES.ID, USER_ID → USERS.ID); portal chỉ đọc.</summary>
internal sealed class VietAnRoleUser
{
    public int Id { get; init; }
    public bool? Deleted { get; init; }
    public int? RoleId { get; init; }
    public int? UserId { get; init; }
}

/// <summary>Quy tắc vai trò. Hàm thuần — có test.</summary>
internal static class VietAnRoles
{
    // ACL_ROLES.NAME, không phân biệt hoa thường.
    public const string Administrator = "Administrator";
    public const string Cs = "CS";
    public const string All = "ALL";

    /// <summary>
    /// Xem mọi khách (08/11/2026 người dùng chốt): Administrator, hoặc CS có thêm vai trò ALL.
    /// ALL một mình không đủ — hầu hết sale cũng có ALL (quyền menu CMS) nhưng sale chỉ thấy khách theo Staff_ID.
    /// </summary>
    public static bool SeesAllCustomers(IEnumerable<string?> roleNames)
    {
        var roles = roleNames.ToList();
        return Has(roles, Administrator) || (Has(roles, Cs) && Has(roles, All));
    }

    /// <summary>CS được giao khách qua TCustomer.SubTer; sale và vai trò khác chỉ theo Staff_ID.</summary>
    public static bool UsesSubTer(IEnumerable<string?> roleNames) => Has(roleNames, Cs);

    private static bool Has(IEnumerable<string?> roles, string role) =>
        roles.Any(n => string.Equals(n?.Trim(), role, StringComparison.OrdinalIgnoreCase));
}

internal sealed class VietAnRoleConfiguration : IEntityTypeConfiguration<VietAnRole>
{
    public void Configure(EntityTypeBuilder<VietAnRole> b)
    {
        b.ToTable("ACL_ROLES", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.Deleted).HasColumnName("DELETED");
        b.Property(x => x.Name).HasColumnName("NAME");
    }
}

internal sealed class VietAnRoleUserConfiguration : IEntityTypeConfiguration<VietAnRoleUser>
{
    public void Configure(EntityTypeBuilder<VietAnRoleUser> b)
    {
        b.ToTable("ACL_ROLES_USERS", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.Deleted).HasColumnName("DELETED");
        b.Property(x => x.RoleId).HasColumnName("ROLE_ID");
        b.Property(x => x.UserId).HasColumnName("USER_ID");
    }
}
