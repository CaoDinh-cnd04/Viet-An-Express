using VietAnExpress.Identity.Auth;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Mã OTP đăng nhập của nhân viên Việt An: đúng mật khẩu → cấp mã 6 số gửi email (USERS.EMAIL1), nhập đúng mã mới cấp phiên.
/// Lưu / kiểm tra mã: <see cref="OtpStore{T}"/> (bộ nhớ, 5 phút, sai 5 lần hủy); mỗi nhân viên chỉ 1 mã còn hiệu lực.
/// </summary>
internal sealed class VietAnOtpStore(TimeProvider clock) : OtpStore<VietAnOtpStore.Verified>(clock)
{
    /// <summary>Nhân viên + "ghi nhớ" của lần nhập mật khẩu — dùng cấp phiên khi nhập đúng mã.</summary>
    public sealed record Verified(int UserId, string UserName, bool Remember);

    public (string ChallengeId, string Code) Issue(int userId, string userName, bool remember) =>
        IssueFor($"vietan:{userId}", new Verified(userId, userName, remember));
}
