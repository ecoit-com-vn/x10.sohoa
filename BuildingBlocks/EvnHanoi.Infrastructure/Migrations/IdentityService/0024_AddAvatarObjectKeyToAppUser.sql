-- Cột AvatarObjectKey có thể đã tồn tại (schema cũ / chạy tay).
-- Việc ADD idempotent được xử lý bởi Migration0024_AddAvatarObjectKeyToAppUser.cs
SELECT 1 FROM DUAL
