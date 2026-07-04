-- Cập nhật quyền menu Kiểm tra nhập liệu: MANAGE thay vì VIEW (đã chạy 0019 trước đó).
UPDATE APP_MENU
SET PermissionCode = 'DOSSIER_DIGITIZATION_MANAGE'
WHERE Url = '/dossier-management/digitization/approve'
  AND PermissionCode = 'DOSSIER_DIGITIZATION_VIEW';

COMMIT;
