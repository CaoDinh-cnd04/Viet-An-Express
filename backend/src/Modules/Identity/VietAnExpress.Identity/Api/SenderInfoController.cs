using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>
/// Thông tin người gửi (hồ sơ công ty ở dbo.TCustomer) — chỉ tài khoản chính. Khách gửi yêu cầu sửa,
/// quản trị Việt An duyệt ở /admin thì mới ghi vào hồ sơ.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/account/sender-info")]
[Tags("Tài khoản")]
[HasPermission(IdentityPermissions.SenderInfo)]
internal sealed class SenderInfoController : ApiControllerBase
{
    /// <summary>Hồ sơ hiện tại + yêu cầu đang chờ duyệt + 10 yêu cầu gần nhất.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<SenderInfoOverviewDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) => FromResult(await Sender.Send(new GetSenderInfoQuery(), ct));

    /// <summary>Gửi yêu cầu sửa (bộ thông tin đầy đủ sau khi sửa + ghi chú). Mỗi lúc chỉ 1 yêu cầu chờ duyệt.</summary>
    [HttpPost("requests")]
    [ProducesResponseType<ApiResponse<SenderInfoRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(SenderInfoChangeRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new RequestSenderInfoChangeCommand(body), ct), "Đã gửi yêu cầu — Việt An sẽ duyệt và báo kết quả cho bạn");

    [HttpPost("requests/{id:long}/cancel")]
    [ProducesResponseType<ApiResponse<SenderInfoRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new CancelSenderInfoRequestCommand(id), ct), "Đã hủy yêu cầu");
}
