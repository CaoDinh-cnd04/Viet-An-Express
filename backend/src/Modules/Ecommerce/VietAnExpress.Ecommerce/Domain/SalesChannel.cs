namespace VietAnExpress.Ecommerce.Domain;

/// <summary>
/// Kênh bán / sàn TMĐT (bảng <c>dbo.KenhTMDT</c>). Danh mục cố định, seed trong migration;
/// thêm kênh mới = thêm 1 dòng (migration) rồi viết adapter của kênh, không đổi cấu trúc bảng.
/// </summary>
internal sealed class SalesChannel
{
    public const int CodeMaxLength = 30;
    public const int NameMaxLength = 100;
    public const int AuthTypeMaxLength = 20;
    public const int WebsiteMaxLength = 255;

    /// <summary>Cách ủy quyền: OAuth 2.0 (TikTok, Amazon, eBay, Etsy, Shopee, Lazada).</summary>
    public const string OAuth2 = "oauth2";
    /// <summary>Cách ủy quyền: khóa API do chủ shop tạo (WooCommerce REST API).</summary>
    public const string ApiKey = "api_key";

    private static readonly DateTime SeedDate = new(2026, 10, 5);

    public int Id { get; private set; }
    /// <summary>Mã kênh, xem <see cref="Contracts.SalesChannelCodes"/>.</summary>
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string AuthType { get; private set; } = OAuth2;
    public string? Website { get; private set; }
    /// <summary>Khách kết nối được kênh này trên portal (đã có adapter).</summary>
    public bool IsActive { get; private set; }
    /// <summary>Kênh có API đẩy mã tracking ngược lên đơn sàn.</summary>
    public bool SupportsTrackingPush { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreateDate { get; private set; }
    public DateTime? ModifyDate { get; private set; }

    private SalesChannel() { }

    /// <summary>Dòng seed trong migration — thứ tự hiển thị theo ID.</summary>
    public static SalesChannel Seed(int id, string code, string name, string authType, string website, bool isActive, bool supportsTrackingPush) =>
        new()
        {
            Id = id, Code = code, Name = name, AuthType = authType, Website = website,
            IsActive = isActive, SupportsTrackingPush = supportsTrackingPush, SortOrder = id, CreateDate = SeedDate
        };
}
