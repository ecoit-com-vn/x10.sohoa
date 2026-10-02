-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0065_CreateDocumentEquipmentsTable.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0065_CreateDocumentEquipmentsTable.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0065_CreateDocumentEquipmentsTable.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: bảng DOCUMENT_EQUIPMENTS — liên kết Tài liệu <-> Thiết bị (nhiều-nhiều), thay cho liên kết
-- Hồ sơ <-> Thiết bị cũ (DOSSIER_EQUIPMENTS), vì giờ thiết bị được phân loại ở cấp Tài liệu đính kèm,
-- không phải ở cấp Hồ sơ. 1 tài liệu có thể gắn nhiều thiết bị.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist" (unqualified name sẽ tìm nhầm trong schema của user đang kết nối, không phải
-- QLSHX10), và kiểm tra tồn tại bảng/index dùng ALL_TABLES/ALL_INDEXES lọc theo OWNER thay vì
-- USER_TABLES/USER_INDEXES (USER_* chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối
-- THẲNG bằng user QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP TABLE QLSHX10.DOCUMENT_EQUIPMENTS;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0065_CreateDocumentEquipmentsTable%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_tables WHERE owner = 'QLSHX10' AND table_name = 'DOCUMENT_EQUIPMENTS';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE QLSHX10.DOCUMENT_EQUIPMENTS (
                DocumentId  VARCHAR2(36) NOT NULL,
                EquipmentId VARCHAR2(36) NOT NULL,
                CONSTRAINT PK_DOCUMENT_EQUIPMENTS PRIMARY KEY (DocumentId, EquipmentId),
                CONSTRAINT FK_DOC_EQUIP_DOCUMENT FOREIGN KEY (DocumentId) REFERENCES QLSHX10.DOCUMENTS(Id) ON DELETE CASCADE,
                CONSTRAINT FK_DOC_EQUIP_EQUIPMENT FOREIGN KEY (EquipmentId) REFERENCES QLSHX10.EQUIPMENTS(Id) ON DELETE CASCADE
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang DOCUMENT_EQUIPMENTS.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang DOCUMENT_EQUIPMENTS da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IDX_DOC_EQUIP_EQUIPMENT';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IDX_DOC_EQUIP_EQUIPMENT ON QLSHX10.DOCUMENT_EQUIPMENTS (EquipmentId)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IDX_DOC_EQUIP_EQUIPMENT.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_DOC_EQUIP_EQUIPMENT da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT table_name FROM all_tables WHERE owner = 'QLSHX10' AND table_name = 'DOCUMENT_EQUIPMENTS';
-- SELECT index_name FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IDX_DOC_EQUIP_EQUIPMENT';
