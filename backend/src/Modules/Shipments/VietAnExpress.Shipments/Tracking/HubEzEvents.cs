using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>Ghép hành trình HUB-EZ vào hành trình của hệ thống cũ. Hàm thuần, có test.</summary>
internal static class HubEzEvents
{
    /// <summary>
    /// Thứ tự (mới nhất trước): sự kiện giao hàng của hệ thống cũ (POD) nếu có → hành trình HUB-EZ → các mốc còn lại của hệ thống cũ
    /// (rời kho, tạo vận đơn — luôn xảy ra trước khi hàng tới HUB-EZ).
    /// </summary>
    public static IReadOnlyList<OrderEventDto> Merge(IReadOnlyList<OrderEventDto> legacy, HubEzTrack? track, bool hasPod)
    {
        if (track is null || track.Events.Count == 0) return legacy;
        var carrier = track.Events.Select(e => new OrderEventDto(
            e.Time?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "", e.Description, e.Location));
        return hasPod && legacy.Count > 0
            ? [legacy[0], .. carrier, .. legacy.Skip(1)]
            : [.. carrier, .. legacy];
    }
}
