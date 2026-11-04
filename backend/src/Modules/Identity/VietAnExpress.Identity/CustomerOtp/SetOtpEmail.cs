using MediatR;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.CustomerOtp;

/// <summary>
/// Lần đăng nhập đầu (sau khi đúng mật khẩu, <see cref="CustomerOtpChallengeDto.NeedEmail"/>): khách nhập email nhận mã + đồng ý xử lý
/// dữ liệu cá nhân → gửi mã xác nhận tới chính email đó. Email chỉ được lưu (ghi đè cột Email) khi nhập đúng mã ở <see cref="VerifyCustomerOtpCommand"/>.
/// </summary>
internal sealed record SetOtpEmailCommand(string ChallengeId, string Email, bool AcceptTerms) : IRequest<Result<CustomerOtpChallengeDto>>;

internal sealed class SetOtpEmailHandler(CustomerOtpStore otps, CustomerOtpMailer mailer, TimeProvider clock)
    : IRequestHandler<SetOtpEmailCommand, Result<CustomerOtpChallengeDto>>
{
    public async Task<Result<CustomerOtpChallengeDto>> Handle(SetOtpEmailCommand cmd, CancellationToken ct)
    {
        if (otps.Peek(cmd.ChallengeId) is not { } pending) return CustomerOtpErrors.Expired;
        if (!cmd.AcceptTerms) return CustomerOtpErrors.ConsentRequired;
        // Chỉ nhận đúng 1 địa chỉ (không danh sách), bỏ email @vietanexpress.com.
        var email = cmd.Email?.Trim() ?? "";
        if (email.Length > 150 || CustomerOtpRules.Recipient(email) != email) return CustomerOtpErrors.InvalidEmail;

        var next = pending with { NewEmail = email, ConsentAt = clock.GetUtcNow() };
        return await mailer.SendAsync(next, email, pending.UserName, ct) is { } challenge ? challenge : CustomerOtpErrors.EmailFailed;
    }
}
