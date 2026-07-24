using EvnHanoi.EquipmentService.Core.Entities;
using FluentValidation;

namespace EvnHanoi.EquipmentService.Validators;

public class CatalogValidator : AbstractValidator<Catalog>
{
    public CatalogValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã danh mục là bắt buộc")
            .MaximumLength(50).WithMessage("Mã danh mục không được vượt quá 50 ký tự");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục là bắt buộc")
            .MaximumLength(255).WithMessage("Tên danh mục không được vượt quá 255 ký tự");

        RuleFor(x => x.CatalogTypeId)
            .GreaterThan(0).WithMessage("Loại danh mục không hợp lệ");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Mô tả không được vượt quá 1000 ký tự")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
