using System.Text.Json.Serialization;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>
/// Kết quả tra cứu công khai — chỉ thông tin không nhạy cảm (không tên, địa chỉ, số điện thoại).
/// Dạng JSON khớp <c>TrackResult</c> của frontend (web/src/features/landing/types.ts).
/// </summary>
internal sealed record PublicTrackResultDto(
    string Bill,
    bool Found,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Status = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Destination = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Service = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PublicTrackEventDto>? Events = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Origin = null,
    // ngày gửi, ngày giao dự kiến: dd/MM/yyyy
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ShipDate = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EstimatedDate = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Pieces = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? WeightKg = null,
    // mã vận đơn của hãng (chỉ khi khác số VA)
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CarrierBill = null);

/// <param name="Time">Giờ Việt Nam, dd/MM/yyyy HH:mm.</param>
internal sealed record PublicTrackEventDto(string Time, string Title, string? Location);
