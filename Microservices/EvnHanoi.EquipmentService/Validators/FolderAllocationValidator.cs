using FluentValidation;
using EvnHanoi.EquipmentService.Core.DTOs;

namespace EvnHanoi.EquipmentService.Validators;

public class CreateFolderAllocationRequestValidator : AbstractValidator<CreateFolderAllocationRequest>
{
    public CreateFolderAllocationRequestValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Thư mục phân bổ không được để trống.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Người xử lý không được để trống.");
    }
}

public class UpdateFolderAllocationRequestValidator : AbstractValidator<UpdateFolderAllocationRequest>
{
    public UpdateFolderAllocationRequestValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Thư mục phân bổ không được để trống.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Người xử lý không được để trống.");
    }
}
