-- Menu configuration for external API call log history. Assigned to the ADMIN permission group
-- so that any role/user linked to it (including the seeded 'admin' account) can see the menu.

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'external_api_log_view_id',
       'EXTERNAL_API_CALL_LOG_VIEW',
       N'Lịch sử đồng bộ API',
       N'Xem lịch sử các lượt gọi API tích hợp ngoài hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'EXTERNAL_API_CALL_LOG_VIEW'
);

INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       (SELECT Id FROM PERMISSION WHERE Code = 'EXTERNAL_API_CALL_LOG_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM PERMISSION_GROUP_PERMISSION
    WHERE PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'EXTERNAL_API_CALL_LOG_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'EXTERNAL_API_CALL_LOG_VIEW' AND ROWNUM = 1) IS NOT NULL;

INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Lịch sử đồng bộ API',
       '/administration/external-api-key-history',
       'pi pi-history',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       100,
       1,
       'EXTERNAL_API_CALL_LOG_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/administration/external-api-key-history'
);

COMMIT;
