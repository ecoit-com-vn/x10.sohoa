-- Menu configuration for external API keys. It is intentionally assigned only to the ADMIN permission group.

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'external_api_key_view_id',
       'EXTERNAL_API_KEY_VIEW',
       N'Cấu hình API',
       N'Xem và quản lý API key tích hợp ngoài hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'EXTERNAL_API_KEY_VIEW'
);

INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       (SELECT Id FROM PERMISSION WHERE Code = 'EXTERNAL_API_KEY_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1
    FROM PERMISSION_GROUP_PERMISSION
    WHERE PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'EXTERNAL_API_KEY_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'EXTERNAL_API_KEY_VIEW' AND ROWNUM = 1) IS NOT NULL;

INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Cấu hình API',
       '/administration/external-api-keys',
       'pi pi-key',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       99,
       1,
       'EXTERNAL_API_KEY_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/administration/external-api-keys'
);

COMMIT;
