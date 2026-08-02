-- Menu "Tài liệu đang số hóa" (giám sát job OCR/bóc tách toàn hệ thống) trong nhóm "Số hóa hồ sơ"

INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Tài liệu đang số hóa',
       '/digitization/ocr-jobs',
       'pi pi-history',
       (SELECT Id FROM APP_MENU WHERE Name = N'Số hóa hồ sơ' AND ROWNUM = 1),
       7,
       1,
       'OCR_JOBS_MONITOR_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/digitization/ocr-jobs'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'ocr_jobs_monitor_view_id',
       'OCR_JOBS_MONITOR_VIEW',
       N'Giám sát job OCR/bóc tách toàn hệ thống',
       N'Tự động sinh: quyền xem màn hình giám sát job OCR/bóc tách',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'OCR_JOBS_MONITOR_VIEW'
);

INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       (SELECT Id FROM PERMISSION WHERE Code = 'OCR_JOBS_MONITOR_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION_GROUP_PERMISSION
    WHERE PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'OCR_JOBS_MONITOR_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'OCR_JOBS_MONITOR_VIEW' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
