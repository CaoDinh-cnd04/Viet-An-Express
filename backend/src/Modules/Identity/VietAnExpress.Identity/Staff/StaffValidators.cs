using FluentValidation;
using VietAnExpress.Identity.Auth;
using VietAnExpress.Identity.Support;

namespace VietAnExpress.Identity.Staff;

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

/// <summary>Luật kiểm tra hồ sơ tài khoản con dùng chung cho validator tạo / sửa.</summary>
internal static class StaffValidationRules
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
