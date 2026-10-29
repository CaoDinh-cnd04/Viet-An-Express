using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VietAnExpress.Shipments.Shared;

namespace VietAnExpress.Shipments.Geo;

/// <summary>1 hãng tính phụ phí vùng sâu vùng xa (VSVX / ODA) ở địa chỉ người nhận. Tier = mức của FedEx (Tier A/B/C), null ở hãng khác.</summary>
internal sealed record RemoteAreaHit(string Carrier, string? Tier);

/// <summary>1 dòng bảng dbo.VungXauVungXa (bảng hệ thống cũ, hàm Load_FEDEX_Remote_Area tra bảng này).</summary>
internal sealed record RemoteAreaRow(string? Carrier, string? City, string? BeginPostal, string? EndPostal, string? ToParcel);
