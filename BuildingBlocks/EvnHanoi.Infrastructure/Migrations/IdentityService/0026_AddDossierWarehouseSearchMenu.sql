-- Menu Tìm kiếm hồ sơ trong kho + quyền SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW

UPDATE APP_MENU
SET PermissionCode = 'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW',
    Url = '/search/dossier',
    Icon = NVL(Icon, 'pi pi-folder-open')
WHERE Url = '/search/dossier'
   OR Name = N'Tìm kiếm hồ sơ trong kho';

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 58,
       N'Tìm kiếm hồ sơ trong kho',
       '/search/dossier',
       'pi pi-folder-open',
       NULL,
       8,
       1,
       'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/search/dossier'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'search_dossier_warehouse_view_id',
       'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW',
       N'Tìm kiếm hồ sơ trong kho',
       N'Tự động sinh: Quyền tra cứu hồ sơ theo cây kho (đã xuất bản)',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW'
);

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId)
SELECT SYS_GUID(), 
       (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1), 
       (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM ROLE_PERMISSION
    WHERE RoleId = (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_IN_WAREHOUSE_VIEW' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
