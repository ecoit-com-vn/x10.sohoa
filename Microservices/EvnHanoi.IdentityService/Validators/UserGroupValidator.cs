using FluentValidation;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Validators;

public class UserGroupValidator : AbstractValidator<UserGroup>
{
    public UserGroupValidator()
    {
        RuleFor(ug => ug.Name)
            .NotEmpty().WithMessage("Tên nhóm người dùng không được để trống.");
    }
}
