using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Support;

/// <summary>Form "Gửi lời nhắn" ở trang chủ — không cần đăng nhập, giới hạn 3 lần / phút / IP.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/public/contact")]
[Tags("Công khai")]
internal sealed class PublicContactController : ApiControllerBase
{
    /// <summary><c>{ name, phone, email, message }</c> (<c>email</c> có thể rỗng) → <c>{ message }</c>.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicContact)]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(PublicContactCommand body, CancellationToken ct)
    {
        var result = await Sender.Send(body, ct);
        return result.IsSuccess ? Ok(new ApiMessage(result.Value)) : Problem(result.Error);
    }
}
