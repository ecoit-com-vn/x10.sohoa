-- Migration: Cập nhật mã quyền và URL cho menu Tra cứu hồ sơ thiết bị sang SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW và gán cho vai trò ADMIN

-- 1. Cập nhật PermissionCode và Url của menu Tra cứu hồ sơ thiết bị
UPDATE APP_MENU
SET PermissionCode = 'SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW',
    Url = '/search/dossier-by-equipment'
WHERE Url = '/search' OR Name = N'Tra cứu hồ sơ thiết bị';

-- 2. Đăng ký quyền SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW vào bảng PERMISSION
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'search_dossier_equip_view_id', 
       'SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW', 
       N'Tra cứu hồ sơ thiết bị', 
       N'Tự động sinh: Quyền tra cứu hồ sơ thiết bị', 
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW'
);

-- 3. Gán quyền này cho vai trò ADMIN
INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId)
SELECT SYS_GUID(), 
       (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1), 
       (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW' AND ROWNUM = 1)
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM ROLE_PERMISSION 
    WHERE RoleId = (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND PermissionId = (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW' AND ROWNUM = 1)
)
AND (SELECT Id FROM ROLE WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL
AND (SELECT Id FROM PERMISSION WHERE Code = 'SEARCH_DOSSIERS_BY_EQUIPMENT_VIEW' AND ROWNUM = 1) IS NOT NULL;

COMMIT;
