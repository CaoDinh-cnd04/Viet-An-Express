using System.Globalization;
using VietAnExpress.Customers.Contracts;
using VietAnExpress.Identity.Auth;
using VietAnExpress.SharedKernel.Application;

namespace VietAnExpress.Identity.VietAnStaff;

/// <summary>
/// Nhân viên Việt An chỉ nhập OTP 1 lần mỗi ngày trên 1 trình duyệt (người dùng chốt 09/11/2026), đăng xuất rồi vào lại không hỏi mã.
/// Nhập đúng OTP → cookie <see cref="AuthCookies.OtpDayPass"/> = HMAC(ngày Việt Nam + nhân viên + mật khẩu), hết hạn lúc 24h giờ Việt Nam.
/// Không lưu ở máy chủ. Vẫn phải đúng mật khẩu; máy khác chưa có cookie vẫn phải nhập mã; đổi mật khẩu thì cookie mất hiệu lực.
/// </summary>
internal sealed class VietAnOtpDayPass(ITokenService tokens, TimeProvider clock)
{
    public IssuedToken Issue(VietAnUserLogin agent)
    {
        var today = Today();
        return new IssuedToken(Value(agent, today), VietnamTime.StartOfDayUtc(today.AddDays(1)));
    }

    public bool IsValid(string? pass, VietAnUserLogin agent) =>
        !string.IsNullOrEmpty(pass) && JwtTokenService.FixedTimeEquals(Value(agent, Today()), pass);

    private DateOnly Today() => DateOnly.FromDateTime(VietnamTime.Now(clock));

    private string Value(VietAnUserLogin agent, DateOnly day) =>
        tokens.VietAnPasswordStamp(agent.Id, $"otp-day:{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}:{agent.Password}");
}
