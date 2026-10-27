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
/// <param name="Id">Mã token (jti) — dùng để thu hồi khi đăng xuất.</param>
/// <param name="AuthTime">Lần nhập mật khẩu gốc của phiên; null với token cấp trước khi có giới hạn phiên.</param>
internal sealed record RefreshClaims(long CustomerId, long? StaffId, string PasswordStamp, bool IsPersistent,
    string Id = "", DateTimeOffset ExpiresAt = default, DateTimeOffset? AuthTime = null);

internal interface ITokenService
{
    /// <param name="lifetime">Null = Jwt:AccessTokenMinutes.</param>
    IssuedToken CreateAccessToken(long customerId, long? staffId, string userName, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions,
        TimeSpan? lifetime = null);

    /// <summary>
    /// Refresh token tự chứa (JWT ký, không lưu DB) mang "dấu" của mật khẩu hiện tại:
    /// đổi mật khẩu (trên portal hay hệ thống cũ) thì mọi refresh token cũ hết hiệu lực.
    /// </summary>
    /// <param name="authTime">Lần nhập mật khẩu gốc (gia hạn phiên thì giữ nguyên); null = đăng nhập mới. Hạn token không vượt quá authTime + Jwt:MaxSessionDays.</param>
    IssuedToken CreateRefreshToken(long customerId, long? staffId, string passwordStamp, bool persistent, DateTimeOffset? authTime = null);

    /// <summary>Null nếu token sai chữ ký, hết hạn, quá giới hạn phiên, hoặc không phải refresh token.</summary>
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
    private const string AuthTimeClaim = "auth_time";

    private readonly JwtOptions _jwt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken CreateAccessToken(long customerId, long? staffId, string userName, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions,
        TimeSpan? lifetime = null)
    {
        var id = customerId.ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new(VaClaimTypes.Subject, id),
            new(VaClaimTypes.Name, userName),
            new(VaClaimTypes.CustomerId, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (staffId is { } sid) claims.Add(new Claim(VaClaimTypes.StaffId, sid.ToString(CultureInfo.InvariantCulture)));
        claims.AddRange(roles.Select(r => new Claim(VaClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(VaClaimTypes.Permission, p)));
        return Create(claims, _jwt.Audience, clock.GetUtcNow() + (lifetime ?? TimeSpan.FromMinutes(_jwt.AccessTokenMinutes)));
    }

    public IssuedToken CreateRefreshToken(long customerId, long? staffId, string passwordStamp, bool persistent, DateTimeOffset? authTime = null)
    {
        var now = clock.GetUtcNow();
        var since = authTime ?? now;
        var expires = persistent ? now.AddDays(_jwt.RefreshTokenDays) : now.AddHours(_jwt.SessionRefreshTokenHours);
        var limit = since.AddDays(_jwt.MaxSessionDays);
        if (expires > limit) expires = limit;
        var claims = new List<Claim>
        {
            new(VaClaimTypes.Subject, customerId.ToString(CultureInfo.InvariantCulture)),
            new(StampClaim, passwordStamp),
            new(PersistentClaim, persistent ? "1" : "0"),
            new(AuthTimeClaim, since.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
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
        if (!long.TryParse(claims.FindFirst(VaClaimTypes.Subject)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || claims.FindFirst(StampClaim)?.Value is not { Length: > 0 } stamp)
            return null;

        long? staffId = null;
        if (claims.FindFirst(VaClaimTypes.StaffId)?.Value is { } staffClaim)
        {
            if (!long.TryParse(staffClaim, NumberStyles.None, CultureInfo.InvariantCulture, out var sid)) return null;
            staffId = sid;
        }
        DateTimeOffset? authTime = null;
        if (claims.FindFirst(AuthTimeClaim)?.Value is { } at)
        {
            if (!long.TryParse(at, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return null;
            authTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
            if (authTime.Value.AddDays(_jwt.MaxSessionDays) <= clock.GetUtcNow()) return null;
        }
        var jwt = (JsonWebToken)result.SecurityToken;
        return new RefreshClaims(id, staffId, stamp, claims.FindFirst(PersistentClaim)?.Value == "1",
            jwt.Id, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero), authTime);
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
