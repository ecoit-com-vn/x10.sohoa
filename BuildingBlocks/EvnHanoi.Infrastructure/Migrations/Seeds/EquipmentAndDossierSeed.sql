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

-- Seed Dossiers
INSERT INTO Dossiers (Id, EquipmentId, Title, Description, Status, Version, UnitId, CreatedAt, CreatedBy, RowVersion)
SELECT '019eb200-0000-7000-8000-000000000001', '019eb100-0000-7000-8000-000000000001', 'Hồ sơ kiểm định Đồng hồ Fluke 179', 'Tài liệu hướng dẫn sử dụng và chứng nhận kiểm định hiệu chuẩn', 'Active', 1, 1, SYSTIMESTAMP, 'admin', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Dossiers WHERE Id = '019eb200-0000-7000-8000-000000000001');

INSERT INTO Dossiers (Id, EquipmentId, Title, Description, Status, Version, UnitId, CreatedAt, CreatedBy, RowVersion)
SELECT '019eb200-0000-7000-8000-000000000002', '019eb100-0000-7000-8000-000000000002', 'Hồ sơ lý lịch Máy biến áp ABB 63MVA', 'Lý lịch vận hành, nhật ký thí nghiệm định kỳ, sự cố máy biến áp', 'Active', 1, 1, SYSTIMESTAMP, 'admin', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Dossiers WHERE Id = '019eb200-0000-7000-8000-000000000002');

INSERT INTO Dossiers (Id, EquipmentId, Title, Description, Status, Version, UnitId, CreatedAt, CreatedBy, RowVersion)
SELECT '019eb200-0000-7000-8000-000000000003', '019eb100-0000-7000-8000-000000000003', 'Hồ sơ thiết kế Tủ RM6 Schneider', 'Bản vẽ kỹ thuật đấu nối mạch nhị thứ và sơ đồ tủ đóng cắt RM6', 'Active', 1, 1, SYSTIMESTAMP, 'admin', 1 FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Dossiers WHERE Id = '019eb200-0000-7000-8000-000000000003');

COMMIT;
