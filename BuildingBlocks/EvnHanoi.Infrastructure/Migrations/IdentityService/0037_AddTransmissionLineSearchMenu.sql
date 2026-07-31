-- Menu Tìm kiếm đường dây và quyền SEARCH_TRANSMISSION_LINE_VIEW
-- Menu này nằm trong menu cha là "Tra cứu tìm kiếm" (nếu có), cạnh menu Trạm biến áp

INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Tra cứu tìm kiếm Đường dây',
       '/search/transmission-line',
       'pi pi-sitemap',
       (SELECT Id FROM APP_MENU WHERE Name = N'Tra cứu tìm kiếm' AND ROWNUM = 1),
       10,
       1,
       'SEARCH_TRANSMISSION_LINE_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/search/transmission-line'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'search_transmission_line_view_id',
       'SEARCH_TRANSMISSION_LINE_VIEW',
       N'Tra cứu tìm kiếm Đường dây',
       N'Tự động sinh: Quyền tra cứu tìm kiếm đường dây',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SEARCH_TRANSMISSION_LINE_VIEW'
);

INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_TRANSMISSION_LINE_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION_GROUP_PERMISSION
    WHERE PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_TRANSMISSION_LINE_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_TRANSMISSION_LINE_VIEW' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
