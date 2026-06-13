-- Clean up catalog sub-menus first to support updates
DELETE FROM APP_MENU WHERE Id IN (11, 26, 27, 28, 29, 30, 31, 32, 33);


-- Insert 3 dynamic sub-menus under parent menu 'Danh mục hệ thống' (Id = 10)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 31, 'Chức vụ', '/catalog/chuc-vu', 'pi pi-user', 10, 1, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 31);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 32, 'Lĩnh vực', '/catalog/linh-vuc', 'pi pi-globe', 10, 2, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 32);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 33, 'Tình trạng vật lý', '/catalog/tinh-trang-vat-ly', 'pi pi-heart-fill', 10, 3, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 33);

COMMIT;
