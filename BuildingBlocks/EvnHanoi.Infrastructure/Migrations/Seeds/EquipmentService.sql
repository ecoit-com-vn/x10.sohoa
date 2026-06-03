-- Seed default Catalogs (UOMs)
INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_CAI', 'Cái', 'UnitOfMeasure', 'Đơn vị tính đếm chiếc' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_CAI');

INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_MET', 'Mét', 'UnitOfMeasure', 'Đơn vị đo chiều dài' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_MET');

INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_KG', 'Kilôgam', 'UnitOfMeasure', 'Đơn vị đo khối lượng' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_KG');

INSERT INTO CATALOG (Code, Name, CatalogType, Description)
SELECT 'UOM_BO', 'Bộ', 'UnitOfMeasure', 'Đơn vị tính theo bộ sản phẩm' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM CATALOG WHERE Code = 'UOM_BO');

-- Seed default Report Groups
INSERT INTO REPORT_GROUPS (Id, Name, SortOrder, Description)
SELECT 1, 'Báo cáo thiết bị', 1, 'Các báo cáo liên quan đến quản lý thiết bị và loại thiết bị' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM REPORT_GROUPS WHERE Id = 1);

-- Seed default Dynamic Reports
INSERT INTO DYNAMIC_REPORTS (GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive)
SELECT 
    1, 
    'Thống kê số lượng thiết bị theo loại', 
    'SELECT et.Name AS "Loại thiết bị", COUNT(e.Id) AS "Số lượng" FROM Equipments e JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id GROUP BY et.Name', 
    '[]', 
    'ADMIN,USER', 
    1 
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM DYNAMIC_REPORTS WHERE Name = 'Thống kê số lượng thiết bị theo loại');

INSERT INTO DYNAMIC_REPORTS (GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive)
SELECT 
    1, 
    'Danh sách thiết bị chi tiết', 
    'SELECT e.Id AS "Mã hệ thống", e.Code AS "Mã thiết bị", e.Name AS "Tên thiết bị", e.SerialNumber AS "Số Serial", et.Name AS "Loại thiết bị" FROM Equipments e JOIN EquipmentTypes et ON e.EquipmentTypeId = et.Id WHERE (:Name IS NULL OR e.Name LIKE ''%'' || :Name || ''%'')', 
    '[{"name": "Name", "type": "text", "label": "Tên thiết bị"}]', 
    'ADMIN,USER', 
    1 
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM DYNAMIC_REPORTS WHERE Name = 'Danh sách thiết bị chi tiết');

COMMIT;
