-- Sửa URL menu Quản lý hồ sơ thiết bị: tránh prefix /dossier-management
-- che mất menu Nhập liệu hồ sơ số hóa (/dossier-management/digitization/...).

UPDATE APP_MENU
SET Url = '/dossier-management/my-dossiers'
WHERE Url = '/dossier-management'
   OR (Name = N'Quản lý hồ sơ thiết bị' AND Url = '/dossier-management');

COMMIT;
