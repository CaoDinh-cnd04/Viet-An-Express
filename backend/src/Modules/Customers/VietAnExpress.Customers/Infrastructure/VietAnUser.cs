using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Customers.Infrastructure;

/// <summary>
/// Nhân viên Việt An — bảng <c>dbo.USERS</c> của hệ thống cũ / CMS (giám đốc đến nhân viên; không migration, portal chỉ đọc).
/// Khách được phân công cho 1 nhân viên qua <c>dbo.TCustomer.Staff_ID</c> → <c>USERS.ID</c>.
/// Không map cột mật khẩu (USER_PASSWORD, USER_HASH, MAIL_SMTPPASS): portal không đăng nhập bằng tài khoản CMS.
/// </summary>
internal sealed class VietAnUser
{
    public int Id { get; init; }
    public bool? Deleted { get; init; }
    public string? UserName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Title { get; init; }
    public string? Email { get; init; }
    public string? MobilePhone { get; init; }
    public string? WorkPhone { get; init; }
    /// <summary>"ACTIVE" = đang hoạt động (theo dữ liệu CMS).</summary>
    public string? Status { get; init; }
    public bool? IsAdmin { get; init; }
    /// <summary>1 = xem mọi khách (không chỉ khách mình phụ trách) — quy tắc của CMS.</summary>
    public int? ViewCustomerAll { get; init; }
    public int? BranchId { get; init; }
}

internal sealed class VietAnUserConfiguration : IEntityTypeConfiguration<VietAnUser>
{
    public void Configure(EntityTypeBuilder<VietAnUser> b)
    {
        b.ToTable("USERS", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID");
        b.Property(x => x.Deleted).HasColumnName("DELETED");
        b.Property(x => x.UserName).HasColumnName("USER_NAME").HasMaxLength(60);
        b.Property(x => x.FirstName).HasColumnName("FIRST_NAME").HasMaxLength(1000);
        b.Property(x => x.LastName).HasColumnName("LAST_NAME").HasMaxLength(1000);
        b.Property(x => x.Title).HasColumnName("TITLE").HasMaxLength(1000);
        b.Property(x => x.Email).HasColumnName("EMAIL1").HasMaxLength(1000);
        b.Property(x => x.MobilePhone).HasColumnName("PHONE_MOBILE").HasMaxLength(50);
        b.Property(x => x.WorkPhone).HasColumnName("PHONE_WORK").HasMaxLength(50);
        b.Property(x => x.Status).HasColumnName("STATUS").HasMaxLength(25);
        b.Property(x => x.IsAdmin).HasColumnName("IS_ADMIN");
        b.Property(x => x.ViewCustomerAll).HasColumnName("VIEW_CUSTOMER_ALL");
        b.Property(x => x.BranchId).HasColumnName("BRANCH_ID");
    }
}
