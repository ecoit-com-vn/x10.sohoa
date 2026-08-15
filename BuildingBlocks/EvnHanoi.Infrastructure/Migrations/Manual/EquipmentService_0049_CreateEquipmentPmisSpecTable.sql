-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0049_CreateEquipmentPmisSpecTable.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0049_CreateEquipmentPmisSpecTable.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0049_CreateEquipmentPmisSpecTable.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: bảng EQUIPMENT_PMIS_SPEC — bản "thông số kỹ thuật" đồng bộ riêng từ PMIS cho từng
-- thiết bị, KHÔNG ghi đè EQUIPMENTS.FORM_VALUES (dữ liệu người dùng chỉnh sửa nội bộ). 1 dòng/thiết
-- bị, dùng cho tính năng so sánh sai khác trên màn chi tiết thiết bị.
-- ROLLBACK thủ công:
--   DROP TABLE EQUIPMENT_PMIS_SPEC;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0049_CreateEquipmentPmisSpecTable%';
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tables WHERE table_name = 'EQUIPMENT_PMIS_SPEC';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE EQUIPMENT_PMIS_SPEC (
                Id                     VARCHAR2(36)  NOT NULL,
                EquipmentId            VARCHAR2(36)  NOT NULL,
                FormTemplateVersionId  VARCHAR2(36)  NULL,
                FormValues             CLOB          NULL,
                SyncedAt               TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                SyncHistoryId          VARCHAR2(36)  NULL,
                RowVersion             NUMBER        DEFAULT 1 NOT NULL,
                CreatedBy              VARCHAR2(100) NULL,
                CreatedDate            TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                ModifiedBy             VARCHAR2(100) NULL,
                ModifiedDate           TIMESTAMP     NULL,
                IsDeleted              NUMBER(1)     DEFAULT 0 NOT NULL,
                CONSTRAINT PK_EQUIPMENT_PMIS_SPEC PRIMARY KEY (Id),
                CONSTRAINT UQ_EQUIPMENT_PMIS_SPEC_EQUIP UNIQUE (EquipmentId),
                CONSTRAINT FK_EQUIPMENT_PMIS_SPEC_EQUIP FOREIGN KEY (EquipmentId)
                    REFERENCES EQUIPMENTS(Id) ON DELETE CASCADE,
                CONSTRAINT FK_EQUIPMENT_PMIS_SPEC_FORMVER FOREIGN KEY (FormTemplateVersionId)
                    REFERENCES EavFormTemplateVersions(Id),
                CONSTRAINT CK_EQUIPMENT_PMIS_SPEC_DELETED CHECK (IsDeleted IN (0, 1))
            )';
        DBMS_OUTPUT.PUT_LINE('Đã tạo bảng EQUIPMENT_PMIS_SPEC');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bảng EQUIPMENT_PMIS_SPEC đã tồn tại, bỏ qua');
    END IF;

    DBMS_OUTPUT.PUT_LINE('Hoàn tất.');
END;
/
