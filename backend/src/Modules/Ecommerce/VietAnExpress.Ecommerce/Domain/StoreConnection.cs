namespace VietAnExpress.Ecommerce.Domain;

/// <summary>
/// 1 shop của khách đã kết nối với 1 kênh bán (bảng <c>dbo.KetNoiTMDT</c>) — khớp <c>StoreConnection</c> trong API_CONTRACT.md §5.1.
/// Token của sàn lưu đã mã hóa, không bao giờ trả về frontend.
/// </summary>
internal sealed class StoreConnection
{
    public const int ShopIdMaxLength = 255;
    public const int ShopNameMaxLength = 200;
    public const int ShopDomainMaxLength = 255;
    public const int RegionMaxLength = 20;
    public const int CurrencyMaxLength = 3;
    public const int ScopesMaxLength = 1000;
    public const int WebhookIdsMaxLength = 2000;
    public const int StatusMaxLength = 20;
    public const int LastErrorMaxLength = 1000;

    /// <summary>Trạng thái — trùng <c>StoreConnection.status</c> của frontend.</summary>
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Failed = "error";
    public const string Revoked = "revoked";
    public static readonly string[] Statuses = [Active, Expired, Failed, Revoked];

    public long Id { get; private set; }
    /// <summary>dbo.TCustomer.CustomerID.</summary>
    public long CustomerId { get; private set; }
    /// <summary>dbo.KenhTMDT.Ma_Kenh.</summary>
    public string ChannelCode { get; private set; } = "";
    /// <summary>Định danh shop trên sàn: TikTok = shop_id, Amazon = seller id…</summary>
    public string ShopId { get; private set; } = "";
    public string ShopName { get; private set; } = "";
    /// <summary>Tên miền shop hiển thị (WooCommerce).</summary>
    public string? ShopDomain { get; private set; }
    /// <summary>TikTok Shop: shop_cipher bắt buộc khi gọi API theo shop.</summary>
    public string? ShopCipher { get; private set; }
    /// <summary>Vùng API: TikTok "global" / "us", Amazon "na" / "eu" / "fe"…</summary>
    public string? Region { get; private set; }
    /// <summary>Tiền tệ của shop (ISO 4217).</summary>
    public string? Currency { get; private set; }
    /// <summary>Quyền đã được cấp, cách nhau bởi dấu phẩy.</summary>
    public string? Scopes { get; private set; }
    public string? AccessTokenEncrypted { get; private set; }
    public DateTime? AccessTokenExpiresAt { get; private set; }
    public string? RefreshTokenEncrypted { get; private set; }
    public DateTime? RefreshTokenExpiresAt { get; private set; }
    /// <summary>Id webhook đã đăng ký trên sàn (JSON), để hủy khi ngắt kết nối.</summary>
    public string? WebhookIds { get; private set; }
    public string Status { get; private set; } = Active;
    public DateTime ConnectedAt { get; private set; }
    public DateTime? LastSyncAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }
    public DateTime? DisconnectedAt { get; private set; }
    public DateTime CreateDate { get; private set; }
    public DateTime? ModifyDate { get; private set; }

    private StoreConnection() { }

    public static StoreConnection Create(long customerId, string channelCode, string shopId, string shopName, DateTime now) =>
        new() { CustomerId = customerId, ChannelCode = channelCode, ShopId = shopId, ShopName = shopName, ConnectedAt = now, CreateDate = now };

    /// <summary>Đã kết nối mới / ủy quyền lại: lưu token (đã mã hóa), bật lại trạng thái active, xóa lỗi cũ.</summary>
    public void Authorize(StoreAuthorization a, DateTime now)
    {
        ShopName = a.ShopName;
        ShopDomain = a.ShopDomain;
        Currency = a.Currency;
        Scopes = a.Scopes;
        AccessTokenEncrypted = a.AccessTokenEncrypted;
        AccessTokenExpiresAt = a.AccessTokenExpiresAt;
        RefreshTokenEncrypted = a.RefreshTokenEncrypted;
        RefreshTokenExpiresAt = a.RefreshTokenExpiresAt;
        Status = Active;
        LastError = null;
        LastErrorAt = null;
        DisconnectedAt = null;
        ConnectedAt = now;
        ModifyDate = now;
    }

    /// <summary>Đã làm mới token bằng refresh token (sàn trả cặp mới thì bỏ cặp cũ).</summary>
    public void RefreshTokens(string accessTokenEncrypted, DateTime? accessExpiresAt, string? refreshTokenEncrypted, DateTime? refreshExpiresAt, DateTime now)
    {
        AccessTokenEncrypted = accessTokenEncrypted;
        AccessTokenExpiresAt = accessExpiresAt;
        if (refreshTokenEncrypted is not null)
        {
            RefreshTokenEncrypted = refreshTokenEncrypted;
            RefreshTokenExpiresAt = refreshExpiresAt;
        }
        ModifyDate = now;
    }

    public void MarkSynced(DateTime now)
    {
        Status = Active;
        LastSyncAt = now;
        LastError = null;
        LastErrorAt = null;
        ModifyDate = now;
    }

    public void MarkFailed(string error, DateTime now)
    {
        Status = Failed;
        LastError = error.Length <= LastErrorMaxLength ? error : error[..LastErrorMaxLength];
        LastErrorAt = now;
        ModifyDate = now;
    }

    /// <summary>Token hết hạn và không làm mới được — khách phải ủy quyền lại.</summary>
    public void MarkExpired(DateTime now)
    {
        Status = Expired;
        LastError = null;
        ModifyDate = now;
    }

    /// <summary>Khách ngắt kết nối: xóa token, ẩn khỏi danh sách (giữ dòng để đơn đã nhận vẫn truy được nguồn).</summary>
    public void Disconnect(DateTime now)
    {
        Status = Revoked;
        AccessTokenEncrypted = null;
        AccessTokenExpiresAt = null;
        RefreshTokenEncrypted = null;
        RefreshTokenExpiresAt = null;
        WebhookIds = null;
        DisconnectedAt = now;
        ModifyDate = now;
    }
}

/// <summary>Kết quả ủy quyền 1 shop, token đã mã hóa.</summary>
internal sealed record StoreAuthorization(
    string ShopName,
    string? ShopDomain,
    string? Currency,
    string? Scopes,
    string AccessTokenEncrypted,
    DateTime? AccessTokenExpiresAt,
    string? RefreshTokenEncrypted,
    DateTime? RefreshTokenExpiresAt);
