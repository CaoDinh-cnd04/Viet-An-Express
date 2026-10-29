using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>Hub nào đi HUB-EZ — so cột Dich_Vu ("Chuyên tuyến|EU") với cấu hình dạng ô Hub ("Chuyên tuyến - EU"). Hàm thuần, có test.</summary>
internal static class HubEzHubs
{
    public static bool Matches(string? serviceName, string rules)
    {
        var service = Normalize((serviceName ?? "").Replace("|", " - ", StringComparison.Ordinal));
        if (service.Length == 0) return false;
        foreach (var raw in rules.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var rule = Normalize(raw);
            if (rule.EndsWith(" - *", StringComparison.Ordinal)
                ? service.StartsWith(rule[..^1], StringComparison.OrdinalIgnoreCase)
                : string.Equals(service, rule, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>Gộp khoảng trắng thừa ("Chuyên tuyến  -  EU" = "Chuyên tuyến - EU").</summary>
    private static string Normalize(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
}
