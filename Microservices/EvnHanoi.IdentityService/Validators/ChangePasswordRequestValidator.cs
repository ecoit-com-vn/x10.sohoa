using EvnHanoi.IdentityService.Controllers;
using FluentValidation;

namespace EvnHanoi.IdentityService.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Trường dữ liệu này không được để trống.");

        RuleFor(x => x.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Trường dữ liệu này không được để trống.")
            .MinimumLength(8)
            .WithMessage("Mật khẩu mới phải có tối thiểu 8 ký tự.")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).+$")
            .WithMessage("Mật khẩu mới phải bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt.")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("Mật khẩu mới không được trùng mật khẩu hiện tại.");

        RuleFor(x => x.ConfirmPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Trường dữ liệu này không được để trống.")
            .Equal(x => x.NewPassword)
            .WithMessage("Xác nhận mật khẩu mới không khớp.");
    }
}
