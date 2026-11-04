using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Persistence;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>
/// Phần thương hiệu + cổng liên lạc của trang MyTracking nằm ở hồ sơ khách <c>dbo.TCustomer</c> như trang mytracking.top cũ
/// (không ở TCustomer_MyTracking): logo = <c>Logo_ShippingMark</c> → <c>dbo.TCommonFile</c> (cũng là shipping mark in trên bill),
/// website = <c>Bank</c> (nút Home của trang cũ), màu = <c>Column1</c> / <c>Column2</c>, cổng liên lạc = <c>CongLienLac_*</c>.
/// Địa chỉ (<c>Address1</c>) và điện thoại (<c>Phone</c>) là thông tin người gửi trên bill — chỉ đọc; khách sửa ở MyTracking thì
/// lưu ở TCustomer_MyTracking (<see cref="MyTrackingPage.SetContactInfo"/>).
/// </summary>
internal sealed class MyTrackingBrand
{
    /// <summary>Thư mục logo của trang cũ — đường dẫn hiển thị <c>/Upload/Logo_ShippingMark/{TCommonFile.ServerFileName}</c>.</summary>
    public const string LogoFolder = "/Upload/Logo_ShippingMark/";

    private MyTrackingBrand() { } // EF Core

    /// <summary>Chỉ dùng khi dựng dữ liệu kiểm thử — portal không thêm khách vào dbo.TCustomer.</summary>
    internal MyTrackingBrand(long customerId, string? address = null, string? phone = null)
    {
        CustomerId = customerId;
        Address = address;
        Phone = phone;
    }

    public long CustomerId { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Website { get; private set; }
    public string? Color01 { get; private set; }
    public string? Color02 { get; private set; }
    /// <summary>TCommonFile.CommonFileID của logo.</summary>
    public long? LogoFileId { get; private set; }

    /// <summary>Loại cổng liên lạc của portal → cột CongLienLac_* (Instagram của portal = cột Signal: trang cũ hiện ô Signal bằng icon Instagram).</summary>
    public static readonly IReadOnlyList<(string Type, string Column, int MaxLength)> ContactColumns =
    [
        ("hotline", "CongLienLac_Hotline", 100),
        ("zalo", "CongLienLac_Zalo", 200),
        ("messenger", "CongLienLac_Messenger", 200),
        ("whatsapp", "CongLienLac_WhatsApp", 200),
        ("wechat", "CongLienLac_WeChat", 200),
        ("telegram", "CongLienLac_Telegram", 200),
        ("viber", "CongLienLac_Viber", 200),
        ("line", "CongLienLac_Line", 200),
        ("snapchat", "CongLienLac_Snapchat", 200),
        ("kakaotalk", "CongLienLac_KakaoTalk", 200),
        ("instagram", "CongLienLac_Signal", 200),
        (MyTrackingChannels.Email, "CongLienLac_Email", 200)
    ];

    public const int WebsiteMaxLength = 150;
    public const int ColorMaxLength = 500;

    private readonly Dictionary<string, string?> _contacts = [];

    /// <summary>Giá trị cột CongLienLac_* theo loại (EF đọc / ghi qua indexer).</summary>
    public string? this[string column]
    {
        get => _contacts.GetValueOrDefault(column);
        set => _contacts[column] = value;
    }

    /// <summary>Danh sách {type, value} các cổng có giá trị, theo thứ tự cột như trang cũ.</summary>
    public JsonArray Contacts() => MyTrackingChannels.Build(ContactColumns.Select(c => (c.Type, this[c.Column])));

    /// <summary>Dữ liệu sai độ dài cột → false (không ghi gì).</summary>
    public static bool FitsColumns(JsonObject? brand)
    {
        if ((MyTrackingJson.Text(brand, "website")?.Trim().Length ?? 0) > WebsiteMaxLength) return false;
        if (brand?["contacts"] is not JsonArray contacts) return true;
        return contacts.OfType<JsonObject>().All(item =>
            ContactColumns.FirstOrDefault(c => c.Type == MyTrackingChannels.Type(item)) is not { Column: not null } col
            || MyTrackingChannels.Value(item).Length <= col.MaxLength);
    }

    /// <summary>Ghi website, màu, cổng liên lạc từ cấu hình; logo ghi riêng (<see cref="SetLogo"/>).</summary>
    public void Update(JsonObject? brand)
    {
        Website = Blank(MyTrackingJson.Text(brand, "website"));
        Color01 = Blank(MyTrackingJson.Text(brand, "primaryColor"));
        Color02 = Blank(MyTrackingJson.Text(brand, "secondaryColor"));
        if (brand?["contacts"] is not JsonArray contacts) return;
        foreach (var (_, column, _) in ContactColumns) this[column] = null;
        foreach (var item in contacts.OfType<JsonObject>())
            if (ContactColumns.FirstOrDefault(c => c.Type == MyTrackingChannels.Type(item)).Column is { } column)
                this[column] = Blank(MyTrackingChannels.Value(item));
    }

    public void SetLogo(long? fileId) => LogoFileId = fileId;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Thương hiệu của khách kèm đường dẫn logo (<c>/Upload/Logo_ShippingMark/…</c>, null = chưa có logo).</summary>
internal sealed record MyTrackingBrandView(MyTrackingBrand? Brand, string? Logo);

internal static class MyTrackingBrands
{
    /// <summary>Đọc thương hiệu ở dbo.TCustomer + logo ở dbo.TCommonFile; <paramref name="tracked"/> = để sửa rồi lưu.</summary>
    public static async Task<MyTrackingBrandView> MyTrackingBrandAsync(this IdentityDbContext db, long customerId, CancellationToken ct, bool tracked = false)
    {
        var query = tracked ? db.MyTrackingBrands : db.MyTrackingBrands.AsNoTracking();
        var brand = await query.FirstOrDefaultAsync(b => b.CustomerId == customerId, ct);
        if (brand?.LogoFileId is not { } fileId) return new(brand, null);
        var file = await db.CommonFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, ct);
        return new(brand, string.IsNullOrWhiteSpace(file?.ServerFileName) ? null : file.LogoPath);
    }
}

/// <summary>dbo.TCommonFile — bảng tệp của hệ thống cũ (logo shipping mark, file đính kèm form). Portal chỉ thêm dòng logo.</summary>
internal sealed class LegacyCommonFile
{
    private LegacyCommonFile() { } // EF Core

    public LegacyCommonFile(string fileName, int fileSize, string mimeType, DateTime now)
    {
        RealFileName = fileName;
        ServerFileName = fileName;
        ServerFilePath = MyTrackingBrand.LogoFolder;
        FileSize = fileSize;
        MimeType = mimeType;
        RegDate = now;
    }

    public long Id { get; private set; }
    public string RealFileName { get; private set; } = "";
    public string ServerFileName { get; private set; } = "";
    public string? ServerFilePath { get; private set; }
    public int FileSize { get; private set; }
    public string? MimeType { get; private set; }
    public DateTime RegDate { get; private set; }

    /// <summary>Đường dẫn ảnh logo như trang cũ hiển thị.</summary>
    public string LogoPath => MyTrackingBrand.LogoFolder + ServerFileName;
}

internal sealed class MyTrackingBrandConfiguration : IEntityTypeConfiguration<MyTrackingBrand>
{
    public void Configure(EntityTypeBuilder<MyTrackingBrand> b)
    {
        // Cùng dòng dbo.TCustomer với CustomerLogin (table splitting) — chỉ cập nhật các cột dưới đây, không thêm / xóa khách.
        b.ToTable("TCustomer", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.CustomerId);
        b.HasOne<CustomerLogin>().WithOne().HasForeignKey<MyTrackingBrand>(x => x.CustomerId);
        b.Property(x => x.CustomerId).HasColumnName("CustomerID").ValueGeneratedNever();
        // Cột NOT NULL không chia sẻ với CustomerLogin: EF dựa vào đây để biết dòng có tồn tại khi mọi cột thương hiệu đều NULL
        // (thiếu thì khách chưa khai gì sẽ đọc ra null). Chỉ đọc — nhân viên phụ trách do CMS quản lý.
        b.Property<int>("Staff_ID").HasColumnName("Staff_ID");
        b.Property(x => x.Address).HasColumnName("Address1").HasMaxLength(250);
        b.Property(x => x.Phone).HasColumnName("Phone").HasMaxLength(150);
        b.Property(x => x.Website).HasColumnName("Bank").HasMaxLength(MyTrackingBrand.WebsiteMaxLength);
        b.Property(x => x.Color01).HasColumnName("Column1").HasMaxLength(MyTrackingBrand.ColorMaxLength);
        b.Property(x => x.Color02).HasColumnName("Column2").HasMaxLength(MyTrackingBrand.ColorMaxLength);
        b.Property(x => x.LogoFileId).HasColumnName("Logo_ShippingMark");
        foreach (var (_, column, maxLength) in MyTrackingBrand.ContactColumns)
            b.IndexerProperty<string?>(column).HasColumnName(column).HasMaxLength(maxLength);
    }
}

internal sealed class LegacyCommonFileConfiguration : IEntityTypeConfiguration<LegacyCommonFile>
{
    public void Configure(EntityTypeBuilder<LegacyCommonFile> b)
    {
        b.ToTable("TCommonFile", "dbo", t => t.LegacyTable());
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("CommonFileID").UseIdentityColumn();
        b.Property(x => x.RealFileName).HasMaxLength(150);
        b.Property(x => x.ServerFileName).HasMaxLength(150);
        b.Property(x => x.ServerFilePath).HasMaxLength(1000);
        b.Property(x => x.MimeType).HasColumnType("varchar(20)");
        b.Property(x => x.RegDate).HasColumnType("datetime");
        b.Ignore(x => x.LogoPath);
    }
}
