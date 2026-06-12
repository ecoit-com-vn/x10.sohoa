using FluentValidation;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Validators;

public class RoleValidator : AbstractValidator<Role>
{
    public RoleValidator()
    {
        RuleFor(r => r.Code)
            .NotEmpty().WithMessage("Mã vai trò không được để trống.");
        RuleFor(r => r.Name)
            .NotEmpty().WithMessage("Tên vai trò không được để trống.");
    }
}
