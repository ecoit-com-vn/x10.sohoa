-- Migration to insert "Quản lý trạm biến áp" and "Quản lý đường dây" menus under parent menu "Quản trị hệ thống" (Id = 2)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 47, 'Quản lý trạm biến áp', '/catalog/substation', 'pi pi-map-marker', 2, 9, 1, 'SUBSTATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 47);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 48, 'Quản lý đường dây', '/catalog/transmission-line', 'pi pi-share-alt', 2, 10, 1, 'TRANSMISSION_LINE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 48);

COMMIT;
