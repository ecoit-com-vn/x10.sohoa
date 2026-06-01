-- BuildingBlocks/EvnHanoi.Infrastructure/Migrations/Script0010_Equipment_Dossier_UnitId.sql
-- Thêm cột UnitId cho bảng Equipments và Dossiers phục vụ phân quyền theo đơn vị
-- Ngày tạo: 2026-05-30

ALTER TABLE Equipments ADD UnitId NUMBER NULL;
ALTER TABLE Dossiers ADD UnitId NUMBER NULL;
