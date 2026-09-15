-- Migration 0053: Thêm menu và quyền cho màn "Ánh xạ mã đơn vị PMIS" (PMIS_UNIT_CODE_MAPPING,
-- EquipmentService). Trước migration này, EquipmentService đã có sẵn API
-- (GET/POST api/v1/pmis-unit-code-mapping) nhưng KHÔNG có màn hình nào gọi tới — admin không có cách
-- nào thêm ánh xạ đơn vị PMIS mới ngoài nhờ dev tự chạy SQL tay, khiến các đơn vị mới thêm sau khi hệ
-- thống đã chạy (vd "Công ty Điện lực Đông Anh" HN07) không bao giờ map được Trạm/Đường dây dù đơn vị
-- đã tồn tại đúng trong ORGANIZATION_UNIT.

-- 1. PERMISSIONS
INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_unit_code_mapping_view_id',
       'PMIS_UNIT_CODE_MAPPING_VIEW',
       N'Ánh xạ mã đơn vị PMIS',
       N'Xem danh sách ánh xạ mã đơn vị PMIS với đơn vị hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_UNIT_CODE_MAPPING_VIEW'
);

INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
SELECT 'pmis_unit_code_mapping_create_id',
       'PMIS_UNIT_CODE_MAPPING_CREATE',
       N'Thêm ánh xạ mã đơn vị PMIS',
       N'Thêm ánh xạ mã đơn vị PMIS mới với đơn vị hệ thống',
       1,
       'SYSTEM'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM PERMISSION WHERE Code = 'PMIS_UNIT_CODE_MAPPING_CREATE'
);

-- 2. GÁN QUYỀN VÀO NHÓM ADMIN
INSERT INTO PERMISSION_GROUP_PERMISSION (Id, PermissionGroupId, PermissionId)
SELECT SYS_GUID(),
       (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1),
       p.Id
FROM PERMISSION p
WHERE p.Code IN ('PMIS_UNIT_CODE_MAPPING_VIEW', 'PMIS_UNIT_CODE_MAPPING_CREATE')
AND NOT EXISTS (
    SELECT 1
    FROM PERMISSION_GROUP_PERMISSION pgp
    WHERE pgp.PermissionGroupId = (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1)
      AND pgp.PermissionId = p.Id
)
AND (SELECT Id FROM PERMISSION_GROUP WHERE Code = 'ADMIN' AND ROWNUM = 1) IS NOT NULL;

-- 3. APP_MENU (dưới menu cha "Quản trị hệ thống", cùng nhóm với các menu Đồng bộ PMIS khác)
INSERT INTO APP_MENU (Name, Url, Icon, ParentId, SortOrder, IsActive, PermissionCode)
SELECT N'Ánh xạ mã đơn vị PMIS',
       '/pmis-sync/unit-mapping',
       'pi pi-sitemap',
       (SELECT Id FROM APP_MENU WHERE Name = N'Quản trị hệ thống' AND ROWNUM = 1),
       104,
       1,
       'PMIS_UNIT_CODE_MAPPING_VIEW'
FROM DUAL
WHERE NOT EXISTS (
    SELECT 1 FROM APP_MENU WHERE Url = '/pmis-sync/unit-mapping'
);

COMMIT;
