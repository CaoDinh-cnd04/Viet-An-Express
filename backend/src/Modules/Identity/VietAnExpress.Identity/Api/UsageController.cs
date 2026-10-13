using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

[ApiVersion(1)]
[Route("api/v{version:apiVersion}")]
internal sealed class UsageController : ApiControllerBase
{
    [HttpPost("usage/events")]
    [AllowAnonymous]
    [EnableRateLimiting("usage")]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Track(TrackUsageCommand body, CancellationToken ct) => FromResult(await Sender.Send(body, ct));

    [HttpGet("admin/analytics")]
    [HasPermission(IdentityPermissions.AdminAnalytics)]
    public async Task<IActionResult> Report([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string audience = "all", CancellationToken ct = default) =>
        FromResult(await Sender.Send(new UsageReportQuery(from, to, audience), ct));
}
