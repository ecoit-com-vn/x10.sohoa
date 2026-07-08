-- Migration: Thêm quyền xuất nhật ký hệ thống AUDIT_LOG_EXPORT

INSERT INTO PERMISSION (Id, Code, Name, Description, ServiceName)
SELECT 'audit_log_export_id',
       'AUDIT_LOG_EXPORT',
       N'Xuất nhật ký hệ thống',
       N'Tự động sinh: Xuất file Excel/CSV nhật ký audit',
       'NotificationService'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'AUDIT_LOG_EXPORT'
);

INSERT INTO ROLE_PERMISSION (RoleId, PermissionId)
SELECT (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1), 'audit_log_export_id'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM ROLE_PERMISSION
    WHERE RoleId = (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = 'audit_log_export_id'
)
AND (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
