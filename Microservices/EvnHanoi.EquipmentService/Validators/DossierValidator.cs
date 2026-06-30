using FluentValidation;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Validators;

public class DossierValidator : AbstractValidator<Dossier>
{
    public DossierValidator()
    {
        RuleFor(d => d.DossierTypeId)
            .NotEmpty().WithMessage("Loại hồ sơ không được để trống.");

        RuleFor(d => d.StatusId)
            .GreaterThan(0).WithMessage("Trạng thái hồ sơ không hợp lệ.");
    }
}
