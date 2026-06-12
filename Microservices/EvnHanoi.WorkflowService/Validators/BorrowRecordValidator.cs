using FluentValidation;
using EvnHanoi.WorkflowService.Models;

namespace EvnHanoi.WorkflowService.Validators;

public class BorrowRecordValidator : AbstractValidator<BorrowRecord>
{
    public BorrowRecordValidator()
    {
        RuleFor(b => b.DossierId)
            .NotEmpty().WithMessage("Mã hồ sơ không được để trống.");
        RuleFor(b => b.RequesterId)
            .NotEmpty().WithMessage("Mã người yêu cầu không được để trống.");
        RuleFor(b => b.Reason)
            .NotEmpty().WithMessage("Lý do mượn không được để trống.");
    }
}
