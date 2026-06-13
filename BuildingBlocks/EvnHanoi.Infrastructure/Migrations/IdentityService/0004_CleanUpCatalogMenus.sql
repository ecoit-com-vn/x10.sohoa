-- Clean up Phông, Mục lục hồ sơ, Loại hồ sơ, Kệ hồ sơ, Tầng hồ sơ, Hộp hồ sơ from APP_MENU
-- to only keep Chức vụ, Lĩnh vực, Tình trạng vật lý under Quản lý danh mục
DELETE FROM APP_MENU WHERE Id IN (11, 26, 27, 28, 29, 30);
COMMIT;
