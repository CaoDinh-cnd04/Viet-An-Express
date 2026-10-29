using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.MyTracking;

/// <summary>Cấu hình trang MyTracking của khách — chỉ tài khoản chính (admin).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/account/mytracking")]
[Tags("Tài khoản")]
[HasPermission(IdentityPermissions.MyTracking)]
internal sealed class MyTrackingController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<MyTrackingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) => FromResult(await Sender.Send(new GetMyTrackingQuery(), ct));

    [HttpPut]
    [RequestSizeLimit(MyTrackingPage.ConfigMaxBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<MyTrackingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Save(SaveMyTrackingRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new SaveMyTrackingCommand(body.Config), ct);
        return FromResult(result, "Đã lưu cấu hình MyTracking");
    }
}

/// <summary>
/// Trang MyTracking công khai /tracking/{mã khách}/... — không cần đăng nhập; tra cứu dùng POST /public/tracking/customers/{mã khách}.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/public/mytracking")]
[Tags("Công khai")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.PublicTracking)]
internal sealed class PublicMyTrackingController : ApiControllerBase
{
    [HttpGet("images/{folder}/{name}")]
    [DisableRateLimiting]
    public IActionResult Image(string folder, string name, [FromServices] MyTrackingImageStore images)
    {
        if (images.Find(folder, name) is not { } image) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return PhysicalFile(image.Path, image.ContentType);
    }

    /// <summary>Trang đã xuất bản theo mã khách — link /tracking/{mã khách}/{số bill}?ref=...</summary>
    [HttpGet("customers/{code}")]
    [ProducesResponseType<ApiResponse<PublicMyTrackingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCustomer(string code, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetCustomerMyTrackingQuery(code), ct));
}

internal sealed record SaveMyTrackingRequest(JsonElement Config);
