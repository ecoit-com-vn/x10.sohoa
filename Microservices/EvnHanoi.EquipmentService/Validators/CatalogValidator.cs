using FluentValidation;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Validators;

public class CatalogValidator : AbstractValidator<Catalog>
{
    public CatalogValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty().WithMessage("Mã danh mục không được để trống.");
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.");
    }
}
