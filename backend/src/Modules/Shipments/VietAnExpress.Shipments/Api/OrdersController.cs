using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Application.Dtos;
using VietAnExpress.Shipments.Application.Orders;
using VietAnExpress.Shipments.Application.Orders.Import;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Shipments.Api;

/// <summary>
/// "Đơn hàng của tôi" — đọc / ghi bảng vận đơn hệ thống cũ dbo.MaVanDon.
/// Dạng dữ liệu theo hợp đồng frontend: web/docs/API_CONTRACT.md §1.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/orders")]
[Tags("Đơn hàng")]
internal sealed class OrdersController : ApiControllerBase
{
    /// <summary>Toàn bộ form để copy đơn; chỉ đọc, giữ phạm vi khách/tài khoản con của đơn gốc.</summary>
    [HttpGet("{bill}/copy")]
    [HasPermission(ShipmentsPermissions.View)]
    [HasPermission(ShipmentsPermissions.Create)]
    public async Task<IActionResult> Copy(string bill, CancellationToken ct) => FromResult(await Sender.Send(new CopyOrderQuery(bill), ct));

    [HttpGet]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<OrderListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? q, [FromQuery] string? searchField, [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal? weightFrom, [FromQuery] decimal? weightTo,
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? sortBy, [FromQuery] string? sortDir,
        CancellationToken ct) =>
        Ok(await Sender.Send(new GetOrdersQuery(q, searchField, status, type, fromDate, toDate, weightFrom, weightTo,
            page, pageSize, sortBy, sortDir), ct));

    /// <summary>
    /// Trang in chứng từ (HTML, tự mở hộp thoại in): doc = bill-a4 | invoice | cvck | label-a6, hoặc nhiều loại
    /// ngăn bằng dấu phẩy (trang gộp, in lần lượt từng loại);
    /// bills = danh sách số vận đơn ngăn bằng dấu phẩy (tối đa 100).
    /// </summary>
    [HttpGet("print")]
    [HasPermission(ShipmentsPermissions.View)]
    [Produces("text/html")]
    public async Task<IActionResult> Print([FromQuery] string? bills, [FromQuery] string? doc, CancellationToken ct)
    {
        var list = (bills ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = await Sender.Send(new PrintOrdersQuery(list, doc ?? ""), ct);
        return result.IsSuccess ? Content(result.Value, "text/html; charset=utf-8") : Problem(result.Error);
    }

    /// <summary>Xuất bảng kê gửi hàng (.xlsx) theo cùng bộ lọc với danh sách đơn.</summary>
    [HttpGet("export")]
    [HasPermission(ShipmentsPermissions.View)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q, [FromQuery] string? searchField, [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal? weightFrom, [FromQuery] decimal? weightTo,
        [FromQuery] string? sortBy, [FromQuery] string? sortDir, CancellationToken ct)
    {
        var file = await Sender.Send(new ExportOrdersQuery(
            new GetOrdersQuery(q, searchField, status, type, fromDate, toDate, weightFrom, weightTo, null, null, sortBy, sortDir)), ct);
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }

    /// <summary>Người gửi đã dùng ở đơn trước, tên công ty / người liên hệ chứa <c>q</c> — gợi ý khi tài khoản con khai người gửi.</summary>
    [HttpGet("senders")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecentSenderDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Senders([FromQuery] string? q, CancellationToken ct) =>
        OkData(await Sender.Send(new GetRecentSendersQuery(q), ct));

    /// <summary>
    /// Người nhận đã gửi trước đây (mới nhất trước): tên công ty chứa <c>q</c> (tối đa 20) — hoặc không có <c>q</c> thì
    /// là sổ địa chỉ (100 người nhận gần nhất).
    /// </summary>
    [HttpGet("receivers")]
    [HasPermission(ShipmentsPermissions.Create)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RecentReceiverDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Receivers([FromQuery] string? q, CancellationToken ct) =>
        OkData(await Sender.Send(new GetRecentReceiversQuery(q), ct));

    /// <summary>Chi tiết 1 đơn theo số VA hoặc mã hãng.</summary>
    [HttpGet("{bill}")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string bill, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetOrderQuery(bill), ct));

    [HttpGet("{bill}/events")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<OrderEventDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Events(string bill, CancellationToken ct) =>
        FromResult(await Sender.Send(new GetOrderEventsQuery(bill), ct));

    /// <summary>
    /// Kiểm tra file Excel tạo đơn (mẫu Mau_Excel_Tao_Don.xlsx) — trả từng dòng kèm lỗi / cảnh báo, chưa tạo đơn.
    /// Form: file (.xlsx), service, hub (tuỳ chọn — đã chọn service thì bắt buộc hub).
    /// </summary>
    [HttpPost("import/preview")]
    [HasPermission(ShipmentsPermissions.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportOrdersCommand.MaxBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<ImportResultDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ImportPreview([FromForm] ImportForm form, CancellationToken ct) => Import(form, commit: false, ct);

    /// <summary>Tạo đơn cho các dòng hợp lệ của file (≤ 100 dòng) — ghi dbo.MaVanDon, cấp số vận đơn ngay; dòng lỗi bị bỏ qua.</summary>
    [HttpPost("import")]
    [HasPermission(ShipmentsPermissions.Create)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportOrdersCommand.MaxBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<ImportResultDto>>(StatusCodes.Status200OK)]
    public Task<IActionResult> ImportCommit([FromForm] ImportForm form, CancellationToken ct) => Import(form, commit: true, ct);

    private async Task<IActionResult> Import(ImportForm form, bool commit, CancellationToken ct)
    {
        if (form.File is not { Length: > 0 } file) return Problem(ImportErrors.Empty);
        await using var stream = file.OpenReadStream();
        var result = await Sender.Send(
            new ImportOrdersCommand(stream, file.FileName, file.Length, form.Service, form.Hub, form.Branch, commit), ct);
        var message = !result.IsSuccess ? null
            : commit ? $"Đã tạo {result.Value.Created} đơn"
            : $"{result.Value.Valid}/{result.Value.Total} dòng hợp lệ";
        return FromResult(result, message);
    }
}

/// <summary>Form tải file tạo đơn từ Excel.</summary>
internal sealed class ImportForm
{
    public IFormFile? File { get; set; }
    public string? Service { get; set; }
    public string? Hub { get; set; }
    public string? Branch { get; set; }
}

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
