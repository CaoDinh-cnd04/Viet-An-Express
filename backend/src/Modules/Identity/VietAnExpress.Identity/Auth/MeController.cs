using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Admin;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.MyTracking;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Auth;

/// <summary>Thông tin khách đang đăng nhập — frontend gọi khi mở trang để biết đã đăng nhập chưa (401 = chưa).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/me")]
[Tags("Tài khoản")]
[Authorize]
internal sealed class MeController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) => FromResult(await Sender.Send(new GetSessionQuery(), ct));
}
