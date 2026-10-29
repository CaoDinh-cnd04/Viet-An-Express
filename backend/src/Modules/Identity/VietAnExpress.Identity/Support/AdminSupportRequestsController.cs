using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Support;

/// <summary>Trang quản trị Việt An: xử lý yêu cầu hỗ trợ &amp; góp ý của khách (dbo.TContact).</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/admin")]
[Tags("Quản trị")]
internal sealed class AdminSupportRequestsController : ApiControllerBase
{
    /// <summary>
    /// Yêu cầu hỗ trợ &amp; sự cố (300 cập nhật gần nhất) kèm nhân viên phụ trách khách.
    /// <c>?staffId=</c> (USERS.ID) chỉ lấy khách của nhân viên đó — trang /admin xem tất cả; CMS dùng cho nhân viên thường.
    /// </summary>
    [HttpGet("troubles")]
    [HasPermission(IdentityPermissions.AdminSupportRequests)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AdminSupportRequestDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SupportRequests([FromQuery] int? staffId, CancellationToken ct) =>
        OkData(await Sender.Send(new GetAdminSupportRequestsQuery(staffId), ct));

    /// <summary>
    /// <c>{ reply?, status? }</c> (ít nhất 1) — trả lời (nối vào TContact.Content) và / hoặc đổi trạng thái. Không gửi thông báo:
    /// khách xem ở trang Hỗ trợ &amp; góp ý; "Chờ khách phản hồi" thì khách trả lời lại được.
    /// </summary>
    [HttpPost("troubles/{id}/reply")]
    [HasPermission(IdentityPermissions.AdminSupportRequests)]
    [ProducesResponseType<ApiResponse<AdminSupportRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplySupportRequest(string id, SupportReplyRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new ReplySupportRequestCommand(id, body.Reply, body.Status), ct), "Đã cập nhật yêu cầu");
}
