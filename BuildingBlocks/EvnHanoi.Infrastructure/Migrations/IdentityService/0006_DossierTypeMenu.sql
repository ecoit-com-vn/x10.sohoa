-- Migration to insert "Danh mục loại hồ sơ" menu under "Danh mục hồ sơ" (Id = 45)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 46, 'Danh mục loại hồ sơ', '/catalog/dossier-type', 'pi pi-file', 45, 1, 1, 'DOSSIER_TYPE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 46);

COMMIT;
