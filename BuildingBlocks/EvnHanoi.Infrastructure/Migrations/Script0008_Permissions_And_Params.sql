-- Script0008_Permissions_And_Params.sql
-- Thêm bảng ROLE_PERMISSION, SYSTEM_PARAM và seed dữ liệu cho Đơn vị tính, tham số hệ thống.

-- 1. Bảng ROLE_PERMISSION
CREATE TABLE ROLE_PERMISSION (
    Id VARCHAR2(36) NOT NULL PRIMARY KEY,
    RoleId NUMBER NOT NULL,
    PermissionCode VARCHAR2(100) NOT NULL,
    CONSTRAINT fk_roleperm_role FOREIGN KEY (RoleId) REFERENCES ROLE(Id) ON DELETE CASCADE
);

-- 2. Bảng SYSTEM_PARAM
CREATE TABLE SYSTEM_PARAM (
    ParamKey VARCHAR2(100) NOT NULL PRIMARY KEY,
    ParamValue VARCHAR2(4000) NOT NULL,
    Description VARCHAR2(1000) NULL,
    DataType VARCHAR2(50) DEFAULT 'String' NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP NULL
);

-- Clean existing admin permissions first
DELETE FROM ROLE_PERMISSION WHERE RoleId IN (SELECT Id FROM ROLE WHERE Code = 'ADMIN');

-- Insert Permissions cho admin (roleId = 1)
INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p1', Id, 'VIEW_DASHBOARD' FROM ROLE WHERE Code = 'ADMIN';

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p2', Id, 'USER_MANAGE' FROM ROLE WHERE Code = 'ADMIN';

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p3', Id, 'ROLE_MANAGE' FROM ROLE WHERE Code = 'ADMIN';

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p4', Id, 'PERMISSION_MANAGE' FROM ROLE WHERE Code = 'ADMIN';

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p5', Id, 'SYSTEM_PARAM_MANAGE' FROM ROLE WHERE Code = 'ADMIN';

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p6', Id, 'ORGANIZATION_MANAGE' FROM ROLE WHERE Code = 'ADMIN';

INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionCode)
SELECT 'p7', Id, 'CATALOG_MANAGE' FROM ROLE WHERE Code = 'ADMIN';

-- Insert System Parameters cố định
DELETE FROM SYSTEM_PARAM;

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
VALUES ('MaxFileUploadSize', '52428800', 'Dung lượng file tối đa cho phép tải lên (Bytes)', 'Number');

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
VALUES ('AllowedFileExtensions', '.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg', 'Định dạng file được phép tải lên hệ thống', 'String');

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
VALUES ('OcrApiUrl', 'http://localhost:5000/ocr', 'Đường dẫn API dịch vụ OCR AI nhận diện chữ', 'String');

INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
VALUES ('TokenExpirationMinutes', '60', 'Thời gian hết hạn của JWT Access Token đăng nhập (Phút)', 'Number');

-- Insert Danh mục đơn vị tính (CatalogType = 'UnitOfMeasure')
INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_CAI', 'Cái', 'UnitOfMeasure', 'Đơn vị tính đếm chiếc' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_CAI');

INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_MET', 'Mét', 'UnitOfMeasure', 'Đơn vị đo chiều dài' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_MET');

INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_KG', 'Kilôgam', 'UnitOfMeasure', 'Đơn vị đo khối lượng' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_KG');

INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_BO', 'Bộ', 'UnitOfMeasure', 'Đơn vị tính theo bộ sản phẩm' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_BO');
