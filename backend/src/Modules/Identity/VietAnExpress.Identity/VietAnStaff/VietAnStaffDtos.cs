using VietAnExpress.Identity.Auth;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// 1 khách trong danh sách chọn khách của nhân viên Việt An. <see cref="IsNew"/> = khách mới tạo trên portal, CS chưa hoàn thiện ở CMS;
/// <see cref="ManagerName"/> = nhân viên phụ trách (TCustomer.Staff_ID).
/// </summary>
internal sealed record ActableCustomerDto(long Id, string Code, string CompanyName, string? Phone, string? Address, string? LoginUserName,
    bool IsNew = false, int? ManagerId = null, string? ManagerName = null);

/// <summary>Nhân viên sales / CS chọn làm người phụ trách khách mới.</summary>
internal sealed record SupportStaffDto(int Id, string UserName, string FullName);

internal sealed record AssignManagerRequest(int StaffId);

internal sealed record VietAnLoginRequest(string UserName, string Password, bool Remember = false);

internal sealed record ActAsCustomerRequest(long CustomerId);

/// <summary>Đúng mật khẩu → đã gửi mã OTP: <see cref="ChallengeId"/> gửi kèm mã ở bước 2, <see cref="Email"/> đã che bớt.</summary>
internal sealed record VietAnOtpChallengeDto(string ChallengeId, string Email, int ExpiresInSeconds);

/// <summary>Bước 1 đăng nhập: có <see cref="Session"/> = hôm nay đã nhập OTP trên trình duyệt này, ngược lại <see cref="Challenge"/> chờ nhập mã.</summary>
internal sealed record VietAnLoginOutcome(VietAnOtpChallengeDto? Challenge, AuthSession? Session = null);

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

    public static readonly Error CannotAssignManager = Error.Forbidden("VIETAN_CANNOT_ASSIGN",
        "Chỉ quản trị / CS được gán nhân viên phụ trách");

    public static readonly Error AssignFailed = Error.BusinessRule("VIETAN_ASSIGN_FAILED",
        "Không gán được — khách không còn là khách mới (CS đã hoàn thiện ở CMS) hoặc nhân viên không thuộc sales / CS");

    public static readonly Error CustomerOutOfScope = Error.Forbidden("CUSTOMER_OUT_OF_SCOPE",
        "Không tìm thấy khách hoặc khách không do bạn phụ trách");
}
