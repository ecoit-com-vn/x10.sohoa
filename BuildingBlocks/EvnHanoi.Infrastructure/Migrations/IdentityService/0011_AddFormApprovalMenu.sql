-- Migration to insert menu "Phê duyệt biểu mẫu" (Id = 52) under parent "Hồ sơ & Thiết bị" (Id = 12)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 52, 'Phê duyệt biểu mẫu', '/equipment/form-approval', 'pi pi-check-square', 12, 2, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 52);

COMMIT;
