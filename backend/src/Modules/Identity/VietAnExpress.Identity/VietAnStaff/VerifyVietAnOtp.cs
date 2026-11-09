using MediatR;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Bước 2 đăng nhập nhân viên Việt An: nhập mã OTP đã gửi email → cấp phiên (chưa gắn khách).
/// Sai mã tính vào khóa tạm của tên đăng nhập như sai mật khẩu; sai 5 lần với 1 mã thì mã hủy, phải đăng nhập lại.
/// </summary>
internal sealed record VerifyVietAnOtpCommand(string ChallengeId, string Code) : IRequest<Result<AuthSession>>;

internal sealed class VerifyVietAnOtpHandler(
    ICustomersApi customers, SessionService sessions, LoginAttemptGuard guard, VietAnOtpStore otps, VietAnOtpDayPass dayPass, VietAnSessionRegistry? activeSessions = null)
    : IRequestHandler<VerifyVietAnOtpCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(VerifyVietAnOtpCommand cmd, CancellationToken ct)
    {
        var (outcome, user) = otps.Verify(cmd.ChallengeId, cmd.Code);
        if (user is not null && guard.LockedFor(VietAnOtp.GuardKey(user.UserName)) is { } locked) return AuthErrors.LoginLocked(locked);
        switch (outcome)
        {
            case VietAnOtpStore.Outcome.Expired:
                return VietAnStaffErrors.OtpExpired;
            case VietAnOtpStore.Outcome.Wrong:
                guard.Failed(VietAnOtp.GuardKey(user!.UserName));
                return VietAnStaffErrors.OtpWrong;
        }

        // Đọc lại nhân viên: trong lúc chờ mã có thể đã bị khóa / đổi mật khẩu trên CMS.
        if (await customers.GetVietAnUserAsync(user!.UserId, ct) is not { } agent) return AuthErrors.InvalidCredentials;
        guard.Succeeded(VietAnOtp.GuardKey(user.UserName));
        // 1 phiên / nhân viên: mã phiên mới → máy đang đăng nhập trước đó bị đá ra.
        activeSessions?.Renew(agent.Id);
        return sessions.Start(agent, null, user.Remember) with { OtpDayPass = dayPass.Issue(agent) };
    }
}
