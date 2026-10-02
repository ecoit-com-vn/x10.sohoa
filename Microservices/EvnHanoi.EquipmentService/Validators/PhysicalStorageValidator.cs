using EvnHanoi.EquipmentService.Core.Entities;
using FluentValidation;

namespace EvnHanoi.EquipmentService.Validators;

internal static class PhysicalStorageValidationRules
{
    internal const string StorageCodePattern = "^[A-Za-z0-9_-]{1,50}$";
    internal const string StorageCodeMessage =
        "Mã chỉ được gồm chữ cái không dấu, số, dấu gạch ngang (-), dấu gạch dưới (_), không được có dấu cách.";
}

public class PhysicalShelfValidator : AbstractValidator<PhysicalShelf>
{
    public PhysicalShelfValidator()
    {
        // Mã do hệ thống tự sinh (xem PhysicalStorageController.CreateShelf) — rỗng khi vừa gửi lên để
        // tạo mới, chỉ có giá trị khi sửa bản ghi đã tồn tại, nên không NotEmpty ở đây; vẫn kiểm tra
        // định dạng nếu có giá trị để không phá format khi sửa.
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(50).WithMessage("Mã kệ không được vượt quá 50 ký tự.")
            .Matches(PhysicalStorageValidationRules.StorageCodePattern)
            .WithMessage(PhysicalStorageValidationRules.StorageCodeMessage)
            .When(x => !string.IsNullOrEmpty(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên kệ không được để trống.");

        RuleFor(x => x.UnitId)
            .NotNull().WithMessage("Đơn vị không được để trống.")
            .GreaterThan(0).WithMessage("Đơn vị không hợp lệ.");
    }
}

public class PhysicalFloorValidator : AbstractValidator<PhysicalFloor>
{
    public PhysicalFloorValidator()
    {
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(50).WithMessage("Mã tầng không được vượt quá 50 ký tự.")
            .Matches(PhysicalStorageValidationRules.StorageCodePattern)
            .WithMessage(PhysicalStorageValidationRules.StorageCodeMessage)
            .When(x => !string.IsNullOrEmpty(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tầng không được để trống.");

        RuleFor(x => x.ShelfId)
            .GreaterThan(0).WithMessage("Kệ lưu trữ không hợp lệ.");
    }
}

public class PhysicalBoxValidator : AbstractValidator<PhysicalBox>
{
    public PhysicalBoxValidator()
    {
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(50).WithMessage("Mã hộp không được vượt quá 50 ký tự.")
            .Matches(PhysicalStorageValidationRules.StorageCodePattern)
            .WithMessage(PhysicalStorageValidationRules.StorageCodeMessage)
            .When(x => !string.IsNullOrEmpty(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên hộp không được để trống.");

        RuleFor(x => x.FloorId)
            .GreaterThan(0).WithMessage("Tầng kệ không hợp lệ.");
    }
}
