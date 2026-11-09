using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Nhân viên Việt An đăng nhập portal bằng USER_NAME / USER_PASSWORD của dbo.USERS (như CMS). Đúng mật khẩu thì chưa cấp phiên:
/// gửi mã OTP 6 số tới email USERS.EMAIL1, nhập đúng mã ở <see cref="VerifyVietAnOtpCommand"/> mới vào (phiên chưa gắn khách,
/// chọn khách ở bước sau — <see cref="ActAsCustomerCommand"/>). Hôm nay đã nhập OTP trên trình duyệt này (<see cref="VietAnOtpDayPass"/>) thì cấp phiên luôn.
/// </summary>
internal sealed record VietAnLoginCommand(string UserName, string Password, bool Remember, string? OtpDayPass = null) : IRequest<Result<VietAnLoginOutcome>>;

internal sealed class VietAnLoginHandler(
    ICustomersApi customers, LoginAttemptGuard guard, VietAnOtpStore otps, IEmailSender email, EmailCompany company, IHostEnvironment env, ILogger<VietAnLoginHandler> logger,
    SessionService sessions, VietAnOtpDayPass dayPass, VietAnSessionRegistry? activeSessions = null)
    : IRequestHandler<VietAnLoginCommand, Result<VietAnLoginOutcome>>
{
    public async Task<Result<VietAnLoginOutcome>> Handle(VietAnLoginCommand cmd, CancellationToken ct)
    {
        // Khóa tạm riêng (tiền tố vietan:) — không lẫn với tên đăng nhập của khách. Nhập sai OTP cũng tính vào đây.
        var key = VietAnOtp.GuardKey(cmd.UserName);
        if (guard.LockedFor(key) is { } locked) return AuthErrors.LoginLocked(locked);
        var candidates = await customers.FindVietAnUsersAsync(cmd.UserName, ct);
        var agent = candidates.FirstOrDefault(u => VietAnOtp.PasswordMatches(u.Password, cmd.Password));
        if (agent is null)
        {
            // Không ghi mật khẩu — chỉ lý do, để tra log máy chủ khi nhân viên báo "đúng mật khẩu vẫn sai".
            logger.LogWarning("Nhân viên Việt An {UserName} đăng nhập không được: {Reason}", cmd.UserName.Trim(),
                candidates.Count == 0 ? "không có tài khoản USERS đang hoạt động (xem log Customers)" : "sai mật khẩu");
            guard.Failed(key);
            return AuthErrors.InvalidCredentials;
        }

        if (dayPass.IsValid(cmd.OtpDayPass, agent))
        {
            guard.Succeeded(key);
            // Vẫn giữ 1 phiên / nhân viên: máy đang đăng nhập trước đó bị đá ra.
            activeSessions?.Renew(agent.Id);
            return new VietAnLoginOutcome(null, sessions.Start(agent, null, cmd.Remember));
        }

        var to = VietAnOtp.ValidEmail(agent.Email);
        // Dev trên laptop: USERS chưa có email / chưa cấu hình SMTP → ghi mã ra log backend để thử được. Máy chủ (Production) bắt buộc gửi email.
        var logOnly = env.IsDevelopment() && (to is null || !email.IsConfigured);
        if (to is null && !logOnly) return VietAnStaffErrors.NoOtpEmail;
        if (!email.IsConfigured && !logOnly) return VietAnStaffErrors.OtpEmailNotConfigured;

        var (challengeId, code) = otps.Issue(agent.Id, agent.UserName, cmd.Remember);
        if (logOnly)
        {
            logger.LogWarning("[Dev] Mã OTP đăng nhập của nhân viên {UserName}: {Code}", agent.UserName, code);
            return new VietAnLoginOutcome(new VietAnOtpChallengeDto(challengeId, to is null ? "log backend (dev)" : VietAnOtp.Mask(to), (int)VietAnOtpStore.Lifetime.TotalSeconds));
        }
        try
        {
            await email.SendAsync(VietAnOtp.Email(to!, agent.FullName, code, company), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Không gửi được email OTP cho nhân viên {UserName}", agent.UserName);
            return VietAnStaffErrors.OtpEmailFailed;
        }
        return new VietAnLoginOutcome(new VietAnOtpChallengeDto(challengeId, VietAnOtp.Mask(to!), (int)VietAnOtpStore.Lifetime.TotalSeconds));
    }
}

/// <summary>Hàm thuần dùng chung cho bước gửi / nhập mã OTP — có test.</summary>
internal static class VietAnOtp
{
    public static string GuardKey(string userName) => "vietan:" + userName.Trim();

    /// <summary>
    /// USER_PASSWORD như phần mềm cũ (Login.aspx.cs: <c>Utility.Md5AddSecret(pwd) == USER_PASSWORD</c>): MD5 không muối của mật khẩu,
    /// byte băm đổi thẳng ra chuỗi bằng UTF8.GetString (mất byte lỗi thành "�" — cố ý giữ nguyên để khớp dữ liệu đã lưu).
    /// Dòng cũ còn lưu chữ thô (vd "123456") thì so trực tiếp. Phân biệt hoa thường, không cắt khoảng trắng — như phần mềm cũ. So thời gian cố định.
    /// </summary>
    public static bool PasswordMatches(string stored, string input) =>
        input.Length > 0
        && (JwtTokenService.FixedTimeEquals(stored, LegacyMd5(input)) | JwtTokenService.FixedTimeEquals(stored, input));

    /// <summary>Port <c>PQT.API.Utility.Md5AddSecret</c> của phần mềm cũ.</summary>
    public static string LegacyMd5(string password) => Encoding.UTF8.GetString(MD5.HashData(Encoding.UTF8.GetBytes(password)));

    /// <summary>EMAIL1 có dạng email thì dùng (CMS có thể để chữ khác / nhiều địa chỉ — lấy địa chỉ đầu).</summary>
    public static string? ValidEmail(string? value)
    {
        var first = value?.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is not null && first.IndexOf('@') > 0 && first.IndexOf('@') < first.Length - 1 ? first : null;
    }

    /// <summary>"thanhtan.vietan@gmail.com" → "th***@gmail.com" — báo cho người đăng nhập biết mã gửi đi đâu mà không lộ đủ địa chỉ.</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        var name = email[..at];
        return (name.Length <= 2 ? name[..1] : name[..2]) + "***" + email[at..];
    }

    public static EmailMessage Email(string to, string fullName, string code, EmailCompany company) =>
        OtpEmail.Build(to, fullName, code, VietAnOtpStore.Lifetime,
            "Nếu không phải bạn đang đăng nhập, hãy đổi mật khẩu trên phần mềm Việt An ngay và báo quản trị.", company);
}
