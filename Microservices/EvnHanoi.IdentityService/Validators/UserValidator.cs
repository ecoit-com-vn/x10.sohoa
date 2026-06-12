using FluentValidation;
using EvnHanoi.IdentityService.Core.Domain.Models;

namespace EvnHanoi.IdentityService.Validators;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(u => u.Username)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.");
        RuleFor(u => u.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.");
        RuleFor(u => u.OrganizationUnitId)
            .NotNull().WithMessage("Đơn vị thành viên không được để trống.")
            .GreaterThan(0).WithMessage("Đơn vị thành viên không hợp lệ.");
    }
}
