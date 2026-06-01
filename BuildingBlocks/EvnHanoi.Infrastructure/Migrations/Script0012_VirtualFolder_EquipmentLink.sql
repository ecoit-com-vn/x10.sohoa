-- BuildingBlocks/EvnHanoi.Infrastructure/Migrations/Script0012_VirtualFolder_EquipmentLink.sql
-- Bổ sung liên kết giữa thư mục ảo với thiết bị cụ thể
-- Ngày tạo: 2026-05-30

ALTER TABLE VIRTUAL_FOLDERS ADD EquipmentId VARCHAR2(36) NULL;
ALTER TABLE VIRTUAL_FOLDERS ADD CONSTRAINT fk_vfolder_equip FOREIGN KEY (EquipmentId) REFERENCES Equipments(Id) ON DELETE CASCADE;
