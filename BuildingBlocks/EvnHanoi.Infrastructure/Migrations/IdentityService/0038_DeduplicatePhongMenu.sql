-- Hợp nhất các menu Danh mục phông cũ/mới: giữ bản ghi nhỏ nhất làm menu chuẩn.
UPDATE APP_MENU
   SET IsActive = 0
 WHERE Id <> (
       SELECT MIN(Id) FROM APP_MENU
        WHERE LOWER(TRIM(Name)) = LOWER('Danh mục phông')
           OR LOWER(RTRIM(TRIM(Url), '/')) IN
              ('/catalog/phong','/catalog/fond','/catalog/fonds','/catalog/phong-ho-so','/catalog/danh-muc-phong')
 )
   AND (
       LOWER(TRIM(Name)) = LOWER('Danh mục phông')
       OR LOWER(RTRIM(TRIM(Url), '/')) IN
          ('/catalog/phong','/catalog/fond','/catalog/fonds','/catalog/phong-ho-so','/catalog/danh-muc-phong')
   );

UPDATE APP_MENU
   SET Name = 'Danh mục phông',
       Url = '/catalog/phong',
       PermissionCode = 'PHONG_VIEW',
       IsActive = 1,
       ParentId = COALESCE(
           (SELECT MIN(Id) FROM APP_MENU WHERE Name = 'Quản lý danh mục'),
           (SELECT MIN(Id) FROM APP_MENU WHERE Name = 'Danh mục hệ thống'),
           ParentId
       )
 WHERE Id = (
       SELECT MIN(Id) FROM APP_MENU
        WHERE LOWER(TRIM(Name)) = LOWER('Danh mục phông')
           OR LOWER(RTRIM(TRIM(Url), '/')) IN
              ('/catalog/phong','/catalog/fond','/catalog/fonds','/catalog/phong-ho-so','/catalog/danh-muc-phong')
 );

COMMIT;
