using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>1 khách trong danh sách chọn khách của nhân viên Việt An.</summary>
internal sealed record ActableCustomerDto(long Id, string Code, string CompanyName, string? Phone, string? Address, string? LoginUserName);

internal sealed record VietAnLoginRequest(string UserName, string Password, bool Remember = false);

internal sealed record ActAsCustomerRequest(long CustomerId);

/// <summary>Đúng mật khẩu → đã gửi mã OTP: <see cref="ChallengeId"/> gửi kèm mã ở bước 2, <see cref="Email"/> đã che bớt.</summary>
internal sealed record VietAnOtpChallengeDto(string ChallengeId, string Email, int ExpiresInSeconds);

internal sealed record VerifyVietAnOtpRequest(string ChallengeId, string Code);

internal static class VietAnStaffErrors
{
    public static readonly Error VietAnOnly = Error.Forbidden("VIETAN_STAFF_ONLY", "Chức năng chỉ dành cho nhân viên Việt An");

    public static readonly Error NoOtpEmail = Error.BusinessRule("VIETAN_NO_EMAIL",
        "Tài khoản nhân viên chưa có email nhận mã OTP — nhờ quản trị cập nhật email trên phần mềm Việt An");

    public static readonly Error OtpEmailNotConfigured = Error.BusinessRule("OTP_EMAIL_NOT_CONFIGURED",
        "Máy chủ chưa cấu hình email gửi mã OTP — liên hệ quản trị");

    public static readonly Error OtpEmailFailed = Error.BusinessRule("OTP_EMAIL_FAILED", "Không gửi được email mã OTP — vui lòng thử lại sau ít phút");

    public static readonly Error OtpWrong = Error.Unauthorized("OTP_INVALID", "Mã OTP không đúng");

    public static readonly Error OtpExpired = Error.Unauthorized("OTP_EXPIRED", "Mã OTP đã hết hạn hoặc nhập sai quá nhiều lần — vui lòng đăng nhập lại để nhận mã mới");

    public static readonly Error CustomerOutOfScope = Error.Forbidden("CUSTOMER_OUT_OF_SCOPE",
        "Không tìm thấy khách hoặc khách không do bạn phụ trách");
}
