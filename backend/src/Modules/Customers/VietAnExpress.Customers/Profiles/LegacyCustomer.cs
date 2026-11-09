using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Customers.Profiles;

/// <summary>
/// Hồ sơ khách hàng — bảng <c>dbo.TCustomer</c> của hệ thống cũ (không migration, không đổi cấu trúc).
/// Chỉ map các cột portal cần; portal chỉ đọc (sửa hồ sơ khách do CS làm ở hệ thống cũ / CMS) — trừ khách mới nhân viên Việt An
/// tạo trên portal (<see cref="NewCustomers.NewCustomerStore"/>).
/// </summary>
internal sealed class LegacyCustomer
{
    public long Id { get; private set; }
    public string? Code { get; private set; }
    public string? CompanyName { get; private set; }
    /// <summary>Tên công ty in trên hóa đơn (TCustomer.InvoiceCustomerName) — tên hiển thị trên trang MyTracking.</summary>
    public string? InvoiceName { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? Email { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Phone { get; private set; }
    public string? Tel { get; private set; }
    public string? Address1 { get; private set; }
    public string? TaxCode { get; private set; }
    public int? Status { get; private set; }
    /// <summary>Nhân viên Việt An phụ trách khách (Staff_ID → dbo.USERS.ID) — CMS phân công.</summary>
    public int AccountManagerId { get; private set; }
    /// <summary>
    /// Nhân viên được xem / chăm sóc khách ở CMS (TCustomer.SubTer, dạng "1158|1157|1159|") — CS được gán khách qua cột này
    /// (Staff_ID là sale). CMS lọc danh sách khách của nhân viên theo cột này (proc p_TCustomer_Row).
    /// </summary>
    public string? SubTer { get; private set; }
    /// <summary>Tên đăng nhập portal / hệ thống cũ (TCustomer.Login_UserName) — chỉ để nhân viên Việt An tìm khách.</summary>
    public string? LoginUserName { get; private set; }
    /// <summary>Nguồn khách (TCustomer.CustomerSource) — <see cref="NewCustomers.NewCustomerRules.Marker"/> = khách mới tạo từ portal, CS cần hoàn thiện ở CMS.</summary>
    public string? Source { get; private set; }
    /// <summary>Nhân viên tạo hồ sơ (TCustomer.Reg_ID → USERS.ID). Khách mới: Staff_ID = Reg_ID nghĩa là chưa ai gán người phụ trách.</summary>
    public int? CreatedBy { get; private set; }

    /// <summary>Độ dài cột trong dbo.TCustomer.</summary>
    public const int CompanyNameMaxLength = 250;
    public const int InvoiceNameMaxLength = 500;
    public const int TextMaxLength = 150;
    public const int AddressMaxLength = 250;
}

internal sealed class LegacyCustomerConfiguration : IEntityTypeConfiguration<LegacyCustomer>
{
    public void Configure(EntityTypeBuilder<LegacyCustomer> b)
    {
        b.ToTable("TCustomer", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("CustomerID");
        b.Property(x => x.Code).HasColumnName("CustomerCode");
        b.Property(x => x.CompanyName).HasColumnName("CustomerName").HasMaxLength(LegacyCustomer.CompanyNameMaxLength);
        b.Property(x => x.InvoiceName).HasColumnName("InvoiceCustomerName").HasMaxLength(LegacyCustomer.InvoiceNameMaxLength);
        b.Property(x => x.ContactName).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.ContactPhone).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.ContactEmail).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.Address1).HasMaxLength(LegacyCustomer.AddressMaxLength);
        b.Property(x => x.TaxCode).HasMaxLength(LegacyCustomer.TextMaxLength);
        b.Property(x => x.AccountManagerId).HasColumnName("Staff_ID");
        b.Property(x => x.SubTer).HasMaxLength(500);
        b.Property(x => x.LoginUserName).HasColumnName("Login_UserName").HasMaxLength(50);
        b.Property(x => x.CreatedBy).HasColumnName("Reg_ID");
        b.Property(x => x.Source).HasColumnName("CustomerSource").HasMaxLength(LegacyCustomer.TextMaxLength);
    }
}
