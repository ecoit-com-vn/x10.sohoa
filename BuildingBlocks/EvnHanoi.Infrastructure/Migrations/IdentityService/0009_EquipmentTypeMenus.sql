-- Migration to insert new parent menu "Quản lý thiết bị" (Id = 49) and sub-menu "Loại thiết bị" (Id = 50) under it

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 49, 'Quản lý thiết bị', NULL, 'pi pi-server', NULL, 5, 1, NULL FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 49);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 50, 'Loại thiết bị', '/equipment/equipment-type', 'pi pi-clone', 49, 1, 1, 'EQUIPMENT_TYPE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 50);

COMMIT;
