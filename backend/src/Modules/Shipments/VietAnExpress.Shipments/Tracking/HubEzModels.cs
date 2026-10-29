using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Tracking;

/// <summary>1 sự kiện hành trình HUB-EZ (giờ ghi đúng như HUB-EZ trả về).</summary>
internal sealed record HubEzEvent(DateTime? Time, string Description, string? Location, string? StatusCode);

/// <summary>Hành trình 1 mã HUB-EZ, mới nhất trước; <see cref="Signed"/> = đã giao.</summary>
internal sealed record HubEzTrack(string HawbNumber, IReadOnlyList<HubEzEvent> Events, bool Signed);
