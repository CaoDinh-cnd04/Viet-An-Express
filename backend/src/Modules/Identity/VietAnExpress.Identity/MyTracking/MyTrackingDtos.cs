
namespace VietAnExpress.Identity.MyTracking;

/// <summary>Cấu hình MyTracking của khách đang đăng nhập; <c>Config</c> null = chưa cấu hình (frontend dùng mặc định).</summary>
/// <param name="CustomerCode">Mã khách trong link chính /tracking/{mã khách}/{số bill}.</param>
/// <param name="CompanyName">Tên công ty theo hồ sơ khách (TCustomer.InvoiceCustomerName, trống thì CustomerName) — trang MyTracking luôn dùng tên này, khách không sửa.</param>
internal sealed record MyTrackingDto(RawJson? Config, string CustomerCode, string CompanyName);

/// <summary>Trang MyTracking công khai (đã xuất bản).</summary>
/// <param name="CustomerCode">Mã khách chuẩn (đúng hoa thường trong TCustomer) để dựng lại link /tracking/{mã khách}/....</param>
internal sealed record PublicMyTrackingDto(RawJson Config, string CustomerCode);

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
