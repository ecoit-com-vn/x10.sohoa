-- Migration 0052: Thêm menu và quyền cho Thiết lập tham số hệ thống và các chức năng đồng bộ PMIS
-- Gán vào nhóm quyền ADMIN để hiển thị động trên thanh menu và quản lý được trong trang Quản lý menu.

-- 1. PERMISSIONS
-- 1.1. Thiết lập tham số hệ thống
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'system_param_view_id',
       'SYSTEM_PARAM_VIEW',
       N'Thiết lập tham số hệ thống',
       N'Xem và cấu hình các tham số hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SYSTEM_PARAM_VIEW'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'system_param_edit_id',
       'SYSTEM_PARAM_EDIT',
       N'Chỉnh sửa tham số hệ thống',
       N'Chỉnh sửa giá trị các tham số hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SYSTEM_PARAM_EDIT'
);

-- 1.2. Cấu hình kết nối PMIS
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_endpoint_config_view_id',
       'PMIS_ENDPOINT_CONFIG_VIEW',
       N'Cấu hình kết nối PMIS',
       N'Xem thông tin cấu hình kết nối API PMIS',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_ENDPOINT_CONFIG_VIEW'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_endpoint_config_edit_id',
       'PMIS_ENDPOINT_CONFIG_EDIT',
       N'Chỉnh sửa cấu hình kết nối PMIS',
       N'Chỉnh sửa endpoint, token cấu hình kết nối API PMIS',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_ENDPOINT_CONFIG_EDIT'
);

-- 1.3. Đồng bộ thủ công PMIS
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_manual_sync_view_id',
       'PMIS_MANUAL_SYNC_VIEW',
       N'Đồng bộ thủ công PMIS',
       N'Xem giao diện đồng bộ thủ công dữ liệu PMIS',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_MANUAL_SYNC_VIEW'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_manual_sync_create_id',
       'PMIS_MANUAL_SYNC_CREATE',
       N'Thực hiện đồng bộ thủ công PMIS',
       N'Kích hoạt tiến trình đồng bộ thủ công dữ liệu PMIS',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_MANUAL_SYNC_CREATE'
);

-- 1.4. Đồng bộ tự động PMIS
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'sync_schedule_view_id',
       'SYNC_SCHEDULE_VIEW',
       N'Đồng bộ tự động PMIS',
       N'Xem cấu hình và lịch sử đồng bộ tự động PMIS',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SYNC_SCHEDULE_VIEW'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'sync_schedule_edit_id',
       'SYNC_SCHEDULE_EDIT',
       N'Cấu hình lịch đồng bộ PMIS',
       N'Thiết lập bật/tắt và tần suất đồng bộ tự động PMIS',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SYNC_SCHEDULE_EDIT'
);

-- 2. GÁN QUYỀN VÀO NHÓM ADMIN (PERMISSION_GROUP = 'ADMIN')
INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       p.Id
FROM PERMISSION p
WHERE p.Code IN (
    'SYSTEM_PARAM_VIEW', 'SYSTEM_PARAM_EDIT',
    'PMIS_ENDPOINT_CONFIG_VIEW', 'PMIS_ENDPOINT_CONFIG_EDIT',
    'PMIS_MANUAL_SYNC_VIEW', 'PMIS_MANUAL_SYNC_CREATE',
    'SYNC_SCHEDULE_VIEW', 'SYNC_SCHEDULE_EDIT'
)
AND NOT EXISTS (
    SELECT 1
    FROM PERMISSION_GROUP_PERMISSION pgp
    WHERE pgp.PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND pgp.PermissionId = p.Id
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL;

-- 3. APP_MENU (Dưới menu cha "Quản trị hệ thống")
-- 3.1. Thiết lập tham số hệ thống
INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Thiết lập tham số hệ thống',
       '/administration/system-param',
       'pi pi-sliders-h',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       100,
       1,
       'SYSTEM_PARAM_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/administration/system-param'
);

-- 3.2. Cấu hình kết nối PMIS
INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Cấu hình kết nối PMIS',
       '/pmis-sync/endpoint-config',
       'pi pi-server',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       101,
       1,
       'PMIS_ENDPOINT_CONFIG_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/pmis-sync/endpoint-config'
);

-- 3.3. Đồng bộ thủ công PMIS
INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Đồng bộ thủ công PMIS',
       '/pmis-sync/manual-sync',
       'pi pi-sync',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       102,
       1,
       'PMIS_MANUAL_SYNC_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/pmis-sync/manual-sync'
);

-- 3.4. Đồng bộ tự động PMIS
INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Đồng bộ tự động PMIS',
       '/pmis-sync/schedule',
       'pi pi-calendar-clock',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       103,
       1,
       'SYNC_SCHEDULE_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/pmis-sync/schedule'
);

COMMIT;
