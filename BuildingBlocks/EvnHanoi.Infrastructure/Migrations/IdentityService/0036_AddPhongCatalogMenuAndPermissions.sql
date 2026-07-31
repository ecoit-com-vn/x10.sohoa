-- Quyền riêng cho Danh mục phông.
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-phong-view', 'PHONG_VIEW', 'Xem danh mục phông', 'Xem danh mục phông theo đơn vị', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PHONG_VIEW');
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-phong-create', 'PHONG_CREATE', 'Thêm danh mục phông', 'Thêm danh mục phông theo đơn vị', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PHONG_CREATE');
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-phong-edit', 'PHONG_EDIT', 'Sửa danh mục phông', 'Sửa danh mục phông theo đơn vị', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PHONG_EDIT');
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-phong-delete', 'PHONG_DELETE', 'Xóa danh mục phông', 'Xóa danh mục phông theo đơn vị', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PHONG_DELETE');
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'perm-phong-manage', 'PHONG_MANAGE', 'Quản lý trạng thái danh mục phông', 'Khóa hoặc mở khóa danh mục phông', 1, 'SYSTEM' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM PERMISSION WHERE Code = 'PHONG_MANAGE');

UPDATE PERMISSION SET IsActive = 1 WHERE Code IN
('PHONG_VIEW','PHONG_CREATE','PHONG_EDIT','PHONG_DELETE','PHONG_MANAGE');

-- Ánh xạ quyền hạt mịn tới đúng controller/action để middleware và màn hình phân quyền
-- cùng nhận diện được chức năng Danh mục phông, không phụ thuộc lần đồng bộ RabbitMQ đầu tiên.
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-view-list', p.Id, 'PhongController', 'GetAll' FROM PERMISSION p
WHERE p.Code = 'PHONG_VIEW' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'GetAll');
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-view-detail', p.Id, 'PhongController', 'GetById' FROM PERMISSION p
WHERE p.Code = 'PHONG_VIEW' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'GetById');
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-create', p.Id, 'PhongController', 'Create' FROM PERMISSION p
WHERE p.Code = 'PHONG_CREATE' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'Create');
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-edit', p.Id, 'PhongController', 'Update' FROM PERMISSION p
WHERE p.Code = 'PHONG_EDIT' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'Update');
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-delete', p.Id, 'PhongController', 'Delete' FROM PERMISSION p
WHERE p.Code = 'PHONG_DELETE' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'Delete');
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-lock', p.Id, 'PhongController', 'Lock' FROM PERMISSION p
WHERE p.Code = 'PHONG_MANAGE' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'Lock');
INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
SELECT 'pd-phong-unlock', p.Id, 'PhongController', 'Unlock' FROM PERMISSION p
WHERE p.Code = 'PHONG_MANAGE' AND NOT EXISTS
(SELECT 1 FROM PERMISSION_DETAIL d WHERE d.PermissionId = p.Id AND d.ControllerName = 'PhongController' AND d.ActionName = 'Unlock');

UPDATE APP_MENU
   SET Name = 'Danh mục phông', PermissionCode = 'PHONG_VIEW', IsActive = 1
 WHERE Url = '/catalog/phong';

INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 'Danh mục phông', '/catalog/phong', 'pi pi-folder',
       COALESCE((SELECT MIN(Id) FROM APP_MENU WHERE Name = 'Quản lý danh mục'),
                (SELECT MIN(Id) FROM APP_MENU WHERE Name = 'Danh mục hệ thống'), 10),
       3, 1, 'PHONG_VIEW'
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Url = '/catalog/phong');

COMMIT;
