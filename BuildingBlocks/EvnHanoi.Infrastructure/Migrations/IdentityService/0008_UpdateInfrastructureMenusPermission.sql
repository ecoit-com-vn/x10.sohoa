-- Migration to update permission codes for substation and transmission line menus to match split controllers
UPDATE APP_MENU SET PermissionCode = 'SUBSTATION_VIEW' WHERE Id = 47;
UPDATE APP_MENU SET PermissionCode = 'TRANSMISSION_LINE_VIEW' WHERE Id = 48;
COMMIT;
