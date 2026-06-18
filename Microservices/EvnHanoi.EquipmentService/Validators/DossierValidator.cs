using FluentValidation;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Validators;

public class DossierValidator : AbstractValidator<Dossier>
{
    public DossierValidator()
    {
        RuleFor(d => d.DossierTypeId)
            .NotEmpty().WithMessage("Loại hồ sơ không được để trống.");

        RuleFor(d => d.Status)
            .NotEmpty().WithMessage("Trạng thái hồ sơ không được để trống.");
    }
}
