using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>Đọc JSON HUB-EZ (khung ABP: { result, success, error, unAuthorizedRequest }). Hàm thuần, có test.</summary>
internal static class HubEzParser
{
    private static readonly string[] TimeFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss.FFFFFFF"];

    public static bool IsUnauthorized(JsonDocument doc) =>
        doc.RootElement.ValueKind == JsonValueKind.Object
        && doc.RootElement.TryGetProperty("unAuthorizedRequest", out var u) && u.ValueKind == JsonValueKind.True;

    /// <summary>{ result: "&lt;token&gt;", success: true }.</summary>
    public static string? Token(JsonDocument doc) =>
        doc.RootElement.ValueKind == JsonValueKind.Object
        && doc.RootElement.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True
        && doc.RootElement.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String
        && r.GetString() is { Length: > 0 } token ? token : null;

    /// <summary>
    /// { result: [ { hawbNumber, result, resultMsg, signed, searchHawbForTrackList: [ { desc, eventTime, statusCode, locationName } ] } ] }.
    /// Bỏ mã HUB-EZ không có (result = false); sự kiện xếp mới nhất trước.
    /// </summary>
    public static IReadOnlyList<HubEzTrack> Tracks(JsonDocument doc)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("result", out var list)
            || list.ValueKind != JsonValueKind.Array) return [];
        var tracks = new List<HubEzTrack>();
        foreach (var t in list.EnumerateArray())
        {
            if (t.ValueKind != JsonValueKind.Object || Str(t, "hawbNumber") is not { } hawb) continue;
            if (t.TryGetProperty("result", out var found) && found.ValueKind == JsonValueKind.False) continue;
            var events = new List<HubEzEvent>();
            if (t.TryGetProperty("searchHawbForTrackList", out var evs) && evs.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in evs.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object || Str(e, "desc") is not { } desc) continue;
                    events.Add(new HubEzEvent(Time(Str(e, "eventTime")), desc, Str(e, "locationName"), Str(e, "statusCode")));
                }
            }
            var signed = t.TryGetProperty("signed", out var s) && s.ValueKind == JsonValueKind.True;
            tracks.Add(new HubEzTrack(hawb, [.. events.OrderByDescending(e => e.Time ?? DateTime.MinValue)], signed));
        }
        return tracks;
    }

    private static DateTime? Time(string? value) =>
        value is not null && DateTime.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
}
