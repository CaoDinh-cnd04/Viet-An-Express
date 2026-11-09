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
        var result = await Sender.Send(new VietAnLoginCommand(body.UserName, body.Password, body.Remember, Request.Cookies[AuthCookies.OtpDayPass]), ct);
        if (result.IsFailure) return Problem(result.Error);
        // Hôm nay đã nhập OTP trên trình duyệt này → vào luôn, trả SessionUser như /auth/verify-otp.
        if (result.Value.Session is { } session)
        {
            AuthCookies.Write(Response, session, jwt.Value);
            return OkData(session.User, "Đăng nhập thành công");
        }
        return OkData(result.Value.Challenge!, $"Đã gửi mã OTP tới {result.Value.Challenge!.Email}");
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

    /// <summary>Tạo khách mới (khách lần đầu gửi) → cookie phiên làm thay khách đó; mở trang tạo đơn với người gửi trống.</summary>
    [HttpPost("customers/new")]
    [HasPermission(IdentityPermissions.ActForCustomer)]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCustomer(CancellationToken ct) =>
        SessionResponse(await Sender.Send(new CreateNewCustomerCommand(Request.Cookies[AuthCookies.Refresh]), ct), "Đã tạo khách mới — nhập thông tin người gửi");

    /// <summary>Nhân viên sales / CS để gán phụ trách khách mới — chỉ quản trị / CS.</summary>
    [HttpGet("support-staff")]
    [HasPermission(IdentityPermissions.ActForCustomer)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<SupportStaffDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SupportStaff(CancellationToken ct) =>
        FromResult(await Sender.Send(new ListSupportStaffQuery(), ct));

    /// <summary>Gán nhân viên phụ trách (TCustomer.Staff_ID) cho khách mới — chỉ quản trị / CS.</summary>
    [HttpPut("customers/{id:long}/manager")]
    [HasPermission(IdentityPermissions.ActForCustomer)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignManager(long id, AssignManagerRequest body, CancellationToken ct) =>
        FromResult(await Sender.Send(new AssignCustomerManagerCommand(id, body.StaffId), ct), "Đã gán nhân viên phụ trách");

    private IActionResult SessionResponse(Result<AuthSession> result, string? message)
    {
        if (result.IsFailure) return Problem(result.Error);
        AuthCookies.Write(Response, result.Value, jwt.Value);
        return OkData(result.Value.User, message);
    }
}
