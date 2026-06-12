using FluentValidation;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Validators;

public class DossierValidator : AbstractValidator<Dossier>
{
    public DossierValidator()
    {
        RuleFor(d => d.Title)
            .NotEmpty().WithMessage("Tiêu đề hồ sơ không được để trống.");
    }
}
