using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.SharedKernel.Web;
using VietAnExpress.Shipments.Drafts;
using VietAnExpress.Shipments.Import;
using VietAnExpress.Shipments.Orders;
using VietAnExpress.Shipments.Printing;
using VietAnExpress.Shipments.Tracking;

namespace VietAnExpress.Shipments.Geo;

/// <summary>Tra cứu địa lý khi khai địa chỉ người nhận: quốc gia + mã điện thoại, mã bưu chính → thành phố / tỉnh bang, gợi ý địa chỉ.</summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/geo")]
[Tags("Địa lý")]
[Authorize]
internal sealed class GeoController(IGeoLookup geo, IRemoteAreaLookup remoteAreas) : ApiControllerBase
{
    /// <summary>Danh sách quốc gia (tên tiếng Anh, mã ISO, mã điện thoại).</summary>
    [HttpGet("countries")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CountryInfo>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Countries(CancellationToken ct) => OkData(await geo.GetCountriesAsync(ct));

    /// <summary>Mã bưu chính → thành phố, tỉnh / bang. 404 khi không tìm thấy hoặc nước chưa hỗ trợ.</summary>
    [HttpGet("postal/{countryCode}/{postalCode}")]
    [ProducesResponseType<ApiResponse<PostalInfo>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Postal(string countryCode, string postalCode, CancellationToken ct) =>
        await geo.LookupPostalAsync(countryCode, postalCode, ct) is { } info
            ? OkData(info)
            : Problem(Error.NotFound("POSTAL_NOT_FOUND", "Không tìm thấy mã bưu chính"));

    /// <summary>Gợi ý mã bưu chính khi khách đang gõ (<c>q</c> ≥ 2 ký tự): mã bắt đầu bằng <c>q</c> trong nước <c>country</c>, tối đa 15 dòng.</summary>
    [HttpGet("postal-search")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PostalSuggestion>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostalSearch([FromQuery] string? country, [FromQuery] string? q, CancellationToken ct) =>
        OkData(await geo.SearchPostalAsync(country ?? "", q ?? "", ct));

    /// <summary>
    /// Hãng tính phụ phí vùng sâu vùng xa (VSVX / ODA) ở địa chỉ người nhận — tra bảng dbo.VungXauVungXa của hệ thống cũ
    /// theo nước (mã ISO) + mã bưu chính + thành phố. Rỗng khi không thuộc vùng nào.
    /// </summary>
    [HttpGet("remote-areas")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RemoteAreaHit>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoteAreas([FromQuery] string? country, [FromQuery] string? postal, [FromQuery] string? city, CancellationToken ct) =>
        OkData(await remoteAreas.FindAsync(country ?? "", postal, city, ct));

    /// <summary>Gợi ý địa chỉ trong nước <c>country</c> theo chữ khách gõ (<c>q</c> ≥ 3 ký tự) — tối đa 6 dòng, rỗng khi không có.</summary>
    [HttpGet("addresses")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AddressSuggestion>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Addresses([FromQuery] string? country, [FromQuery] string? q, CancellationToken ct) =>
        OkData(await geo.SuggestAddressesAsync(country ?? "", q ?? "", ct));
}
