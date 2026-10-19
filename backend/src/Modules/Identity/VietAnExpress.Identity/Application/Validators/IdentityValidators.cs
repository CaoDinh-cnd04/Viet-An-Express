using FluentValidation;
using VietAnExpress.Identity.Application.Commands;
using VietAnExpress.Identity.Domain;

namespace VietAnExpress.Identity.Application.Validators;

internal static class PasswordPolicy
{
    /// <summary>Tối thiểu 8 ký tự, có cả chữ và số — ĐỒNG BỘ với frontend (ChangePasswordPage, PASSWORD_RULE).</summary>
    public const string Pattern = @"^(?=.*[A-Za-z])(?=.*\d).{8,}$";
    public const string Message = "Mật khẩu tối thiểu 8 ký tự, gồm cả chữ và số";

    /// <summary>Cột dbo.TCustomer.Login_Password là nvarchar(150).</summary>
    public const int MaxLength = 150;

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Nhập mật khẩu")
            .MaximumLength(MaxLength)
            .Matches(Pattern).WithMessage(Message);
}

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Nhập mã khách hàng").MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Nhập mật khẩu").MaximumLength(PasswordPolicy.MaxLength);
    }
}

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Nhập mật khẩu hiện tại");
        RuleFor(x => x.NewPassword).StrongPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại");
    }
}

internal sealed class CreateStaffCommandValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().WithMessage("Nhập tên đăng nhập")
            .Must(v => v.Trim().Length is >= StaffAccount.UserNameMinLength and <= StaffAccount.UserNameMaxLength)
            .WithMessage($"Tên đăng nhập từ {StaffAccount.UserNameMinLength} đến {StaffAccount.UserNameMaxLength} ký tự")
            .Must(v => System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), StaffAccount.UserNamePattern))
            .WithMessage("Tên đăng nhập chỉ gồm chữ không dấu, số và . _ - @");
        RuleFor(x => x.Password).StrongPassword();
        this.StaffProfileRules(x => x.FullName, x => x.Email, x => x.Phone, x => x.Permissions);
        this.SecondContactRules(x => x.Email2, x => x.Phone2);
    }
}

internal sealed class UpdateStaffCommandValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffCommandValidator()
    {
        this.StaffProfileRules(x => x.FullName, x => x.Email, x => x.Phone, x => x.Permissions);
        this.SecondContactRules(x => x.Email2, x => x.Phone2);
    }
}

internal sealed class ResetStaffPasswordCommandValidator : AbstractValidator<ResetStaffPasswordCommand>
{
    public ResetStaffPasswordCommandValidator() => RuleFor(x => x.NewPassword).StrongPassword();
}

internal static class StaffRules
{
    public static void StaffProfileRules<T>(this AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, string>> fullName,
        System.Linq.Expressions.Expression<Func<T, string?>> email,
        System.Linq.Expressions.Expression<Func<T, string?>> phone,
        System.Linq.Expressions.Expression<Func<T, IReadOnlyList<string>>> permissions)
    {
        v.RuleFor(fullName).NotEmpty().WithMessage("Nhập họ tên nhân viên").MaximumLength(StaffAccount.FullNameMaxLength);
        v.RuleFor(email).MaximumLength(StaffAccount.EmailMaxLength)
            .Must(e => string.IsNullOrWhiteSpace(e) || System.Text.RegularExpressions.Regex.IsMatch(e.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            .WithMessage("Email không hợp lệ");
        v.RuleFor(phone).MaximumLength(StaffAccount.PhoneMaxLength);
        v.RuleFor(permissions).NotNull();
    }

    /// <summary>Email 2 / di động 2 (Email2, Di_Dong2) — không bắt buộc, cùng luật với email / SĐT chính.</summary>
    public static void SecondContactRules<T>(this AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, string?>> email2,
        System.Linq.Expressions.Expression<Func<T, string?>> phone2)
    {
        v.RuleFor(email2).MaximumLength(StaffAccount.EmailMaxLength)
            .Must(e => string.IsNullOrWhiteSpace(e) || System.Text.RegularExpressions.Regex.IsMatch(e.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            .WithMessage("Email 2 không hợp lệ");
        v.RuleFor(phone2).MaximumLength(StaffAccount.PhoneMaxLength);
    }
}

internal sealed class SaveMyTrackingCommandValidator : AbstractValidator<SaveMyTrackingCommand>
{
    public SaveMyTrackingCommandValidator() =>
        RuleFor(x => x.Slug).Must(s => MyTrackingPage.IsValidSlug(s.Trim().ToLowerInvariant()))
            .WithMessage($"Đường dẫn từ {MyTrackingPage.SlugMinLength} đến {MyTrackingPage.SlugMaxLength} ký tự, chỉ gồm chữ thường không dấu, số và dấu gạch ngang");
}
