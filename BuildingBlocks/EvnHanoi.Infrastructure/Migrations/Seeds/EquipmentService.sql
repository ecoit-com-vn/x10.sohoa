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

-- Seed EquipmentTypes
INSERT INTO EquipmentTypes (Id, Name, Code, Description, CreatedAt, UpdatedAt, GridTypeId, SortOrder, IsActive)
SELECT '019eb000-0000-7000-8000-000000000001', 'Thiết bị đo lường', 'TB_DO_LUONG', 'Các thiết bị đo lường kiểm thử dòng điện', SYSTIMESTAMP, SYSTIMESTAMP, 1, 1, 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM EquipmentTypes WHERE Id = '019eb000-0000-7000-8000-000000000001');

INSERT INTO EquipmentTypes (Id, Name, Code, Description, CreatedAt, UpdatedAt, GridTypeId, SortOrder, IsActive)
SELECT '019eb000-0000-7000-8000-000000000002', 'Máy biến áp lực', 'MBI_AP_LUC', 'Các máy biến áp công suất lớn trong trạm', SYSTIMESTAMP, SYSTIMESTAMP, 1, 2, 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM EquipmentTypes WHERE Id = '019eb000-0000-7000-8000-000000000002');

INSERT INTO EquipmentTypes (Id, Name, Code, Description, CreatedAt, UpdatedAt, GridTypeId, SortOrder, IsActive)
SELECT '019eb000-0000-7000-8000-000000000003', 'Thiết bị đóng cắt', 'TB_DONG_CAT', 'Máy cắt, cầu dao cách ly, tủ trung thế', SYSTIMESTAMP, SYSTIMESTAMP, 1, 3, 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM EquipmentTypes WHERE Id = '019eb000-0000-7000-8000-000000000003');

-- Seed Equipments
INSERT INTO Equipments (Id, EquipmentTypeId, Name, Code, SerialNumber, UnitId, CreatedAt, CreatedBy, RowVersion)
SELECT '019eb100-0000-7000-8000-000000000001', '019eb000-0000-7000-8000-000000000001', 'Đồng hồ vạn năng Fluke 179', 'FLK-179', 'SN-FLUKE-9921', 1, SYSTIMESTAMP, 'admin', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Equipments WHERE Id = '019eb100-0000-7000-8000-000000000001');

INSERT INTO Equipments (Id, EquipmentTypeId, Name, Code, SerialNumber, UnitId, CreatedAt, CreatedBy, RowVersion)
SELECT '019eb100-0000-7000-8000-000000000002', '019eb000-0000-7000-8000-000000000002', 'Máy biến áp ABB 110kV 63MVA', 'ABB-63MVA', 'SN-ABB-88321', 1, SYSTIMESTAMP, 'admin', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Equipments WHERE Id = '019eb100-0000-7000-8000-000000000002');

INSERT INTO Equipments (Id, EquipmentTypeId, Name, Code, SerialNumber, UnitId, CreatedAt, CreatedBy, RowVersion)
SELECT '019eb100-0000-7000-8000-000000000003', '019eb000-0000-7000-8000-000000000003', 'Tủ điện đóng cắt RM6 Schneider', 'RM6-SCH', 'SN-SCH-44219', 1, SYSTIMESTAMP, 'admin', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Equipments WHERE Id = '019eb100-0000-7000-8000-000000000003');

-- Seed Dossiers đã bị xóa — Schema bảng DOSSIERS đã được thiết kế lại (Migration0009)
-- Dữ liệu mẫu hồ sơ sẽ được tạo thủ công qua UI sau khi deploy

COMMIT;


