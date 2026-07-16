-- Menu Tìm kiếm trạm biến áp và quyền SEARCH_SUBSTATION_VIEW
-- Menu này dự kiến nằm trong menu cha là "Tra cứu tìm kiếm" (nếu có)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 60,
       N'Tra cứu tìm kiếm Trạm biến áp',
       '/search/substation',
       'pi pi-map-marker',
       (SELECT Id FROM APP_MENU WHERE Name = N'Tra cứu tìm kiếm' AND ROWNUM = 1),
       9,
       1,
       'SEARCH_SUBSTATION_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/search/substation'
);

-- Nếu chưa có menu cha "Tra cứu tìm kiếm" thì ParentId sẽ là NULL.
-- Nếu sau này menu cha được tạo, nó sẽ tự động nhận giá trị qua tên.

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'search_substation_view_id',
       'SEARCH_SUBSTATION_VIEW',
       N'Tra cứu tìm kiếm Trạm biến áp',
       N'Tự động sinh: Quyền tra cứu tìm kiếm trạm biến áp',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SEARCH_SUBSTATION_VIEW'
);

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId)
SELECT SYS_GUID(), 
       (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1), 
       (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_SUBSTATION_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM ROLE_PERMISSION
    WHERE RoleId = (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_SUBSTATION_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_SUBSTATION_VIEW' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
