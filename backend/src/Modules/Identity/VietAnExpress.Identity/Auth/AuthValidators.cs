using FluentValidation;
using VietAnExpress.Identity.Shared;
using VietAnExpress.Identity.Staff;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Auth;

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
