using Microsoft.AspNetCore.Http;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Auth;

/// <summary>
/// Chế độ cookie cho web portal: token nằm trong cookie HttpOnly (JavaScript không đọc được → chống XSS lấy token),
/// SameSite=Strict (trình duyệt không gửi kèm request từ site khác → chống CSRF).
/// </summary>
internal static class AuthCookies
{
    public const string Access = "va_access";
    public const string Refresh = "va_refresh";

    /// <summary>Refresh token chỉ gửi kèm các request tới /api/.../auth — giảm bề mặt lộ.</summary>
    private const string RefreshPath = "/api/v1/auth";

    public static void Write(HttpResponse response, AuthSession session, JwtOptions jwt)
    {
        // Không ghi nhớ → cookie phiên (không có Expires), đóng trình duyệt là mất.
        DateTimeOffset? expires = session.IsPersistent ? session.RefreshTokenExpiresAt : null;
        response.Cookies.Append(Access, session.AccessToken, Options(jwt, "/", expires));
        response.Cookies.Append(Refresh, session.RefreshToken, Options(jwt, RefreshPath, expires));
    }

    public static void Clear(HttpResponse response, JwtOptions jwt)
    {
        response.Cookies.Delete(Access, Options(jwt, "/", null));
        response.Cookies.Delete(Refresh, Options(jwt, RefreshPath, null));
    }

    private static CookieOptions Options(JwtOptions jwt, string path, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = jwt.SecureCookies,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Expires = expires,
        IsEssential = true
    };
}
