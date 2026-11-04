using MediatR;
using Microsoft.EntityFrameworkCore;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Shared;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.CustomerOtp;

/// <summary>
/// Bước 2 đăng nhập khách: nhập mã OTP đã gửi email → cấp phiên. Đọc lại tài khoản (trong lúc chờ mã có thể đã đổi mật khẩu / khóa).
/// Đang xác nhận email lần đầu (<see cref="CustomerOtpStore.Pending.NewEmail"/>): đúng mã thì ghi đè cột Email (TCustomer / TCustomer_NguoiLienLac)
/// và ghi sổ đã xác nhận + đồng ý (<see cref="OtpEmailRegistry"/>).
/// Sai mã tính vào khóa tạm của tên đăng nhập như sai mật khẩu; sai 5 lần với 1 mã thì mã hủy, phải đăng nhập lại.
/// </summary>
internal sealed record VerifyCustomerOtpCommand(string ChallengeId, string Code) : IRequest<Result<AuthSession>>;

internal sealed class VerifyCustomerOtpHandler(
    IdentityDbContext db, SessionService sessions, LoginAttemptGuard guard, CustomerOtpStore otps, OtpEmailRegistry registry)
    : IRequestHandler<VerifyCustomerOtpCommand, Result<AuthSession>>
{
    public async Task<Result<AuthSession>> Handle(VerifyCustomerOtpCommand cmd, CancellationToken ct)
    {
        var (outcome, pending) = otps.Verify(cmd.ChallengeId, cmd.Code);
        if (pending is not null && guard.LockedFor(pending.UserName) is { } locked) return AuthErrors.LoginLocked(locked);
        switch (outcome)
        {
            case CustomerOtpStore.Outcome.Expired:
                return CustomerOtpErrors.Expired;
            case CustomerOtpStore.Outcome.Wrong:
                guard.Failed(pending!.UserName);
                return CustomerOtpErrors.Wrong;
        }

        AuthSession? session;
        if (pending!.StaffId is { } staffId)
        {
            var staff = await db.StaffAccounts.FirstOrDefaultAsync(s => s.Id == staffId && s.CustomerId == pending.CustomerId, ct);
            if (staff is null || !staff.IsActive) return AuthErrors.InvalidCredentials;
            if (pending.NewEmail is { } email) staff.SetEmail(email);
            session = await sessions.StartAsync(staff, pending.Remember, ct);
        }
        else
        {
            var login = await db.Logins.FirstOrDefaultAsync(l => l.CustomerId == pending.CustomerId, ct);
            if (login is not null && pending.NewEmail is { } email) login.SetEmail(email);
            session = login is null ? null : await sessions.StartAsync(login, pending.Remember, ct);
        }
        if (session is null) return AuthErrors.InvalidCredentials;

        if (pending.NewEmail is { } confirmed)
        {
            await db.SaveChangesAsync(ct);
            registry.Confirm(OtpEmailRegistry.Key(pending.CustomerId, pending.StaffId), confirmed,
                pending.ConsentAt ?? DateTimeOffset.UtcNow, CustomerOtpRules.ConsentVersion);
        }
        guard.Succeeded(pending.UserName);
        return session;
    }
}
