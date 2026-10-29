using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Catalog;
using VietAnExpress.Shipments.Contracts;
using VietAnExpress.Shipments.Import;
using VietAnExpress.Shipments.OrderData;
using VietAnExpress.Shipments.Orders;

namespace VietAnExpress.Shipments.Drafts;

/// <summary>Đơn nháp &amp; chưa in — hợp đồng frontend: web/docs/API_CONTRACT.md §2.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/drafts")]
[Tags("Đơn hàng")]
internal sealed class DraftsController : ApiControllerBase
{
    [HttpGet]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DraftDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => OkData(await Sender.Send(new GetDraftsQuery(), ct));

    [HttpPost]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<DraftDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(DraftInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new SaveDraftCommand(null, body), ct),
            body.Stt == DraftInput.StatusReady ? "Đã lưu đơn, chờ in" : "Đã lưu nháp");

    [HttpPut("{id:long}")]
    [HasPermission(ShipmentsPermissions.Update)]
    [ProducesResponseType<ApiResponse<DraftDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, DraftInput body, CancellationToken ct) =>
        FromResult(await Sender.Send(new SaveDraftCommand(id, body), ct), "Đã cập nhật đơn nháp");

    [HttpDelete("{id:long}")]
    [HasPermission(ShipmentsPermissions.Update)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct) =>
        FromResult(await Sender.Send(new DeleteDraftCommand(id), ct), "Đã xoá đơn nháp");

    /// <summary>
    /// In &amp; cấp bill: cấp số vận đơn cho dòng dbo.MaVanDon chưa in (Status 0 → 1).
    /// Khách phải đồng ý Điều khoản dịch vụ (body <c>{ acceptTerms: true }</c>), thiếu → 422 TERMS_NOT_ACCEPTED.
    /// </summary>
    [HttpPost("{id:long}/print")]
    [HasPermission(ShipmentsPermissions.IssueBill)]
    [ProducesResponseType<PrintDraftResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Print(
        long id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] PrintDraftRequest? body, CancellationToken ct)
    {
        if (body?.AcceptTerms != true) return Problem(OrderErrors.TermsNotAccepted);
        var result = await Sender.Send(new PrintDraftCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}

/// <param name="AcceptTerms">Khách đã tick đồng ý Điều khoản dịch vụ.</param>
/// <param name="TermsVersion">Phiên bản điều khoản khách đã đọc, vd "ĐKDV-2026.11".</param>
internal sealed record PrintDraftRequest(bool AcceptTerms, string? TermsVersion);
