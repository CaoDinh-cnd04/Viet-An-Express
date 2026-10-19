namespace VietAnExpress.Identity.Application.Dtos;

/// <summary>
/// Khách đang đăng nhập — GET /me. Khớp <c>SessionUser</c> của frontend (web/src/features/auth/types.ts):
/// customerCode, companyName, contactName, email, avatarUrl, defaultBranch; kèm quyền để UI ẩn/hiện chức năng.
/// Tài khoản con (accountType = "staff", isAdmin = false): fullName / userName của nhân viên, quyền do admin chọn, hồ sơ công ty của khách cha.
/// Hồ sơ lấy từ dbo.TCustomer — companyName, contactName, phone, address, taxCode, email dùng để điền sẵn người gửi khi tạo đơn.
/// </summary>
internal sealed record SessionUserDto(
    long CustomerId,
    string UserName,
    string FullName,
    string AccountType,
    string CustomerCode,
    string CompanyName,
    string? ContactName,
    string? Email,
    string? AvatarUrl,
    string? DefaultBranch,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? Phone = null,
    string? Address = null,
    string? TaxCode = null,
    bool IsAdmin = false);

/// <summary>Loại tài khoản: "customer" = tài khoản chính của khách (admin), "staff" = tài khoản con của nhân viên.</summary>
internal static class AccountTypes
{
    public const string Customer = "customer";
    public const string Staff = "staff";
}

/// <summary>Kết quả đăng nhập / làm mới phiên — controller quyết định trả qua cookie hay body.</summary>
internal sealed record AuthSession(
    SessionUserDto User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    bool IsPersistent);

/// <summary>Tài khoản con của nhân viên — khớp <c>StaffAccount</c> của frontend (web/src/features/staff/types.ts).</summary>
internal sealed record StaffAccountDto(
    long Id,
    string UserName,
    string FullName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Permissions,
    bool Active,
    DateTime CreatedAt,
    string? Phone2,
    string? Email2);

/// <summary>1 quyền admin có thể cấp cho tài khoản con.</summary>
internal sealed record AssignablePermissionDto(string Code, string Description);

/// <summary>Cấu hình MyTracking của khách đang đăng nhập; <c>Config</c> null = chưa cấu hình (frontend dùng mặc định).</summary>
internal sealed record MyTrackingDto(string Slug, bool Published, RawJson? Config, DateTime? UpdatedAt);

/// <summary>Trang MyTracking công khai (đã xuất bản).</summary>
internal sealed record PublicMyTrackingDto(string Slug, RawJson Config);

/// <summary>
/// JSON đã lưu (đã kiểm tra khi ghi), trả nguyên văn vào phản hồi — không parse / clone lại cấu hình có thể tới 2 MB
/// mỗi lần người nhận mở trang /t/{slug}.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(RawJsonConverter))]
internal readonly record struct RawJson(string Json);

internal sealed class RawJsonConverter : System.Text.Json.Serialization.JsonConverter<RawJson>
{
    public override RawJson Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        using var doc = System.Text.Json.JsonDocument.ParseValue(ref reader);
        return new RawJson(doc.RootElement.GetRawText());
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, RawJson value, System.Text.Json.JsonSerializerOptions options) =>
        writer.WriteRawValue(value.Json, skipInputValidation: true);
}
