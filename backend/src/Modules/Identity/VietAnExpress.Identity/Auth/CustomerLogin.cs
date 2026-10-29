using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Identity.Auth;

/// <summary>
/// Tài khoản đăng nhập của khách — cột Login_UserName / Login_Password trong <c>dbo.TCustomer</c> (dùng chung với hệ thống cũ).
/// Hệ thống cũ so khớp mật khẩu dạng chữ thường, nên portal cũng lưu y như vậy để 2 bên cùng đăng nhập được.
/// </summary>
internal sealed class CustomerLogin
{
    private CustomerLogin() { } // EF Core

    public CustomerLogin(long customerId, string? userName, string? password, int? customerTypeId = null, int? viewLinkTracking = null)
    {
        CustomerId = customerId;
        UserName = userName;
        Password = password;
        CustomerTypeId = customerTypeId;
        ViewLinkTracking = viewLinkTracking;
    }

    public long CustomerId { get; private set; }
    public string? UserName { get; private set; }
    public string? Password { get; private set; }
    /// <summary>Loại khách (TCustomer.CustomerTypeID → dbo.TCustomerType, chỉ đọc) — đăng thông báo theo loại khách.</summary>
    public int? CustomerTypeId { get; private set; }
    /// <summary>TCustomer.ViewLinkTracking (chỉ đọc, CMS bật / tắt): 1 = khách được vào trang cấu hình MyTracking.</summary>
    public int? ViewLinkTracking { get; private set; }

    public bool MyTrackingAllowed => ViewLinkTracking == 1;

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public void ChangePassword(string newPassword) => Password = newPassword;
}

internal sealed class CustomerLoginConfiguration : IEntityTypeConfiguration<CustomerLogin>
{
    public void Configure(EntityTypeBuilder<CustomerLogin> b)
    {
        b.ToTable("TCustomer", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.CustomerId);
        b.Property(x => x.CustomerId).HasColumnName("CustomerID").ValueGeneratedNever();
        b.Property(x => x.UserName).HasColumnName("Login_UserName").HasMaxLength(50);
        b.Property(x => x.Password).HasColumnName("Login_Password").HasMaxLength(150);
        b.Property(x => x.CustomerTypeId).HasColumnName("CustomerTypeID");
        b.Property(x => x.ViewLinkTracking).HasColumnName("ViewLinkTracking");
    }
}
