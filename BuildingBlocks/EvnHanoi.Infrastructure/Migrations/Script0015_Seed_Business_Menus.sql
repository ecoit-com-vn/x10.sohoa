-- E:\ecoit\sohoax10\sohoa.backend\BuildingBlocks\EvnHanoi.Infrastructure\Migrations\Script0015_Seed_Business_Menus.sql
-- Migration bổ sung các menu nghiệp vụ chính vào bảng APP_MENU để hiển thị đầy đủ trên Sidebar

-- 1. Phân hệ Hồ sơ Thiết bị (Equipment)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (10, 'Hồ sơ Thiết bị', '/equipment', 'pi pi-bolt', NULL, 3, 1, 'EQUIPMENT_VIEW');

-- 2. Phân hệ Số hóa Hồ sơ (Digitization)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (11, 'Số hóa Hồ sơ', '/digitization', 'pi pi-cloud-upload', NULL, 4, 1, 'DIGITIZATION_VIEW');

-- 3. Phân hệ Hiệu đính AI-OCR
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (12, 'Hiệu đính AI-OCR', '/ocr-correction', 'pi pi-eye', NULL, 5, 1, 'DIGITIZATION_VIEW');

-- 4. Phân hệ Kệ lưu trữ (Shelf)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (13, 'Kệ lưu trữ (Shelf)', '/physical-storage', 'pi pi-database', NULL, 6, 1, 'EQUIPMENT_VIEW');

-- 5. Phân hệ Danh mục dùng chung (Catalog)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (14, 'Danh mục dùng chung', '/catalog', 'pi pi-bookmark', NULL, 7, 1, 'CATALOG_VIEW');

-- 6. Phân hệ Báo cáo & Thống kê
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (15, 'Báo cáo & Thống kê', '/reports', 'pi pi-chart-line', NULL, 8, 1, 'REPORT_VIEW');

-- 7. Phân hệ Luồng & Quy trình (Workflow)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (16, 'Luồng & Quy trình', '/workflow', 'pi pi-directions', NULL, 9, 1, 'REPORT_VIEW');

-- 8. Phân hệ Tra cứu nâng cao (Search)
INSERT INTO APP_MENU (Id, Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
VALUES (17, 'Tra cứu nâng cao', '/search', 'pi pi-search', NULL, 10, 1, 'VIEW_DASHBOARD');

COMMIT;
