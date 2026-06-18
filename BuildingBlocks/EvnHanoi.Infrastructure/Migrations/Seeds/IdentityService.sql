-- Seed default Roles
INSERT INTO ROLE (Code, Name, Description, CreatedBy)
SELECT 'ADMIN', 'Quản trị viên hệ thống', 'Tài khoản có toàn quyền trên hệ thống', 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM ROLE WHERE Code = 'ADMIN');

INSERT INTO ROLE (Code, Name, Description, CreatedBy)
SELECT 'OPERATOR', 'Nhân viên vận hành', 'Tài khoản nhân viên vận hành hệ thống', 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM ROLE WHERE Code = 'OPERATOR');

INSERT INTO ROLE (Code, Name, Description, CreatedBy)
SELECT 'USER', 'Người dùng thông thường', 'Tài khoản người dùng thông thường', 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM ROLE WHERE Code = 'USER');

-- Seed Users (Mật khẩu mặc định: Admin@123!)
INSERT INTO APP_USER (Id, UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
SELECT '018fc1e0-0000-0000-0000-000000000000', 'admin', 'admin@evnhanoi.vn', N'Quản trị viên Hệ thống', '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K', 1, 0, 0, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_USER WHERE UserName = 'admin');

INSERT INTO APP_USER (Id, UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
SELECT '018fc1e0-0000-0000-0000-000000000001', 'operator', 'operator@evnhanoi.vn', N'Nhân viên Vận hành', '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K', 1, 1, 0, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_USER WHERE UserName = 'operator');

-- Assign ADMIN role to admin user
INSERT INTO USER_ROLE (UserId, RoleId)
SELECT '018fc1e0-0000-0000-0000-000000000000', Id FROM ROLE WHERE Code = 'ADMIN'
AND NOT EXISTS (SELECT 1 FROM USER_ROLE WHERE UserId = '018fc1e0-0000-0000-0000-000000000000' AND RoleId = (SELECT Id FROM ROLE WHERE Code = 'ADMIN'));

-- Seed default Permissions
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'admin-super-perm-uuid-111111111111', 'SUPER_ADMIN', 'Toàn quyền Hệ thống', 'Quyền quản trị tối cao, cho phép truy cập mọi API/Controller', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'SUPER_ADMIN');

-- Seed default Permission details for SUPER_ADMIN
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'admin-super-detail-uuid-111111111111', 'admin-super-perm-uuid-111111111111', '*', '*' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION_DETAIL WHERE Id = 'admin-super-detail-uuid-111111111111');

-- Assign SUPER_ADMIN permission to admin user
INSERT INTO USER_PERMISSION (UserId, PermissionId)
SELECT '018fc1e0-0000-0000-0000-000000000000', 'admin-super-perm-uuid-111111111111' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM USER_PERMISSION WHERE UserId = '018fc1e0-0000-0000-0000-000000000000' AND PermissionId = 'admin-super-perm-uuid-111111111111');

-- Seed basic dashboard permission
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-uuid-dashboard-111111111111', 'VIEW_DASHBOARD', 'Xem bảng điều khiển', 'Xem giao diện thống kê, tổng quan hệ thống', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'VIEW_DASHBOARD');

-- Seed default system params
INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
SELECT 'MaxFileUploadSize', '52428800', 'Dung lượng file tối đa cho phép tải lên (Bytes)', 'Number' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PARAM WHERE ParamKey = 'MaxFileUploadSize');

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
SELECT 'AllowedFileExtensions', '.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg', 'Định dạng file được phép tải lên hệ thống', 'String' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PARAM WHERE ParamKey = 'AllowedFileExtensions');

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
SELECT 'OcrApiUrl', 'http://localhost:5000/ocr', 'Đường dẫn API dịch vụ OCR AI nhận diện chữ', 'String' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PARAM WHERE ParamKey = 'OcrApiUrl');

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
SELECT 'TokenExpirationMinutes', '60', 'Thời gian hết hạn của JWT Access Token đăng nhập (Phút)', 'Number' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM SYSTEM_PARAM WHERE ParamKey = 'TokenExpirationMinutes');

-- Seed default upload configs
INSERT INTO UPLOAD_CONFIG (ModuleCode, AllowedExtensions, MaxSizeMb, Description)
SELECT 'EQUIPMENT_ATTACHMENT', '.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg', 50, 'Tài liệu đính kèm thiết bị' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM UPLOAD_CONFIG WHERE ModuleCode = 'EQUIPMENT_ATTACHMENT');

INSERT INTO UPLOAD_CONFIG (ModuleCode, AllowedExtensions, MaxSizeMb, Description)
SELECT 'DIGITIZATION_SCAN', '.pdf,.png,.jpg,.jpeg', 100, 'File quét số hóa hồ sơ' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM UPLOAD_CONFIG WHERE ModuleCode = 'DIGITIZATION_SCAN');

-- Seed default menus
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 1, 'Bảng điều khiển', '/dashboard', 'pi pi-home', NULL, 1, 1, 'VIEW_DASHBOARD' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 1);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 2, 'Quản trị hệ thống', NULL, 'pi pi-cog', NULL, 2, 1, 'USER_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 2);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 3, 'Người dùng', '/administration/user-management', 'pi pi-users', 2, 1, 1, 'USER_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 3);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 4, 'Vai trò & Quyền', '/administration/role-management', 'pi pi-key', 2, 2, 1, 'ROLE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 4);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 5, 'Cấu hình Menu', '/administration/menu-management', 'pi pi-list', 2, 3, 1, 'MENU_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 5);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 6, 'Nhóm người dùng', '/administration/user-groups', 'pi pi-user-plus', 2, 4, 1, 'USER_GROUP_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 6);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 7, 'Cấu hình Upload', '/administration/upload-configuration', 'pi pi-upload', 2, 5, 1, 'UPLOAD_CONFIG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 7);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 8, 'Cơ cấu tổ chức', '/administration/organization-settings', 'pi pi-sitemap', 2, 6, 1, 'ORGANIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 8);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 9, 'Nhật ký hệ thống', '/administration/audit-log', 'pi pi-history', 2, 7, 1, 'AUDIT_LOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 9);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 10, 'Danh mục hệ thống', NULL, 'pi pi-folder-open', NULL, 3, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 10);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 11, 'Đơn vị tính', '/catalog/unit-of-measurement', 'pi pi-tag', 10, 1, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 11);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 12, 'Hồ sơ & Thiết bị', NULL, 'pi pi-file', NULL, 4, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 12);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 13, 'Quản lý thông số EAV', '/equipment/form-management', 'pi pi-sliders-h', 12, 1, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 13);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 14, 'Số hóa hồ sơ', NULL, 'pi pi-cloud-upload', NULL, 5, 1, 'DIGITIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 14);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 15, 'Tải lên & Nhận dạng OCR', '/digitization/ocr-upload', 'pi pi-upload', 14, 1, 1, 'DIGITIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 15);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 16, 'Thư mục ảo (Explorer)', '/digitization/virtual-folders', 'pi pi-folder', 14, 2, 1, 'DIGITIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 16);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 17, 'Phân bổ hồ sơ số hóa', '/digitization/ocr-allocation', 'pi pi-share-alt', 14, 3, 1, 'DIGITIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 17);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 18, 'Dữ liệu huấn luyện AI', '/digitization/ocr-training', 'pi pi-cog', 14, 4, 1, 'DIGITIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 18);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 19, 'Hiệu đính OCR', '/ocr-correction', 'pi pi-check-square', NULL, 6, 1, 'DIGITIZATION_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 19);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 20, 'Tra cứu hồ sơ thiết bị', '/search', 'pi pi-search', NULL, 7, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 20);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 21, 'Mượn trả hồ sơ', NULL, 'pi pi-envelope', NULL, 8, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 21);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 22, 'Yêu cầu mượn trả', '/workflow/borrow-return', 'pi pi-send', 21, 1, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 22);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 23, 'Cài đặt quy trình', '/administration/workflow-builder', 'pi pi-sitemap', 2, 8, 1, 'ROLE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 23);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 24, 'Báo cáo & Thống kê', '/reports', 'pi pi-chart-bar', NULL, 9, 1, 'REPORT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 24);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 25, 'Kho lưu trữ vật lý', '/physical-storage', 'pi pi-box', NULL, 10, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 25);

COMMIT;

