using System.Globalization;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.VietAnStaff;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.CustomerOtp;

/// <summary>
/// Công tắc OTP đăng nhập của khách (mục <c>CustomerOtp</c>). <c>Mode</c>: "off" (mặc định, như cũ — chỉ mật khẩu) ·
/// "on" (cũ: "email-only"): đăng nhập bằng mật khẩu nào cũng phải nhập mã OTP gửi email. Lần đầu (chưa xác nhận email) mọi tài khoản phải
/// nhập email nhận mã + đồng ý xử lý dữ liệu cá nhân, xác nhận bằng mã gửi tới email đó — kể cả khách đã có email trong hồ sơ.
/// </summary>
internal sealed class CustomerOtpOptions
{
    public const string Section = "CustomerOtp";
    public const string Off = "off";
    public const string On = "on";
    public const string EmailOnly = "email-only";

    public string Mode { get; set; } = Off;

    public bool Enabled => Mode?.Trim().ToLowerInvariant() is On or EmailOnly;
}

/// <summary>Mã OTP đăng nhập của khách / tài khoản con — mỗi tài khoản 1 mã còn hiệu lực.</summary>
internal sealed class CustomerOtpStore(TimeProvider clock) : OtpStore<CustomerOtpStore.Pending>(clock)
{
    /// <summary>
    /// Tài khoản đã nhập đúng mật khẩu, chờ mã OTP — <see cref="StaffId"/> null = tài khoản chính.
    /// <see cref="NewEmail"/> có giá trị = đang xác nhận email lần đầu: nhập đúng mã thì lưu email này + ghi nhận đồng ý (<see cref="ConsentAt"/>).
    /// </summary>
    public sealed record Pending(long CustomerId, long? StaffId, string UserName, bool Remember, string? NewEmail = null, DateTimeOffset? ConsentAt = null);

    public (string ChallengeId, string Code) Issue(Pending p) => IssueFor($"{p.CustomerId}:{p.StaffId}", p);
}

/// <summary>
/// Đúng mật khẩu, cần mã OTP: <see cref="ChallengeId"/> gửi kèm mã ở bước 2, <see cref="Email"/> đã che bớt.
/// <see cref="NeedEmail"/> = lần đầu: chưa gửi mã, khách nhập email nhận mã (<see cref="SuggestedEmail"/> = email hồ sơ để điền sẵn) ở /auth/otp-email.
/// </summary>
internal sealed record CustomerOtpChallengeDto(string ChallengeId, string Email, int ExpiresInSeconds, bool NeedEmail = false, string? SuggestedEmail = null)
{
    /// <summary>Frontend phân biệt với phiên đăng nhập thường (cùng endpoint /auth/login).</summary>
    public bool OtpRequired => true;
}

internal sealed record VerifyCustomerOtpRequest(string ChallengeId, string Code);

/// <summary>Lần đầu: email nhận mã + đồng ý xử lý dữ liệu cá nhân (<see cref="CustomerOtpRules.ConsentVersion"/>).</summary>
internal sealed record SetOtpEmailRequest(string ChallengeId, string Email, bool AcceptTerms, string? TermsVersion = null);

/// <summary>Hàm thuần của OTP khách — có test.</summary>
internal static class CustomerOtpRules
{
    /// <summary>Phiên bản nội dung đồng ý xử lý dữ liệu cá nhân hiện trên màn nhập email (frontend gửi kèm, lưu ở sổ đồng ý).</summary>
    public const string ConsentVersion = "BVDLCN-2026.11";

    /// <summary>
    /// Email nhận mã: địa chỉ đầu tiên hợp lệ; bỏ email @vietanexpress.com (nhân viên nhập thay, không phải của khách) → null = chưa có email.
    /// Email công ty của khách (vd ketoan@congtykhach.vn) dùng được.
    /// </summary>
    public static string? Recipient(params string?[] candidates) =>
        candidates.Select(VietAnOtp.ValidEmail)
            .FirstOrDefault(e => e is not null && !e.EndsWith("@vietanexpress.com", StringComparison.OrdinalIgnoreCase)
                && !e.EndsWith("@vietanexpress.com.vn", StringComparison.OrdinalIgnoreCase));

    public static EmailMessage Email(string to, string name, string code) => new(
        $"Mã đăng nhập portal Việt An: {code}",
        string.Join('\n',
            $"Chào {name},",
            "",
            $"Mã OTP đăng nhập portal Việt An Express của bạn là: {code}",
            $"Mã có hiệu lực {CustomerOtpStore.Lifetime.TotalMinutes.ToString(CultureInfo.InvariantCulture)} phút và chỉ dùng được 1 lần.",
            "",
            "Nếu không phải bạn đang đăng nhập, hãy đổi mật khẩu trong trang Tài khoản của portal và báo nhân viên Việt An phụ trách."),
        To: [to]);
}

internal static class CustomerOtpErrors
{
    public static readonly Error Wrong = VietAnStaffErrors.OtpWrong;
    public static readonly Error Expired = VietAnStaffErrors.OtpExpired;
    public static readonly Error InvalidEmail = Error.Validation("OTP_EMAIL_INVALID", "Email không hợp lệ — nhập email của bạn hoặc công ty bạn (không dùng email @vietanexpress.com)");
    public static readonly Error ConsentRequired = Error.Validation("CONSENT_REQUIRED", "Vui lòng đọc và đồng ý cho Việt An Express xử lý email để gửi mã đăng nhập");
    public static readonly Error EmailFailed = VietAnStaffErrors.OtpEmailFailed;
}
