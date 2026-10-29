using FluentValidation;
using VietAnExpress.Identity.Notifications;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.SharedKernel.Application;
using VietAnExpress.SharedKernel.Results;

namespace VietAnExpress.Identity.Support;

internal sealed class PublicContactCommandValidator : AbstractValidator<PublicContactCommand>
{
    public const int NameMaxLength = 100;
    public const int MessageMinLength = 10;
    public const int MessageMaxLength = 2000;
    /// <summary>9–15 ký tự: chữ số, +, khoảng trắng, dấu chấm, gạch nối (như frontend).</summary>
    public const string PhonePattern = @"^\+?[\d\s.-]{9,15}$";

    public PublicContactCommandValidator()
    {
        RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Nhập họ tên")
            .Must(v => (v?.Trim().Length ?? 0) <= NameMaxLength).WithMessage($"Họ tên tối đa {NameMaxLength} ký tự");
        RuleFor(x => x.Phone).Must(v => v is not null && System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), PhonePattern))
            .WithMessage("Số điện thoại không hợp lệ");
        RuleFor(x => x.Email).Must(v => string.IsNullOrWhiteSpace(v) || SupportRules.IsEmail(v)).WithMessage("Email không hợp lệ");
        RuleFor(x => x.Message).Must(v => (v?.Trim().Length ?? 0) is >= MessageMinLength and <= MessageMaxLength)
            .WithMessage($"Nội dung từ {MessageMinLength} đến {MessageMaxLength} ký tự");
    }
}
