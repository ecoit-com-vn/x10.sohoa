-- Chỉ chèn khi menu cha tồn tại: menu cha này KHÔNG được tạo bởi bất kỳ script nào trong repo
-- (và cũng không còn trên DB dùng chung — nó từng tồn tại rồi bị xoá, FK_MENU_PARENT có ON DELETE
-- CASCADE nên các menu con biến mất theo). Thiếu điều kiện AND EXISTS, script này vi phạm
-- ORA-02291 trên mọi database mới và làm DbUp dừng cả lượt migrate.
-- Migration to insert "Danh mục loại hồ sơ" menu under "Danh mục hồ sơ" (Id = 45)

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 46, 'Danh mục loại hồ sơ', '/catalog/dossier-type', 'pi pi-file', 45, 1, 1, 'DOSSIER_TYPE_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 46)
  AND EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 45);

COMMIT;
