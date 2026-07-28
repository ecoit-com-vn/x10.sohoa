-- Menu Danh mục mục lục hồ sơ. Idempotent theo URL để không tạo trùng.
UPDATE APP_MENU
   SET Name = 'Danh mục mục lục hồ sơ',
       PermissionCode = 'CATALOG_VIEW',
       IsActive = 1
 WHERE Url = '/catalog/muc-luc-ho-so';

INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 'Danh mục mục lục hồ sơ',
       '/catalog/muc-luc-ho-so',
       'pi pi-list',
       COALESCE(
           (SELECT MIN(Id) FROM APP_MENU WHERE Name = 'Quản lý danh mục'),
           (SELECT MIN(Id) FROM APP_MENU WHERE Name = 'Danh mục hệ thống'),
           10
       ),
       2,
       1,
       'CATALOG_VIEW'
  FROM DUAL
 WHERE NOT EXISTS (
       SELECT 1 FROM APP_MENU WHERE Url = '/catalog/muc-luc-ho-so'
 );

COMMIT;
