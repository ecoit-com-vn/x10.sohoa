-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0060_CreateEquipmentTransferHistoryTable.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0060_CreateEquipmentTransferHistoryTable.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0060_CreateEquipmentTransferHistoryTable.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: tạo bảng EQUIPMENT_TRANSFER_HISTORY — lưu lịch sử mỗi lần "Chuyển thiết bị" (đổi Trạm/Đường
-- dây quản lý). Cơ chế chuyển hiện tại tạo 1 bản ghi EQUIPMENTS mới (Id mới) và đánh dấu bản ghi cũ
-- IsActive=0, không có bảng lịch sử nào lưu lại các lần chuyển trước đó. Gom theo EquipmentCode (không
-- đổi qua các lần chuyển) vì Id đổi mỗi lần chuyển nên không dùng được để truy vết toàn bộ chuỗi lịch sử.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP TABLE QLSHX10.EQUIPMENT_TRANSFER_HISTORY;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0060_CreateEquipmentTransferHistoryTable%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM ALL_TABLES WHERE OWNER = 'QLSHX10' AND TABLE_NAME = 'EQUIPMENT_TRANSFER_HISTORY';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE QLSHX10.EQUIPMENT_TRANSFER_HISTORY (
                Id                      VARCHAR2(36)   NOT NULL,
                EquipmentCode           VARCHAR2(100)  NOT NULL,
                SourceEquipmentId       VARCHAR2(36)   NULL,
                TargetEquipmentId       VARCHAR2(36)   NOT NULL,
                SourceInfrastructureId  VARCHAR2(36)   NULL,
                TargetInfrastructureId  VARCHAR2(36)   NOT NULL,
                SourceUnitId            NUMBER         NULL,
                TargetUnitId            NUMBER         NULL,
                Note                    VARCHAR2(2000) NULL,
                TransferredBy           VARCHAR2(100)  NULL,
                TransferredAt           TIMESTAMP      DEFAULT SYSTIMESTAMP NOT NULL,
                CONSTRAINT PK_EQUIP_TRANSFER_HISTORY PRIMARY KEY (Id),
                CONSTRAINT FK_EQUIP_TRANSFER_HIST_TARGET FOREIGN KEY (TargetEquipmentId)
                    REFERENCES QLSHX10.EQUIPMENTS(Id) ON DELETE CASCADE
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang EQUIPMENT_TRANSFER_HISTORY.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang EQUIPMENT_TRANSFER_HISTORY da ton tai — bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM ALL_INDEXES WHERE OWNER = 'QLSHX10' AND INDEX_NAME = 'IDX_EQUIP_TRANSFER_HIST_CODE';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IDX_EQUIP_TRANSFER_HIST_CODE ON QLSHX10.EQUIPMENT_TRANSFER_HISTORY (EquipmentCode)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IDX_EQUIP_TRANSFER_HIST_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_EQUIP_TRANSFER_HIST_CODE da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA:
-- SELECT table_name FROM all_tables WHERE owner = 'QLSHX10' AND table_name = 'EQUIPMENT_TRANSFER_HISTORY';
-- SELECT index_name FROM all_indexes WHERE owner = 'QLSHX10' AND table_name = 'EQUIPMENT_TRANSFER_HISTORY';
