using EvnHanoi.IdentityService.Controllers;
using FluentValidation;

namespace EvnHanoi.IdentityService.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Trường dữ liệu này không được để trống.")
            .MaximumLength(255)
            .WithMessage("Họ và tên không được vượt quá 255 ký tự.");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Trường dữ liệu này không được để trống.")
            .MaximumLength(100)
            .WithMessage("Email không được vượt quá 100 ký tự.")
            .EmailAddress()
            .WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.PositionName)
            .MaximumLength(255)
            .WithMessage("Chức vụ không được vượt quá 255 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.PositionName));
    }
}
