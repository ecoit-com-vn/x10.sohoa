-- Chỉ chèn khi menu cha tồn tại: menu cha này KHÔNG được tạo bởi bất kỳ script nào trong repo
-- (và cũng không còn trên DB dùng chung — nó từng tồn tại rồi bị xoá, FK_MENU_PARENT có ON DELETE
-- CASCADE nên các menu con biến mất theo). Thiếu điều kiện AND EXISTS, script này vi phạm
-- ORA-02291 trên mọi database mới và làm DbUp dừng cả lượt migrate.
-- Delete submenus first to ensure clean execution/idempotency
DELETE FROM APP_MENU WHERE Id IN (28, 29, 30, 44);

-- Re-insert Kệ, Tầng, Hộp under ParentId = 43 (Danh mục riêng)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 28, 'Kệ hồ sơ', '/catalog/shelf', 'pi pi-database', 43, 1, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 28)
  AND EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 43);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 29, 'Tầng hồ sơ', '/catalog/floor', 'pi pi-align-justify', 43, 2, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 29)
  AND EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 43);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 30, 'Hộp hồ sơ', '/catalog/box', 'pi pi-box', 43, 3, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 30)
  AND EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 43);

-- Insert new submenu "Danh mục riêng" (IsPrivate = true) under ParentId = 43
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 44, 'Danh mục riêng', '/catalog/private', 'pi pi-folder-open', 43, 4, 1, 'PRIVATE_CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 44)
  AND EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 43);

COMMIT;
