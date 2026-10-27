using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Application.Queries;

namespace VietAnExpress.Shipments.Api;

/// <summary>Tra cứu vận đơn trên trang ngoài — không cần đăng nhập, có giới hạn tần suất theo IP.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/public/tracking")]
[Tags("Công khai")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicTracking)]
internal sealed class PublicTrackingController : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PublicTrackResultDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Track(PublicTrackingRequest body, CancellationToken ct) =>
        OkData(await Sender.Send(new TrackShipmentsQuery(body.Bills ?? []), ct));

    /// <summary>Trang MyTracking của khách (/tracking/{mã khách}/...): chỉ tìm trong đơn của khách có mã này.</summary>
    [HttpPost("customers/{code}")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PublicTrackResultDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TrackForCustomer(string code, PublicTrackingRequest body, CancellationToken ct) =>
        OkData(await Sender.Send(new TrackShipmentsQuery(body.Bills ?? [], code), ct));
}

internal sealed record PublicTrackingRequest(IReadOnlyList<string>? Bills);
