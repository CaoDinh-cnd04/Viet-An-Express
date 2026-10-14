using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Application;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Application.Dtos;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.Infrastructure;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Api;

/// <summary>
/// Đăng nhập / làm mới / đăng xuất / đổi mật khẩu cho khách hàng (tài khoản ở dbo.TCustomer).
/// Mặc định (web portal): token trả qua cookie HttpOnly, body chỉ có thông tin người dùng — khớp frontend.
/// Ứng dụng khác (mobile, tích hợp): gọi với ?useCookies=false để nhận token trong body và gửi header Authorization: Bearer.
/// </summary>
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
[Tags("Tài khoản")]
internal sealed class AuthController(IOptions<JwtOptions> jwt) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<TokenResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest body, [FromQuery] bool useCookies = true, CancellationToken ct = default)
    {
        var result = await Sender.Send(new LoginCommand(body.UserName, body.Password, body.Remember), ct);
        return result.IsSuccess ? SessionResponse(result.Value, useCookies, "Đăng nhập thành công") : Problem(result.Error);
    }

    /// <summary>Làm mới phiên. Refresh token lấy từ body, nếu không có thì từ cookie.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshRequest? body, CancellationToken ct)
    {
        var fromBody = !string.IsNullOrEmpty(body?.RefreshToken);
        var token = fromBody ? body!.RefreshToken : Request.Cookies[AuthCookies.Refresh];
        if (string.IsNullOrEmpty(token)) return Problem(IdentityErrors.SessionExpired);

        var result = await Sender.Send(new RefreshSessionCommand(token!), ct);
        if (result.IsFailure && !fromBody) AuthCookies.Clear(Response, jwt.Value);
        return result.IsSuccess ? SessionResponse(result.Value, useCookies: !fromBody, message: null) : Problem(result.Error);
    }

    /// <summary>
    /// Phiên hiện tại cho web portal — luôn 200 (không sinh lỗi 401 khi chưa đăng nhập).
    /// Còn refresh cookie → cấp lại token theo quyền hiện tại rồi trả người dùng (thêm / đổi quyền trong code hoặc admin đổi quyền
    /// tài khoản con thì mở lại trang là có hiệu lực, không phải đăng nhập lại); chỉ có access token → người dùng; còn lại → data null.
    /// </summary>
    [HttpGet("session")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        var refresh = Request.Cookies[AuthCookies.Refresh];
        if (!string.IsNullOrEmpty(refresh))
        {
            var renewed = await Sender.Send(new RefreshSessionCommand(refresh), ct);
            if (renewed.IsSuccess) return SessionResponse(renewed.Value, useCookies: true, message: null);
            AuthCookies.Clear(Response, jwt.Value);
            return OkData<SessionUserDto?>(null);
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var current = await Sender.Send(new GetSessionQuery(), ct);
            if (current.IsSuccess) return OkData(current.Value);
        }
        return OkData<SessionUserDto?>(null);
    }

    /// <summary>
    /// Thu hồi phiên của thiết bị này ở máy chủ (dbo.PhienThuHoi — access / refresh token của phiên bị từ chối ngay, kể cả
    /// response làm mới phiên về muộn) rồi xoá cookie. Đổi mật khẩu để huỷ phiên trên mọi thiết bị.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await Sender.Send(new LogoutCommand(Request.Cookies[AuthCookies.Refresh], User.FindFirst(VaClaimTypes.SessionId)?.Value), ct);
        AuthCookies.Clear(Response, jwt.Value);
        return Ok(new ApiMessage("Đã đăng xuất"));
    }

    [HttpPost("change-password")]
    [HasPermission(IdentityPermissions.ChangePassword)]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(
            new ChangePasswordCommand(body.CurrentPassword, body.NewPassword, Request.Cookies[AuthCookies.Refresh]), ct);
        if (result.IsFailure) return Problem(result.Error);

        // Mật khẩu đổi → phiên cũ hết hiệu lực; thiết bị này nhận cookie phiên mới để không bị đăng xuất.
        AuthCookies.Write(Response, result.Value, jwt.Value);
        return Ok(new ApiMessage("Đã đổi mật khẩu. Các thiết bị khác sẽ phải đăng nhập lại"));
    }

    private IActionResult SessionResponse(AuthSession session, bool useCookies, string? message)
    {
        if (!useCookies)
            return OkData(new TokenResponse(session.User, session.AccessToken, session.AccessTokenExpiresAt,
                session.RefreshToken, session.RefreshTokenExpiresAt), message);

        AuthCookies.Write(Response, session, jwt.Value);
        return OkData(session.User, message);
    }
}

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

/// <param name="UserName">Tên đăng nhập của khách (dbo.TCustomer.Login_UserName — frontend gửi trường "username").</param>
internal sealed record LoginRequest(string UserName, string Password, bool Remember = false);

internal sealed record RefreshRequest(string? RefreshToken);

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record TokenResponse(
    SessionUserDto User,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
