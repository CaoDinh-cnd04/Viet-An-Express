using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VietAnExpress.SharedKernel.Authorization;

namespace VietAnExpress.Identity.Infrastructure;

internal sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Nội dung 1 refresh token hợp lệ.</summary>
/// <param name="StaffId">Có khi là tài khoản con (nhân viên); null = tài khoản chính của khách.</param>
/// <summary>Nội dung refresh token đã kiểm chữ ký + hạn. <see cref="SessionId"/>: mã phiên (thu hồi khi đăng xuất).</summary>
internal sealed record RefreshClaims(long CustomerId, long? StaffId, string PasswordStamp, bool IsPersistent, string SessionId, DateTimeOffset ExpiresAt);

internal interface ITokenService
{
    /// <param name="sessionId">Mã phiên; null → phiên mới.</param>
    IssuedToken CreateAccessToken(long customerId, long? staffId, string userName, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions,
        string? sessionId = null);

    /// <summary>
    /// Refresh token tự chứa (JWT ký, không lưu DB) mang "dấu" của mật khẩu hiện tại:
    /// đổi mật khẩu (trên portal hay hệ thống cũ) thì mọi refresh token cũ hết hiệu lực.
    /// </summary>
    IssuedToken CreateRefreshToken(long customerId, long? staffId, string passwordStamp, bool persistent, string? sessionId = null);

    /// <summary>Null nếu token sai chữ ký, hết hạn, hoặc không phải refresh token.</summary>
    Task<RefreshClaims?> ReadRefreshTokenAsync(string token);

    /// <summary>
    /// Dấu mật khẩu = HMAC(secret, CustomerID + mật khẩu): không lộ mật khẩu, đổi mật khẩu là đổi dấu.
    /// Tài khoản con: HMAC(secret, "staff:" + ID + mật khẩu đã băm).
    /// </summary>
    string PasswordStamp(long customerId, string password, long? staffId = null);
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private const string StampClaim = "pst";
    private const string PersistentClaim = "rem";

    private readonly JwtOptions _jwt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    /// <summary>Mã phiên mới cho 1 lần đăng nhập.</summary>
    public static string NewSessionId() => Guid.NewGuid().ToString("N");

    public IssuedToken CreateAccessToken(long customerId, long? staffId, string userName, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions,
        string? sessionId = null)
    {
        var id = customerId.ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new(VaClaimTypes.Subject, id),
            new(VaClaimTypes.Name, userName),
            new(VaClaimTypes.CustomerId, id),
            new(VaClaimTypes.SessionId, sessionId ?? NewSessionId()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (staffId is { } sid) claims.Add(new Claim(VaClaimTypes.StaffId, sid.ToString(CultureInfo.InvariantCulture)));
        claims.AddRange(roles.Select(r => new Claim(VaClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(VaClaimTypes.Permission, p)));
        return Create(claims, _jwt.Audience, clock.GetUtcNow().AddMinutes(_jwt.AccessTokenMinutes));
    }

    public IssuedToken CreateRefreshToken(long customerId, long? staffId, string passwordStamp, bool persistent, string? sessionId = null)
    {
        var now = clock.GetUtcNow();
        var expires = persistent ? now.AddDays(_jwt.RefreshTokenDays) : now.AddHours(_jwt.SessionRefreshTokenHours);
        var claims = new List<Claim>
        {
            new(VaClaimTypes.Subject, customerId.ToString(CultureInfo.InvariantCulture)),
            new(StampClaim, passwordStamp),
            new(PersistentClaim, persistent ? "1" : "0"),
            new(VaClaimTypes.SessionId, sessionId ?? NewSessionId()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (staffId is { } sid) claims.Add(new Claim(VaClaimTypes.StaffId, sid.ToString(CultureInfo.InvariantCulture)));
        return Create(claims, _jwt.RefreshAudience, expires);
    }

    public async Task<RefreshClaims?> ReadRefreshTokenAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.RefreshAudience,
            IssuerSigningKey = SigningKey(_jwt),
            LifetimeValidator = (_, expires, _, _) => expires is { } e && e > clock.GetUtcNow().UtcDateTime
        });
        if (!result.IsValid) return null;

        var claims = result.ClaimsIdentity;
        // Token cấp trước khi có mã phiên (không thu hồi được) → không nhận, khách đăng nhập lại 1 lần.
        if (!long.TryParse(claims.FindFirst(VaClaimTypes.Subject)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || claims.FindFirst(StampClaim)?.Value is not { Length: > 0 } stamp
            || claims.FindFirst(VaClaimTypes.SessionId)?.Value is not { Length: > 0 } sessionId)
            return null;

        long? staffId = null;
        if (claims.FindFirst(VaClaimTypes.StaffId)?.Value is { } staffClaim)
        {
            if (!long.TryParse(staffClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var sid)) return null;
            staffId = sid;
        }
        return new RefreshClaims(id, staffId, stamp, claims.FindFirst(PersistentClaim)?.Value == "1", sessionId,
            new DateTimeOffset(result.SecurityToken.ValidTo, TimeSpan.Zero));
    }

    public string PasswordStamp(long customerId, string password, long? staffId = null)
    {
        var subject = staffId is { } sid
            ? string.Create(CultureInfo.InvariantCulture, $"staff:{sid}:{password}")
            : string.Create(CultureInfo.InvariantCulture, $"{customerId}:{password}");
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_jwt.Secret), Encoding.UTF8.GetBytes(subject));
        return Base64UrlEncoder.Encode(mac[..16]);
    }

    private IssuedToken Create(IEnumerable<Claim> claims, string audience, DateTimeOffset expires)
    {
        var now = clock.GetUtcNow();
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(_jwt), SecurityAlgorithms.HmacSha256)
        });
        return new IssuedToken(token, expires);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.Secret));

    /// <summary>So sánh thời gian cố định — không lộ độ dài / ký tự đúng qua thời gian phản hồi.</summary>
    public static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
