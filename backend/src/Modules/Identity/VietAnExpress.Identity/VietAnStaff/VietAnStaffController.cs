using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Nhân viên Việt An (dbo.USERS) đăng nhập portal, chọn khách rồi dùng chính các trang của khách để tạo đơn giúp.
/// Token qua cookie như phiên khách (cùng <see cref="AuthCookies"/>) — làm mới / đăng xuất dùng chung /auth/refresh, /auth/logout.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth/vietan")]
[Tags("Tài khoản")]
internal sealed class VietAnStaffController(IOptions<JwtOptions> jwt) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<VietAnOtpChallengeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(VietAnLoginRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new VietAnLoginCommand(body.UserName, body.Password, body.Remember), ct);
        return result.IsFailure ? Problem(result.Error) : OkData(result.Value, $"Đã gửi mã OTP tới {result.Value.Email}");
    }

    /// <summary>Bước 2: nhập mã OTP đã gửi email → cookie phiên.</summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyOtp(VerifyVietAnOtpRequest body, CancellationToken ct) =>
        SessionResponse(await Sender.Send(new VerifyVietAnOtpCommand(body.ChallengeId, body.Code), ct), "Đăng nhập thành công");

    /// <summary>Khách được làm thay — <c>?q=</c> tìm theo mã / tên / SĐT / tên đăng nhập.</summary>
    [HttpGet("customers")]
    [HasPermission(IdentityPermissions.ActForCustomer)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ActableCustomerDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Customers([FromQuery] string? q, CancellationToken ct) =>
        FromResult(await Sender.Send(new ListActableCustomersQuery(q), ct));

    /// <summary>Chọn / đổi khách → cookie phiên mới gắn khách đó.</summary>
    [HttpPost("act-as")]
    [HasPermission(IdentityPermissions.ActForCustomer)]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ActAs(ActAsCustomerRequest body, CancellationToken ct) =>
        SessionResponse(await Sender.Send(new ActAsCustomerCommand(body.CustomerId, Request.Cookies[AuthCookies.Refresh]), ct), null);

    private IActionResult SessionResponse(Result<AuthSession> result, string? message)
    {
        if (result.IsFailure) return Problem(result.Error);
        AuthCookies.Write(Response, result.Value, jwt.Value);
        return OkData(result.Value.User, message);
    }
}
