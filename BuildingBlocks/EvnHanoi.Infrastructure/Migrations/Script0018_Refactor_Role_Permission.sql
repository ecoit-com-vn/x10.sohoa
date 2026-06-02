-- BuildingBlocks/EvnHanoi.Infrastructure/Migrations/Script0018_Refactor_Role_Permission.sql
-- Migration bổ sung 19 Quyền hệ thống động và tái cấu trúc bảng ROLE_PERMISSION liên kết bằng PermissionId dạng UUID v7.

-- =========================================================================
-- 1. Seed 19 Quyền hệ thống động vào bảng PERMISSION (nếu chưa có)
-- =========================================================================
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-dashboard-111111111111', 'VIEW_DASHBOARD', 'Xem bảng điều khiển', 'Xem giao diện thống kê, tổng quan hệ thống', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'VIEW_DASHBOARD');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-user-111111111111', 'USER_MANAGE', 'Quản lý người dùng', 'Thêm, sửa, khóa tài khoản người dùng', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'USER_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-role-111111111111', 'ROLE_MANAGE', 'Quản lý vai trò', 'Xem danh sách và thay đổi vai trò (Role)', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'ROLE_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-perm-111111111111', 'PERMISSION_MANAGE', 'Phân quyền vai trò', 'Gán quyền thao tác cụ thể cho từng vai trò', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PERMISSION_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-sysparam-111111111111', 'SYSTEM_PARAM_MANAGE', 'Quản lý cấu hình tham số', 'Xem và sửa đổi các tham số cài đặt hệ thống', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'SYSTEM_PARAM_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-org-111111111111', 'ORGANIZATION_MANAGE', 'Cài đặt tổ chức', 'Quản lý sơ đồ phòng ban, đơn vị thành viên', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'ORGANIZATION_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-catalog-111111111111', 'CATALOG_MANAGE', 'Quản lý danh mục đơn vị', 'Quản lý danh mục đơn vị tính và các danh mục khác', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'CATALOG_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-menu-111111111111', 'MENU_MANAGE', 'Quản lý Menu', 'Xem, thêm, sửa, xóa cấu hình Menu động', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'MENU_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-usrgrp-111111111111', 'USER_GROUP_MANAGE', 'Quản lý nhóm người dùng', 'Xem, thêm, sửa, xóa các nhóm người dùng (User Group)', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'USER_GROUP_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-upload-111111111111', 'UPLOAD_CONFIG_MANAGE', 'Cấu hình upload file', 'Xem và sửa đổi các quy định về định dạng, dung lượng file tải lên', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'UPLOAD_CONFIG_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-auditview-1111111111', 'AUDIT_LOG_VIEW', 'Xem nhật ký hệ thống', 'Xem danh sách nhật ký thao tác bảo mật hệ thống', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'AUDIT_LOG_VIEW');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-auditdel-11111111111', 'AUDIT_LOG_DELETE', 'Xóa nhật ký hệ thống', 'Thực hiện xóa/dọn dẹp nhật ký hệ thống một cách an toàn', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'AUDIT_LOG_DELETE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-equipview-1111111111', 'EQUIPMENT_VIEW', 'Xem thông tin thiết bị', 'Xem danh sách và chi tiết hồ sơ thiết bị', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'EQUIPMENT_VIEW');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-equipman-1111111111', 'EQUIPMENT_MANAGE', 'Quản lý thông số thiết bị', 'Thêm mới, sửa đổi cấu trúc EAV hồ sơ thiết bị', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'EQUIPMENT_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-digview-11111111111', 'DIGITIZATION_VIEW', 'Xem hồ sơ số hóa', 'Truy cập thư mục ảo khám phá tài liệu số hóa', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'DIGITIZATION_VIEW');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-digman-11111111111', 'DIGITIZATION_MANAGE', 'Quản trị số hóa OCR', 'Tải lên, phân bổ và nhận dạng chữ OCR tài liệu', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'DIGITIZATION_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-repview-111111111111', 'REPORT_VIEW', 'Xem báo cáo & thống kê', 'Xem giao diện hiển thị biểu đồ báo cáo', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'REPORT_VIEW');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-repman-111111111111', 'REPORT_MANAGE', 'Thiết lập báo cáo', 'Cấu hình chỉ tiêu và nguồn dữ liệu động cho báo cáo', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'REPORT_MANAGE');

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-repexport-11111111111', 'REPORT_EXPORT', 'Xuất file báo cáo', 'Được phép xuất bản in PDF/Excel của báo cáo', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'REPORT_EXPORT');

-- =========================================================================
-- 2. Seed chi tiết API Endpoints tương ứng vào PERMISSION_DETAIL
-- =========================================================================
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-dashboard-111111111111', 'perm-uuid-dashboard-111111111111', '*', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-dashboard-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-user-111111111111', 'perm-uuid-user-111111111111', 'UsersController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-user-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-role-111111111111', 'perm-uuid-role-111111111111', 'RolesController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-role-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-perm-111111111111', 'perm-uuid-perm-111111111111', 'PermissionsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-perm-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-sysparam-111111111111', 'perm-uuid-sysparam-111111111111', 'SystemParamsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-sysparam-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-org-111111111111', 'perm-uuid-org-111111111111', 'OrganizationUnitsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-org-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-catalog-111111111111', 'perm-uuid-catalog-111111111111', 'CatalogsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-catalog-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-menu-111111111111', 'perm-uuid-menu-111111111111', 'MenusController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-menu-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-usrgrp-111111111111', 'perm-uuid-usrgrp-111111111111', 'UserGroupsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-usrgrp-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-upload-111111111111', 'perm-uuid-upload-111111111111', 'UploadConfigsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-upload-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-auditview-1111111111', 'perm-uuid-auditview-1111111111', 'AuditLogController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-auditview-1111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-auditdel-11111111111', 'perm-uuid-auditdel-11111111111', 'AuditLogController', 'Delete' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-auditdel-11111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-equipview-1111111111', 'perm-uuid-equipview-1111111111', 'EquipmentsController', 'Get*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-equipview-1111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-equipman-1111111111', 'perm-uuid-equipman-1111111111', 'EquipmentsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-equipman-1111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-digview-11111111111', 'perm-uuid-digview-11111111111', 'DigitizationsController', 'Get*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-digview-11111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-digman-11111111111', 'perm-uuid-digman-11111111111', 'DigitizationsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-digman-11111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-repview-111111111111', 'perm-uuid-repview-111111111111', 'ReportsController', 'Get*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-repview-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-repman-111111111111', 'perm-uuid-repman-111111111111', 'ReportsController', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-repman-111111111111');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'det-uuid-repexport-11111111111', 'perm-uuid-repexport-11111111111', 'ReportsController', 'Export*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'det-uuid-repexport-11111111111');


-- =========================================================================
-- 3. Tái cấu trúc bảng ROLE_PERMISSION kết nối theo PermissionId UUID
-- =========================================================================

-- Tạo bảng tạm sao lưu cấu hình phân quyền vai trò hiện tại
CREATE TABLE TEMP_ROLE_PERMISSION AS 
SELECT RoleId, PermissionCode FROM ROLE_PERMISSION;

-- Hủy bỏ bảng ROLE_PERMISSION cấu trúc cũ
DROP TABLE ROLE_PERMISSION;

-- Tạo bảng ROLE_PERMISSION chuẩn hóa kết cấu ngoại
CREATE TABLE ROLE_PERMISSION (
    Id VARCHAR2(36) PRIMARY KEY,
    RoleId NUMBER(19) NOT NULL,
    PermissionId VARCHAR2(36) NOT NULL,
    CONSTRAINT fk_rp_role FOREIGN KEY (RoleId) REFERENCES ROLE(Id) ON DELETE CASCADE,
    CONSTRAINT fk_rp_permission FOREIGN KEY (PermissionId) REFERENCES PERMISSION(Id) ON DELETE CASCADE
);

-- Chỉ mục hỗ trợ tối ưu hóa truy vấn kết hợp (JOIN)
CREATE INDEX idx_rp_role_perm ON ROLE_PERMISSION(RoleId, PermissionId);

-- Di cư dữ liệu cũ: Phân tích khớp mã Code chuỗi tĩnh sang định danh PermissionId UUID tương ứng
INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId)
SELECT 
    LOWER(RAWTOHEX(SYS_GUID())) AS Id,
    t.RoleId,
    p.Id AS PermissionId
FROM TEMP_ROLE_PERMISSION t
INNER JOIN PERMISSION p ON UPPER(t.PermissionCode) = UPPER(p.Code);

-- Xóa bảng tạm
DROP TABLE TEMP_ROLE_PERMISSION;
