-- Clean up catalog sub-menus first to support updates
DELETE FROM APP_MENU WHERE Id IN (11, 26, 27, 28, 29, 30, 31, 32, 33);


-- Insert 9 dynamic sub-menus under parent menu 'Danh mục hệ thống' (Id = 10)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 11, 'Phông', '/catalog/phong', 'pi pi-folder', 10, 1, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 11);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 26, 'Mục lục hồ sơ', '/catalog/muc-luc', 'pi pi-list', 10, 2, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 26);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 27, 'Loại hồ sơ', '/catalog/loai-ho-so', 'pi pi-file', 10, 3, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 27);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 28, 'Kệ hồ sơ', '/catalog/ke', 'pi pi-database', 10, 4, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 28);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 29, 'Tầng hồ sơ', '/catalog/tang', 'pi pi-align-justify', 10, 5, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 29);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 30, 'Hộp hồ sơ', '/catalog/hop', 'pi pi-box', 10, 6, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 30);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 31, 'Chức vụ', '/catalog/chuc-vu', 'pi pi-user', 10, 7, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 31);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 32, 'Lĩnh vực', '/catalog/linh-vuc', 'pi pi-globe', 10, 8, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 32);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 33, 'Tình trạng vật lý', '/catalog/tinh-trang-vat-ly', 'pi pi-heart-fill', 10, 9, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 33);

COMMIT;
