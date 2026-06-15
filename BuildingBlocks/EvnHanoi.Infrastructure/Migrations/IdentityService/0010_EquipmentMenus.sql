-- Migration to insert sub-menu "Quản lý thiết bị" (Id = 51) under parent "Quản lý thiết bị" (Id = 49)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 51, 'Quản lý thiết bị', '/equipment/list', 'pi pi-cog', 49, 2, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 51);

COMMIT;
