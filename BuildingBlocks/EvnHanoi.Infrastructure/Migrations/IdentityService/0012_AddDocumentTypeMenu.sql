-- Migration to insert "Danh mục loại văn bản" menu under "Danh mục hồ sơ" (Id = 45)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 53, 'Danh mục loại văn bản', '/catalog/document-type', 'pi pi-file-edit', 45, 2, 1, 'DOCUMENT_TYPE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 53);

COMMIT;
