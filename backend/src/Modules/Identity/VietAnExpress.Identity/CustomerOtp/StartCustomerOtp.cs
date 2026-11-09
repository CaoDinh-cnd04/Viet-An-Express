using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Authorization;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.CustomerOtp;

/// <summary>
/// Sau khi khách nhập đúng mật khẩu (<see cref="LoginCommand"/>), bật OTP (<see cref="CustomerOtpOptions"/>): trả thử thách thay cho phiên
/// (phiên vừa dựng bị bỏ, chưa gửi cho trình duyệt).
/// <list type="bullet">
/// <item>Đã xác nhận email (<see cref="OtpEmailRegistry"/>) và email hồ sơ còn đúng → gửi mã 6 số tới email đó.</item>
/// <item>Lần đầu / email hồ sơ đã đổi → chưa gửi mã: khách nhập email nhận mã + đồng ý điều khoản ở <see cref="SetOtpEmailCommand"/>.</item>
/// </list>
/// Null = cấp phiên như cũ (OTP tắt, hoặc máy chủ mail lỗi — không khóa khách ra ngoài, ghi log).
/// Dev trên laptop: chưa cấu hình SMTP → mã ghi ra log backend.
/// </summary>
/// <param name="LoginName">Tên đăng nhập khách gõ — cùng khóa tạm với <see cref="LoginAttemptGuard"/> của bước mật khẩu.</param>
internal sealed record StartCustomerOtpCommand(AuthSession Session, string LoginName, bool Remember) : IRequest<Result<CustomerOtpChallengeDto?>>;

internal sealed class StartCustomerOtpHandler(
    IdentityDbContext db, CustomerOtpStore otps, OtpEmailRegistry registry, CustomerOtpMailer mailer, IOptions<CustomerOtpOptions> options)
    : IRequestHandler<StartCustomerOtpCommand, Result<CustomerOtpChallengeDto?>>
{
    public async Task<Result<CustomerOtpChallengeDto?>> Handle(StartCustomerOtpCommand cmd, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Result.Success<CustomerOtpChallengeDto?>(null);
        var user = cmd.Session.User;
        var staffId = StaffIdOf(cmd.Session.RefreshToken);
        var current = staffId is { } sid
            ? (await db.StaffAccounts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid, ct))?.Email
            : (await db.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.CustomerId == user.CustomerId, ct))?.Email;
        var email = CustomerOtpRules.Recipient(current);
        var pending = new CustomerOtpStore.Pending(user.CustomerId, staffId, cmd.LoginName.Trim(), cmd.Remember);

        if (email is null || !registry.IsConfirmed(OtpEmailRegistry.Key(user.CustomerId, staffId), email))
        {
            // Lần đầu: chưa gửi mã — mã thử thách chỉ giữ chỗ cho bước nhập email (5 phút).
            var (challengeId, _) = otps.Issue(pending);
            return new CustomerOtpChallengeDto(challengeId, "", (int)CustomerOtpStore.Lifetime.TotalSeconds, NeedEmail: true, SuggestedEmail: email);
        }
        return await mailer.SendAsync(pending, email, Name(user), ct) is { } challenge
            ? challenge
            : Result.Success<CustomerOtpChallengeDto?>(null);
    }

    internal static string Name(SessionUserDto user) => string.IsNullOrWhiteSpace(user.FullName) ? user.CompanyName : user.FullName;

    /// <summary>Tài khoản con đọc từ claim staff_id của refresh token vừa cấp (tài khoản chính không có claim này).</summary>
    private static long? StaffIdOf(string refreshToken) =>
        new JsonWebToken(refreshToken).TryGetClaim(VaClaimTypes.StaffId, out var claim)
        && long.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
}

/// <summary>Cấp mã + gửi email OTP khách (dùng ở bước mật khẩu và bước nhập email lần đầu). Null = không gửi được (đã ghi log).</summary>
internal sealed class CustomerOtpMailer(CustomerOtpStore otps, IEmailSender email, EmailCompany company, IHostEnvironment env, ILogger<CustomerOtpMailer> logger)
{
    public async Task<CustomerOtpChallengeDto?> SendAsync(CustomerOtpStore.Pending pending, string to, string name, CancellationToken ct)
    {
        var (challengeId, code) = otps.Issue(pending);
        var seconds = (int)CustomerOtpStore.Lifetime.TotalSeconds;
        if (!email.IsConfigured)
        {
            if (!env.IsDevelopment())
            {
                logger.LogWarning("Bật OTP khách nhưng chưa cấu hình email gửi — bỏ qua OTP cho {UserName}", pending.UserName);
                return null;
            }
            logger.LogWarning("[Dev] Mã OTP đăng nhập của khách {UserName} ({Email}): {Code}", pending.UserName, to, code);
            return new CustomerOtpChallengeDto(challengeId, VietAnOtp.Mask(to), seconds);
        }
        try
        {
            await email.SendAsync(CustomerOtpRules.Email(to, name, code, company), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Không gửi được email OTP cho khách {UserName}", pending.UserName);
            return null;
        }
        if (env.IsDevelopment()) logger.LogInformation("[Dev] Đã gửi mã OTP khách {UserName} tới {Email}: {Code}", pending.UserName, to, code);
        return new CustomerOtpChallengeDto(challengeId, VietAnOtp.Mask(to), seconds);
    }
}
