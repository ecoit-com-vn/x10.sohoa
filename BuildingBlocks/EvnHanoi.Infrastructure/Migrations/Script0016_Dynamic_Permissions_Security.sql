-- BuildingBlocks/EvnHanoi.Infrastructure/Migrations/Script0016_Dynamic_Permissions_Security.sql
-- Migration bổ sung các bảng cho việc phân quyền động dựa trên Tài nguyên (Resource/Controller-Action)

-- 1. Bảng PERMISSION
CREATE TABLE PERMISSION (
    Id VARCHAR2(36) PRIMARY KEY,
    Code VARCHAR2(100) NOT NULL UNIQUE,
    Name VARCHAR2(200) NOT NULL,
    Description VARCHAR2(500) NULL,
    IsActive NUMBER(1) DEFAULT 1 NOT NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CreatedBy VARCHAR2(36) NOT NULL,
    UpdatedAt TIMESTAMP NULL,
    UpdatedBy VARCHAR2(36) NULL
);

-- 2. Bảng PERMISSION_DETAIL
CREATE TABLE PERMISSION_DETAIL (
    Id VARCHAR2(36) PRIMARY KEY,
    PermissionId VARCHAR2(36) NOT NULL,
    ControllerName VARCHAR2(100) NOT NULL,
    ActionName VARCHAR2(100) NOT NULL,
    CONSTRAINT fk_pd_permission FOREIGN KEY (PermissionId) REFERENCES PERMISSION(Id) ON DELETE CASCADE
);

CREATE INDEX idx_pd_perm_ctrl ON PERMISSION_DETAIL(PermissionId, ControllerName);

-- 3. Bảng USER_PERMISSION
CREATE TABLE USER_PERMISSION (
    UserId VARCHAR2(36) NOT NULL,
    PermissionId VARCHAR2(36) NOT NULL,
    PRIMARY KEY (UserId, PermissionId),
    CONSTRAINT fk_up_permission FOREIGN KEY (PermissionId) REFERENCES PERMISSION(Id) ON DELETE CASCADE
);

-- 4. Bảng USER_GROUP_PERMISSION
CREATE TABLE USER_GROUP_PERMISSION (
    UserGroupId NUMBER(19) NOT NULL,
    PermissionId VARCHAR2(36) NOT NULL,
    PRIMARY KEY (UserGroupId, PermissionId),
    CONSTRAINT fk_ugp_group FOREIGN KEY (UserGroupId) REFERENCES USER_GROUP(Id) ON DELETE CASCADE,
    CONSTRAINT fk_ugp_permission FOREIGN KEY (PermissionId) REFERENCES PERMISSION(Id) ON DELETE CASCADE
);

-- 5. Seed dữ liệu quyền tối cao (SUPER_ADMIN) cho admin user
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
VALUES ('admin-super-perm-uuid-111111111111', 'SUPER_ADMIN', 'Toàn quyền Hệ thống', 'Quyền quản trị tối cao, cho phép truy cập mọi API/Controller', 1, 'SYSTEM');

INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
VALUES ('admin-super-detail-uuid-111111111111', 'admin-super-perm-uuid-111111111111', '*', '*');

INSERT INTO USER_PERMISSION (UserId, PermissionId)
SELECT Id, 'admin-super-perm-uuid-111111111111'
FROM APP_USER WHERE UserName = 'admin';
