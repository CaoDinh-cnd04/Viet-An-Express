using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using VietAnExpress.Identity.Contracts;
using VietAnExpress.Identity.CustomerOtp;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Web;

namespace VietAnExpress.Identity.Auth;

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
        if (result.IsFailure) return Problem(result.Error);
        // OTP khách (công tắc CustomerOtp:Mode): đúng mật khẩu → gửi mã, chưa cấp phiên; nhập mã ở /auth/verify-otp.
        var otp = await Sender.Send(new StartCustomerOtpCommand(result.Value, body.UserName, body.Remember), ct);
        if (otp.IsFailure) return Problem(otp.Error);
        return otp.Value is { } challenge
            ? OkData(challenge, $"Đã gửi mã OTP tới {challenge.Email}")
            : SessionResponse(result.Value, useCookies, "Đăng nhập thành công");
    }

    /// <summary>OTP khách, lần đăng nhập đầu: email nhận mã + đồng ý xử lý dữ liệu cá nhân → gửi mã xác nhận tới email đó.</summary>
    [HttpPost("otp-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<CustomerOtpChallengeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OtpEmail(SetOtpEmailRequest body, CancellationToken ct)
    {
        var result = await Sender.Send(new SetOtpEmailCommand(body.ChallengeId, body.Email, body.AcceptTerms), ct);
        return result.IsSuccess ? OkData(result.Value, $"Đã gửi mã xác nhận tới {result.Value.Email}") : Problem(result.Error);
    }

    /// <summary>Bước 2 khi bật OTP khách: nhập mã OTP đã gửi email → phiên (cookie, hoặc body khi ?useCookies=false).</summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<ApiResponse<SessionUserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyOtp(VerifyCustomerOtpRequest body, [FromQuery] bool useCookies = true, CancellationToken ct = default)
    {
        var result = await Sender.Send(new VerifyCustomerOtpCommand(body.ChallengeId, body.Code), ct);
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
        if (string.IsNullOrEmpty(token)) return Problem(AuthErrors.SessionExpired);

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
    /// Đăng xuất phiên này: thu hồi refresh token (cookie, hoặc body với ứng dụng không dùng cookie) và access token đang gửi kèm
    /// — chép được token cũng không dùng tiếp được — rồi xoá cookie. Đổi mật khẩu để huỷ phiên trên mọi thiết bị.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<ApiMessage>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(RefreshRequest? body, CancellationToken ct)
    {
        var refresh = !string.IsNullOrEmpty(body?.RefreshToken) ? body!.RefreshToken : Request.Cookies[AuthCookies.Refresh];
        await Sender.Send(new LogoutCommand(refresh, User.FindFirst(VaClaimTypes.TokenId)?.Value, AccessTokenExpiry(User)), ct);
        AuthCookies.Clear(Response, jwt.Value);
        return Ok(new ApiMessage("Đã đăng xuất"));
    }

    /// <summary>Hạn của access token đã xác thực (claim exp, giây Unix) — null khi request không kèm token hợp lệ.</summary>
    internal static DateTimeOffset? AccessTokenExpiry(System.Security.Claims.ClaimsPrincipal user) =>
        long.TryParse(user.FindFirst(VaClaimTypes.Expires)?.Value, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

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
