using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>Sự cố vận đơn của khách: báo sự cố, xem lịch sử trao đổi, gửi thêm thông tin / tự đóng. Việt An xử lý ở /admin.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/troubles")]
[Tags("Sự cố")]
internal sealed class TroublesController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(IdentityPermissions.Troubles)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TroubleDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => FromResult(await Sender.Send(new GetTroublesQuery(), ct));

    [HttpPost]
    [HasPermission(IdentityPermissions.Troubles)]
    [ProducesResponseType<ApiResponse<TroubleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(NewTroubleRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new CreateTroubleCommand(body), ct);
        return FromResult(result, result.IsSuccess ? $"Đã gửi sự cố {result.Value.Id} — CS Việt An sẽ phản hồi sớm" : null);
    }

    /// <summary><c>{ reply, status? }</c> — <c>status: "done"</c> = khách xác nhận đã xử lý xong.</summary>
    [HttpPost("{id}/reply")]
    [HasPermission(IdentityPermissions.Troubles)]
    [ProducesResponseType<ApiResponse<TroubleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reply(string id, TroubleReplyRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new ReplyTroubleCommand(id, body.Reply, body.Status), ct),
            body.Status == "done" ? "Đã đóng sự cố" : "Đã gửi thông tin cho CS Việt An");
}

internal sealed record TroubleReplyRequest(string? Reply, string? Status);
