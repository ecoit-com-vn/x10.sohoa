-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0064_AddNormalizedPmisCodeIndexes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0064_AddNormalizedPmisCodeIndexes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0064_AddNormalizedPmisCodeIndexes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm 2 index hàm (không UNIQUE) trên UPPER(TRIM(PMIS_CODE)) của INFRASTRUCTURE/EQUIPMENTS —
-- khớp ĐÚNG biểu thức dùng trong mọi câu tra cứu PMIS_CODE của luồng đồng bộ PMIS (trước đây không index
-- nào khớp, buộc full table scan mỗi lần gọi — xem comment đầy đủ trong file .cs). Chỉ CỘNG THÊM, không
-- đụng dữ liệu/constraint hiện có, an toàn chạy trên môi trường đang có dữ liệu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist" (unqualified name sẽ tìm nhầm trong schema của user đang kết nối, không phải
-- QLSHX10), và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_INFRASTRUCTURE_PMIS_CODE_NORM;
--   DROP INDEX QLSHX10.IX_EQUIPMENTS_PMIS_CODE_NORM;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0064_AddNormalizedPmisCodeIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_INFRASTRUCTURE_PMIS_CODE_NORM';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_INFRASTRUCTURE_PMIS_CODE_NORM ON QLSHX10.INFRASTRUCTURE (UPPER(TRIM(PMIS_CODE)))';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_PMIS_CODE_NORM.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_PMIS_CODE_NORM da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_EQUIPMENTS_PMIS_CODE_NORM';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_EQUIPMENTS_PMIS_CODE_NORM ON QLSHX10.EQUIPMENTS (UPPER(TRIM(PMIS_CODE)))';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_PMIS_CODE_NORM.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_PMIS_CODE_NORM da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM all_indexes WHERE owner = 'QLSHX10' AND index_name LIKE '%PMIS_CODE_NORM';
