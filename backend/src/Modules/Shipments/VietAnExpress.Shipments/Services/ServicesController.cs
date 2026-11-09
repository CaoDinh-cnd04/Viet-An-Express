using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Contracts;

namespace VietAnExpress.Shipments.Services;

/// <summary>Danh mục dịch vụ vận chuyển của hệ thống cũ (dbo.TGroupBaseCode / dbo.TBaseCode) — hợp đồng: web/docs/API_CONTRACT.md §3.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/services")]
[Tags("Tạo đơn")]
internal sealed class ServicesController : ApiControllerBase
{
    /// <summary>Hãng / dịch vụ → hub (tuyến), đúng chữ danh mục cũ. Rỗng = chưa đọc được danh mục (frontend dùng danh sách dự phòng).</summary>
    [HttpGet("hubs")]
    [HasPermission(ShipmentsPermissions.View)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ServiceCarrierDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Hubs(CancellationToken ct) => OkData(await Sender.Send(new GetServiceHubsQuery(), ct));
}
