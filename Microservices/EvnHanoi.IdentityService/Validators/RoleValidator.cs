using FluentValidation;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Validators;

public class RoleValidator : AbstractValidator<Role>
{
    public RoleValidator()
    {
        RuleFor(r => r.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mã vai trò không được để trống.")
            .MaximumLength(50).WithMessage("Mã vai trò không được vượt quá 50 ký tự.")
            .Matches("^[A-Za-z0-9_]+$")
            .WithMessage("Mã vai trò chỉ được chứa chữ cái không dấu, chữ số và dấu gạch dưới; không được chứa khoảng trắng.");
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Tên vai trò không được để trống.");
    }
}
