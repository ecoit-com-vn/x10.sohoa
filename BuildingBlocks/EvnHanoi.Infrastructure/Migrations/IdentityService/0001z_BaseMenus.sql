-- Tạo 3 menu GỐC mà các migration menu phía sau dùng làm ParentId.
--
-- VÌ SAO CẦN FILE NÀY: các menu gốc này vốn chỉ được tạo trong Migrations/Seeds/IdentityService.sql,
-- mà DatabaseMigrationHelper chạy seed SAU toàn bộ schema migration (và seed chỉ chạy khi
-- DbUp:RunSeeds = true, mặc định là false). Trong khi đó 0002_CatalogMenus.sql chèn menu con với
-- ParentId = 10, 0009/0010/... dùng ParentId = 2 và 12. Trên một database MỚI, các script đó vi
-- phạm FK_MENU_PARENT (ORA-02291) và DbUp DỪNG toàn bộ lượt migrate ngay tại đó — kéo theo mọi
-- migration sau (kể cả các bảng của EquipmentService như DOCUMENT_VERSIONS) không bao giờ được
-- tạo. Đó là lý do một môi trường mới không thể cài từ đầu, dù DB dùng chung vẫn chạy bình thường
-- (nó đã có sẵn các menu này từ trước, nên script cũ đi qua được).
--
-- Vì migration lỗi làm DbUp dừng ngay, KHÔNG thể sửa bằng cách thêm migration số lớn hơn —
-- phải chèn một script chạy TRƯỚC 0002. Tên "0001z_" xếp sau "0001_Schema.sql" (bảng APP_MENU
-- phải tồn tại trước) và trước "0002_..." với cả sắp xếp ordinal lẫn culture-aware.
--
-- AN TOÀN VỚI DATABASE ĐANG CHẠY: chỉ chèn khi chưa có (WHERE NOT EXISTS), và 3 Id này không bị
-- bất kỳ migration nào DELETE (các lệnh DELETE FROM APP_MENU chỉ nhắm 11, 26-33, 44), nên không
-- có nguy cơ làm sống lại menu đã bị xoá có chủ đích. Giá trị lấy đúng từ Seeds/IdentityService.sql.

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 2, 'Quản trị hệ thống', NULL, 'pi pi-cog', NULL, 2, 1, 'USER_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 2);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 10, 'Danh mục hệ thống', NULL, 'pi pi-folder-open', NULL, 3, 1, 'CATALOG_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 10);

INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT 12, 'Hồ sơ & Thiết bị', NULL, 'pi pi-file', NULL, 4, 1, 'EQUIPMENT_VIEW' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM APP_MENU WHERE Id = 12);

COMMIT;
