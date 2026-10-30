using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VietAnExpress.SharedKernel.Persistence;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>
/// Hành trình của hệ thống cũ cho 1 vận đơn đã khai thác: dòng <c>dbo.Orders</c> (cùng số bill) + <c>dbo.Order_Tracking</c>
/// (web cũ gọi API hãng theo <c>ConnectAWB</c> rồi lưu đệm JSON vào <c>TrackingData</c>) — trang tracking cũ hiện đúng các dữ liệu này.
/// </summary>
/// <param name="ShipDate">Orders.OrderDate — "Ship date" của trang cũ.</param>
/// <param name="Track">Sự kiện tách từ TrackingData (không tách được thì sự kiện cuối ở LastEvent_*); null = chưa có Order_Tracking.</param>
/// <param name="Destination">"Thành phố, Nước" như trang cũ (Orders.CONSIGNEE_CITY + Countries.CountryName).</param>
internal sealed record OrderTrack(string OrderNumber, DateTime ShipDate, HubEzTrack? Track, DateTime? EstimateDate, bool Delivered,
    long? CustomerId = null, string? Destination = null, string? Service = null);

internal interface IOrderTracking
{
    /// <summary>Theo số bill Việt An (OrderNumber, bỏ khoảng trắng); số không có trong dbo.Orders thì không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<string, OrderTrack>> FindAsync(IReadOnlyCollection<string> orderNumbers, CancellationToken cancellationToken);
}

/// <summary>Chỉ đọc 2 bảng cũ dbo.Orders + dbo.Order_Tracking (portal không ghi). Chưa có bảng / lỗi SQL → không có hành trình cũ.</summary>
internal sealed class OrderTrackingReader(ShipmentsDbContext db, ILogger<OrderTrackingReader> logger) : IOrderTracking
{
    public async Task<IReadOnlyDictionary<string, OrderTrack>> FindAsync(IReadOnlyCollection<string> orderNumbers, CancellationToken cancellationToken)
    {
        var numbers = orderNumbers.Select(n => n.Replace(" ", "", StringComparison.Ordinal).Trim())
            .Where(n => n.Length is > 0 and <= 50 && !n.Contains(',', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new Dictionary<string, OrderTrack>(StringComparer.OrdinalIgnoreCase);
        if (numbers.Count == 0 || !db.Database.IsSqlServer()) return result;
        var list = string.Join(',', numbers);
        List<OrderTrackingRow> rows;
        try
        {
            // Câu lệnh có IF nên EF chạy nguyên văn; database chưa có 2 bảng cũ → không có dòng.
            rows = await db.Database.SqlQuery<OrderTrackingRow>($"""
                IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Order_Tracking', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.Countries', N'U') IS NOT NULL
                    SELECT REPLACE(O.OrderNumber, ' ', '') AS OrderNumber, O.OrderDate, CAST(O.CustomerID AS bigint) AS CustomerId,
                           O.CONSIGNEE_CITY AS City, C.CountryName, T.Hang AS Carrier, T.TrackingData, T.StatusString,
                           T.LastEvent_Date AS LastEventDate, T.LastEvent_Content AS LastEventContent, T.LastEvent_Location AS LastEventLocation,
                           T.EstmateDate AS EstimateDate, T.DeleveryDate AS DeliveryDate, CAST(CASE WHEN T.OrderID IS NULL THEN 0 ELSE 1 END AS bit) AS HasTracking
                    FROM dbo.Orders O
                    LEFT JOIN dbo.Order_Tracking T ON T.OrderID = O.OrderID
                    LEFT JOIN dbo.Countries C ON C.CountryID = O.CountryID
                    WHERE REPLACE(O.OrderNumber, ' ', '') IN (SELECT value FROM STRING_SPLIT({list}, ','))
                    ORDER BY O.OrderDate DESC;
                ELSE
                    SELECT CAST(NULL AS nvarchar(50)) AS OrderNumber, CAST(NULL AS datetime) AS OrderDate, CAST(NULL AS bigint) AS CustomerId,
                           CAST(NULL AS nvarchar(250)) AS City, CAST(NULL AS nvarchar(250)) AS CountryName, CAST(NULL AS nvarchar(50)) AS Carrier,
                           CAST(NULL AS nvarchar(max)) AS TrackingData,
                           CAST(NULL AS nvarchar(50)) AS StatusString, CAST(NULL AS datetime) AS LastEventDate, CAST(NULL AS nvarchar(500)) AS LastEventContent,
                           CAST(NULL AS nvarchar(500)) AS LastEventLocation, CAST(NULL AS datetime) AS EstimateDate, CAST(NULL AS datetime) AS DeliveryDate,
                           CAST(0 AS bit) AS HasTracking
                    WHERE 1 = 0;
                """).ToListAsync(cancellationToken);
        }
        catch (Exception e) when (e is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            logger.LogWarning(e, "Không đọc được dbo.Orders / dbo.Order_Tracking");
            return result;
        }
        List<DomesticEventRow> domestic = rows.Count == 0 ? [] : await DomesticAsync(list, cancellationToken);
        // Trùng số bill: lấy dòng mới nhất (đã sắp OrderDate giảm dần).
        foreach (var row in rows)
            if (row.OrderNumber is { Length: > 0 } number && row.OrderDate is { } shipDate && !result.ContainsKey(number))
            {
                // Hàng nội địa / chuyên tuyến: hành trình ở Order_Tracking_NoiDong (trang cũ hiện thay cho hành trình hãng).
                var events = OrderTrackingData.Domestic(domestic.Where(d => string.Equals(d.OrderNumber, number, StringComparison.OrdinalIgnoreCase)));
                var track = events.Count > 0 ? new HubEzTrack(number, events, false)
                    : row.HasTracking ? OrderTrackingData.ToTrack(number, row) : null;
                result[number] = new OrderTrack(number, shipDate, track,
                    row.EstimateDate, row.DeliveryDate is not null || OrderTrackingData.IsDelivered(row.StatusString),
                    row.CustomerId, OrderTrackingData.Destination(row.City, row.CountryName), OrderTrackingData.Blank(row.Carrier));
            }
        return result;
    }

    /// <summary>
    /// Hành trình nội địa / chuyên tuyến web cũ đã sinh (proc p_Orders_Tracking_NoiDong_New ghi Order_Tracking_NoiDong theo mẫu Carriers_NoiDong
    /// mỗi khi có người tra ở trang cũ). Chỉ đọc như phần kết quả của proc: sự kiện đã tới ngày, ngày = ngày gửi + SortOrder, giờ = cột Time.
    /// </summary>
    private async Task<List<DomesticEventRow>> DomesticAsync(string list, CancellationToken ct)
    {
        try
        {
            return await db.Database.SqlQuery<DomesticEventRow>($"""
                IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Order_Tracking_NoiDong', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.Carriers_NoiDong', N'U') IS NOT NULL
                    SELECT REPLACE(O.OrderNumber, ' ', '') AS OrderNumber, CAST(DATEADD(DAY, C.SortOrder, O.OrderDate) AS date) AS EventDate,
                           B.Time, B.Status, B.Local AS Location
                    FROM dbo.Orders O
                    INNER JOIN dbo.Order_Tracking_NoiDong B ON B.OrderID = O.OrderID
                    INNER JOIN dbo.Carriers_NoiDong C ON C.ID = B.ID_Ref
                    WHERE REPLACE(O.OrderNumber, ' ', '') IN (SELECT value FROM STRING_SPLIT({list}, ','))
                      AND DATEADD(DAY, C.SortOrder, O.OrderDate) <= GETDATE();
                ELSE
                    SELECT CAST(NULL AS nvarchar(50)) AS OrderNumber, CAST(NULL AS date) AS EventDate, CAST(NULL AS nvarchar(50)) AS Time,
                           CAST(NULL AS nvarchar(250)) AS Status, CAST(NULL AS nvarchar(250)) AS Location
                    WHERE 1 = 0;
                """).ToListAsync(ct);
        }
        catch (Exception e) when (e is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            logger.LogWarning(e, "Không đọc được dbo.Order_Tracking_NoiDong");
            return [];
        }
    }
}

internal sealed class DomesticEventRow
{
    public string? OrderNumber { get; set; }
    public DateTime? EventDate { get; set; }
    public string? Time { get; set; }
    public string? Status { get; set; }
    public string? Location { get; set; }
}

internal sealed class OrderTrackingRow
{
    public string? OrderNumber { get; set; }
    public DateTime? OrderDate { get; set; }
    public long? CustomerId { get; set; }
    public string? City { get; set; }
    public string? CountryName { get; set; }
    public string? Carrier { get; set; }
    public string? TrackingData { get; set; }
    public string? StatusString { get; set; }
    public DateTime? LastEventDate { get; set; }
    public string? LastEventContent { get; set; }
    public string? LastEventLocation { get; set; }
    public DateTime? EstimateDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public bool HasTracking { get; set; }
}

/// <summary>
/// Tách sự kiện từ <c>Order_Tracking.TrackingData</c> — JSON trả về từ API của từng hãng (DHL, UPS, FedEx, dịch vụ tổng hợp…),
/// mỗi hãng một dạng nên đọc linh hoạt: mảng đối tượng có thời gian + nội dung (+ địa điểm) dài nhất trong JSON là danh sách sự kiện.
/// Hàm thuần — có test.
/// </summary>
internal static class OrderTrackingData
{
    private static readonly string[] DateTimeKeys =
        ["datetime", "timestamp", "checkpointtime", "eventtime", "eventdatetime", "localdatetime", "datetimelocal", "occurredat", "createdat", "trackingtime", "time"];
    private static readonly string[] DateKeys = ["date", "eventdate", "localdate", "activitydate", "statusdate"];
    private static readonly string[] TimeKeys = ["time", "eventtime", "localtime", "activitytime", "statustime"];
    private static readonly string[] TextKeys =
        ["description", "message", "activity", "statusdescription", "eventdescription", "details", "detail", "content", "context", "checkpointstatus", "event", "status", "remark", "remarks", "info"];
    private static readonly string[] PlaceKeys =
        ["location", "city", "place", "address", "area", "areaname", "facility", "servicearea", "checkpointlocation", "countryname", "country"];

    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm",
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd-MM-yyyy HH:mm", "yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd HHmmss", "yyyyMMdd HHmm",
        "yyyy-MM-dd", "dd/MM/yyyy", "yyyyMMdd"
    ];

    public static bool IsDelivered(string? status) => status?.Contains("deliver", StringComparison.OrdinalIgnoreCase) == true
        && !status.Contains("undeliver", StringComparison.OrdinalIgnoreCase) && !status.Contains("out", StringComparison.OrdinalIgnoreCase);

    public static HubEzTrack ToTrack(string number, OrderTrackingRow row)
    {
        var events = Parse(row.TrackingData);
        if (events.Count == 0 && !string.IsNullOrWhiteSpace(row.LastEventContent))
            events = [new HubEzEvent(row.LastEventDate, row.LastEventContent.Trim(), Blank(row.LastEventLocation), null)];
        return new HubEzTrack(number, events, row.DeliveryDate is not null || IsDelivered(row.StatusString));
    }

    /// <summary>Sự kiện mới nhất trước; JSON lỗi / không nhận ra → rỗng.</summary>
    public static IReadOnlyList<HubEzEvent> Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return [];
        JsonNode? root;
        try { root = JsonNode.Parse(data); }
        catch (JsonException) { return []; }
        List<HubEzEvent> best = [];
        foreach (var array in Arrays(root))
        {
            var events = array.OfType<JsonObject>().Select(Event).OfType<HubEzEvent>().ToList();
            if (events.Count > best.Count) best = events;
        }
        return best.OrderByDescending(e => e.Time ?? DateTime.MinValue).ToList();
    }

    private static IEnumerable<JsonArray> Arrays(JsonNode? node)
    {
        switch (node)
        {
            case JsonArray array:
                yield return array;
                foreach (var item in array) foreach (var inner in Arrays(item)) yield return inner;
                break;
            case JsonObject obj:
                foreach (var (_, value) in obj) foreach (var inner in Arrays(value)) yield return inner;
                break;
        }
    }

    private static HubEzEvent? Event(JsonObject obj)
    {
        var fields = obj.Where(p => p.Value is not null).GroupBy(p => Key(p.Key)).ToDictionary(g => g.Key, g => g.First().Value!);
        var text = First(fields, TextKeys);
        var time = Time(fields);
        if (string.IsNullOrWhiteSpace(text) || time is null) return null;
        return new HubEzEvent(time, text.Trim(), Blank(First(fields, PlaceKeys)), null);
    }

    private static DateTime? Time(Dictionary<string, JsonNode> fields)
    {
        // Ngày và giờ ở 2 trường riêng ("date": "2026-10-22", "time": "11:35") → ghép.
        var date = First(fields, DateKeys);
        var time = First(fields, TimeKeys);
        if (date is not null && time is not null && ParseTime($"{date} {time}") is { } combined) return combined;
        foreach (var key in DateTimeKeys)
            if (fields.TryGetValue(key, out var node) && Text(node) is { } value && HasDate(value) && ParseTime(value) is { } parsed) return parsed;
        return date is null ? null : ParseTime(date);
    }

    /// <summary>Có phần ngày (năm 4 chữ số) — "11:35" một mình không phải thời điểm (TryParse sẽ tự gán ngày hôm nay).</summary>
    private static bool HasDate(string value) => value.Count(char.IsDigit) >= 8 || System.Text.RegularExpressions.Regex.IsMatch(value, @"\d{4}");

    /// <summary>Giữ giờ địa phương hãng ghi (như trang cũ hiện), không đổi múi giờ.</summary>
    private static DateTime? ParseTime(string value)
    {
        value = value.Trim();
        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact)) return exact;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var offset)) return offset.DateTime;
        return null;
    }

    private static string? First(Dictionary<string, JsonNode> fields, string[] keys)
    {
        foreach (var key in keys)
            if (fields.TryGetValue(key, out var node) && Text(node) is { Length: > 0 } value) return value;
        return null;
    }

    /// <summary>Chuỗi / số; đối tượng (vd địa điểm {"city": "HANOI", "country": "VN"}) → nối các giá trị chữ.</summary>
    private static string? Text(JsonNode node) => node switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => Blank(s),
        JsonValue v => Blank(v.ToJsonString()),
        JsonObject o => Blank(string.Join(", ", Leaves(o).Distinct(StringComparer.OrdinalIgnoreCase))),
        _ => null
    };

    private static IEnumerable<string> Leaves(JsonObject obj)
    {
        foreach (var (_, value) in obj)
        {
            if (value is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) yield return s.Trim();
            else if (value is JsonObject inner) foreach (var leaf in Leaves(inner)) yield return leaf;
        }
    }

    private static string Key(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>Sự kiện nội địa / chuyên tuyến: ngày (ngày gửi + SortOrder) ghép giờ "HH:mm" của cột Time; mới nhất trước.</summary>
    public static IReadOnlyList<HubEzEvent> Domestic(IEnumerable<DomesticEventRow> rows) =>
        rows.Where(r => r.EventDate is not null && !string.IsNullOrWhiteSpace(r.Status))
            .Select(r => new HubEzEvent(
                TimeSpan.TryParseExact(r.Time?.Trim(), [@"hh\:mm", @"h\:mm", @"hh\:mm\:ss"], CultureInfo.InvariantCulture, out var time)
                    ? r.EventDate!.Value.Date + time : r.EventDate!.Value.Date,
                r.Status!.Trim(), Blank(r.Location), null))
            .OrderByDescending(e => e.Time)
            .ToList();

    public static string? Destination(string? city, string? country)
    {
        var parts = new[] { city, country }.Select(Blank).OfType<string>().ToList();
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
