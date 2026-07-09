-- Tách namespace phân quyền biểu mẫu EAV theo controller:
-- EavFormTemplateController → EAV_FORM_TEMPLATE_*
-- EavFormApprovalController → EAV_FORM_APPROVAL_*
-- EavCompletedFormController → EAV_COMPLETED_FORM_*

UPDATE APP_MENU
SET PermissionCode = 'EAV_FORM_TEMPLATE_VIEW'
WHERE Url = '/equipment/form-management';

UPDATE APP_MENU
SET PermissionCode = 'EAV_FORM_APPROVAL_VIEW'
WHERE Url = '/equipment/form-approval';

UPDATE APP_MENU
SET PermissionCode = 'EAV_COMPLETED_FORM_VIEW'
WHERE Url = '/equipment/completed-forms';

UPDATE PERMISSION
SET Code = 'EAV_FORM_APPROVAL_VIEW',
    Name = N'Xem hàng chờ phê duyệt biểu mẫu',
    Description = N'Tự động sinh: Cho phép xem hàng chờ phê duyệt biểu mẫu EAV'
WHERE Code = 'EAV_FORM_TEMPLATE_APPROVAL_VIEW';

UPDATE PERMISSION
SET Code = 'EAV_FORM_APPROVAL_APPROVE',
    Name = N'Phê duyệt / từ chối biểu mẫu',
    Description = N'Tự động sinh: Cho phép phê duyệt hoặc từ chối biểu mẫu EAV'
WHERE Code = 'EAV_FORM_TEMPLATE_APPROVE';

UPDATE PERMISSION
SET Code = 'EAV_COMPLETED_FORM_VIEW',
    Name = N'Xem danh sách form hoàn thành',
    Description = N'Tự động sinh: Cho phép xem danh sách biểu mẫu EAV hoàn thành'
WHERE Code = 'EAV_FORM_TEMPLATE_COMPLETED_VIEW';

UPDATE PERMISSION
SET Code = 'EAV_COMPLETED_FORM_MANAGE',
    Name = N'Khóa / mở khóa biểu mẫu hoàn thành',
    Description = N'Tự động sinh: Cho phép khóa hoặc mở khóa biểu mẫu EAV hoàn thành'
WHERE Code = 'EAV_FORM_TEMPLATE_MANAGE';

COMMIT;
