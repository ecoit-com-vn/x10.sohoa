-- BuildingBlocks/EvnHanoi.Infrastructure/Migrations/Script0017_Standardize_User_And_Uuid.sql
-- Migration chuẩn hóa định danh APP_USER.Id và các khóa ngoại liên quan sang dạng VARCHAR2(36) để lưu UUID v7

-- 1. Hủy bỏ khóa ngoại cũ tham chiếu tới APP_USER(Id)
ALTER TABLE USER_ROLE DROP CONSTRAINT fk_userrole_user;
ALTER TABLE USER_GROUP_MEMBER DROP CONSTRAINT fk_ugm_user;
ALTER TABLE USER_UNIT_ROLE DROP CONSTRAINT fk_uur_user;

-- Xóa dữ liệu cũ ở các bảng liên kết để tránh lỗi vi phạm ràng buộc khóa ngoại với UUID mới
DELETE FROM USER_ROLE;
DELETE FROM USER_GROUP_MEMBER;
DELETE FROM USER_UNIT_ROLE;
DELETE FROM USER_PERMISSION;

-- 2. Xóa bảng APP_USER cũ
DROP TABLE APP_USER;

-- 3. Tạo lại bảng APP_USER với cột Id là VARCHAR2(36) để lưu UUID v7
CREATE TABLE APP_USER (
    Id VARCHAR2(36) PRIMARY KEY,
    UserName VARCHAR2(50) NOT NULL UNIQUE,
    Email VARCHAR2(100) NULL,
    FullName VARCHAR2(255) NOT NULL,
    PasswordHash VARCHAR2(255) NOT NULL,
    IsActive NUMBER(1) DEFAULT 1,
    OrganizationUnitId NUMBER NULL,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR2(50),
    UpdatedAt TIMESTAMP,
    UpdatedBy VARCHAR2(50),
    AccessFailedCount NUMBER DEFAULT 0 NOT NULL,
    LockoutEnd TIMESTAMP NULL,
    LockoutEnabled NUMBER DEFAULT 1 NOT NULL,
    CONSTRAINT fk_appuser_orgunit FOREIGN KEY (OrganizationUnitId) REFERENCES ORGANIZATION_UNIT(Id)
);

-- 4. Thay đổi kiểu dữ liệu cột UserId ở các bảng phụ thuộc và tạo lại khóa ngoại
ALTER TABLE USER_ROLE MODIFY (UserId VARCHAR2(36));
ALTER TABLE USER_ROLE ADD CONSTRAINT fk_userrole_user FOREIGN KEY (UserId) REFERENCES APP_USER(Id) ON DELETE CASCADE;

ALTER TABLE USER_GROUP_MEMBER MODIFY (UserId VARCHAR2(36));
ALTER TABLE USER_GROUP_MEMBER ADD CONSTRAINT fk_ugm_user FOREIGN KEY (UserId) REFERENCES APP_USER(Id) ON DELETE CASCADE;

ALTER TABLE USER_UNIT_ROLE MODIFY (UserId VARCHAR2(36));
ALTER TABLE USER_UNIT_ROLE ADD CONSTRAINT fk_uur_user FOREIGN KEY (UserId) REFERENCES APP_USER(Id) ON DELETE CASCADE;

-- 5. Bổ sung ràng buộc khóa ngoại fk_up_user cho USER_PERMISSION
ALTER TABLE USER_PERMISSION ADD CONSTRAINT fk_up_user FOREIGN KEY (UserId) REFERENCES APP_USER(Id) ON DELETE CASCADE;

-- 6. Seed lại dữ liệu người dùng mặc định (Sử dụng UUID v7 cố định, mật khẩu mặc định Admin@123!)
INSERT INTO APP_USER (Id, UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
VALUES ('018fc1e0-0000-0000-0000-000000000000', 'admin', 'admin@evnhanoi.vn', N'Quản trị viên Hệ thống', '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K', 1, 0, 0, 'SYSTEM');

INSERT INTO APP_USER (Id, UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
VALUES ('018fc1e0-0000-0000-0000-000000000001', 'operator', 'operator@evnhanoi.vn', N'Nhân viên Vận hành', '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K', 1, 1, 0, 'SYSTEM');

-- Gán vai trò cho admin seed
INSERT INTO USER_ROLE (UserId, RoleId)
SELECT '018fc1e0-0000-0000-0000-000000000000', Id FROM ROLE WHERE Code = 'ADMIN';

-- Gán quyền SUPER_ADMIN cho admin seed
DELETE FROM USER_PERMISSION;
INSERT INTO USER_PERMISSION (UserId, PermissionId)
VALUES ('018fc1e0-0000-0000-0000-000000000000', 'admin-super-perm-uuid-111111111111');
