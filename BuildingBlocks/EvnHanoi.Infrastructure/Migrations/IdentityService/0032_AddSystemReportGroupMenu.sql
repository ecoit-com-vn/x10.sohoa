-- Menu Nhóm báo cáo hệ thống và quyền REPORT_GROUP_VIEW
-- Menu này là menu con của menu cha "Báo cáo & Thống kê" (Id = 24), SortOrder = 1 để đưa lên ưu tiên 1

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 90,
       N'Nhóm báo cáo hệ thống',
       '/reports/groups',
       'pi pi-list',
       24,
       1,
       1,
       'REPORT_GROUP_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/reports/groups'
);

-- Thêm quyền REPORT_GROUP_VIEW
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'report_group_view_id',
       'REPORT_GROUP_VIEW',
       N'Xem nhóm báo cáo hệ thống',
       N'Tự động sinh: Quyền xem cấu hình nhóm báo cáo hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'REPORT_GROUP_VIEW'
);

-- Gán quyền cho ADMIN
INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(), 
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1), 
       (SELECT Id FROM PERMISSION WHERE Code = 'REPORT_GROUP_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION_GROUP_PERMISSION
    WHERE PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'REPORT_GROUP_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'REPORT_GROUP_VIEW' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
