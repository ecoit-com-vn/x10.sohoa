-- E:\ecoit\sohoax10\sohoa.backend\BuildingBlocks\EvnHanoi.Infrastructure\Migrations\Script0013_Seed_Admin_User.sql
-- Script seed dữ liệu ban đầu: Vai trò ADMIN và tài khoản quản trị viên hệ thống.
-- BCrypt hash được tính cho mật khẩu: Admin@123!
-- Sử dụng BCrypt cost factor 11 (an toàn và tương thích với BCrypt.Net-Next 4.x)

-- =============================================
-- 1. Seed vai trò ADMIN (nếu chưa có)
-- =============================================
INSERT INTO ROLE (Code, Name, Description, CreatedBy)
SELECT 'ADMIN', 'Quản trị viên hệ thống', 'Tài khoản có toàn quyền trên hệ thống', 'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM ROLE WHERE Code = 'ADMIN');

-- =============================================
-- 2. Seed tài khoản admin (nếu chưa có)
-- Mật khẩu mặc định: Admin@123!
-- Hash BCrypt cost=11 của Admin@123!
-- LƯU Ý: Hãy đổi mật khẩu ngay sau khi đăng nhập lần đầu!
-- =============================================
INSERT INTO APP_USER (UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
SELECT 
    'admin',
    'admin@evnhanoi.vn',
    N'Quản trị viên Hệ thống',
    '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K',
    1,
    0,
    0,
    'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_USER WHERE UserName = 'admin');

-- =============================================
-- 3. Gán vai trò ADMIN cho tài khoản admin (nếu chưa có)
-- =============================================
INSERT INTO USER_ROLE (UserId, RoleId)
SELECT u.Id, r.Id
FROM APP_USER u, ROLE r
WHERE u.UserName = 'admin'
  AND r.Code = 'ADMIN'
  AND NOT EXISTS (
    SELECT 1 FROM USER_ROLE ur 
    WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
  );

-- =============================================
-- 4. Bổ sung seed tài khoản test thêm (tuỳ chọn)
-- Mật khẩu mặc định: User@123!
-- Hash BCrypt cost=11 của User@123!
-- =============================================
INSERT INTO APP_USER (UserName, Email, FullName, PasswordHash, IsActive, LockoutEnabled, AccessFailedCount, CreatedBy)
SELECT 
    'operator',
    'operator@evnhanoi.vn',
    N'Nhân viên Vận hành',
    '$2a$11$uLpFt/IGMdFQH.zRBNT1uORBf5u3b3qnFfQygBH2Y4fFKKsAFl/3K',
    1,
    1,
    0,
    'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_USER WHERE UserName = 'operator');
