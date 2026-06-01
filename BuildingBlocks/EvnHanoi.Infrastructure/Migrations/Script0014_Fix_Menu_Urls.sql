-- E:\ecoit\sohoax10\sohoa.backend\BuildingBlocks\EvnHanoi.Infrastructure\Migrations\Script0014_Fix_Menu_Urls.sql
-- Migration cập nhật lại Url của các menu trong APP_MENU để khớp với định tuyến Angular của frontend

UPDATE APP_MENU SET Url = '/dashboard' WHERE Id = 1;
UPDATE APP_MENU SET Url = '/administration/user-management' WHERE Id = 3;
UPDATE APP_MENU SET Url = '/administration/role-management' WHERE Id = 4;
UPDATE APP_MENU SET Url = '/administration/menu-management' WHERE Id = 5;
UPDATE APP_MENU SET Url = '/administration/user-groups' WHERE Id = 6;
UPDATE APP_MENU SET Url = '/administration/upload-configuration' WHERE Id = 7;
UPDATE APP_MENU SET Url = '/administration/organization-settings' WHERE Id = 8;
UPDATE APP_MENU SET Url = '/administration/audit-log' WHERE Id = 9;

COMMIT;
