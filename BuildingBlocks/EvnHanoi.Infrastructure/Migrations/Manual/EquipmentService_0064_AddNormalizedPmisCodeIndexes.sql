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
-- ROLLBACK thủ công:
--   DROP INDEX IX_INFRASTRUCTURE_PMIS_CODE_NORM;
--   DROP INDEX IX_EQUIPMENTS_PMIS_CODE_NORM;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0064_AddNormalizedPmisCodeIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IX_INFRASTRUCTURE_PMIS_CODE_NORM';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_INFRASTRUCTURE_PMIS_CODE_NORM ON INFRASTRUCTURE (UPPER(TRIM(PMIS_CODE)))';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_INFRASTRUCTURE_PMIS_CODE_NORM.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_INFRASTRUCTURE_PMIS_CODE_NORM da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IX_EQUIPMENTS_PMIS_CODE_NORM';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IX_EQUIPMENTS_PMIS_CODE_NORM ON EQUIPMENTS (UPPER(TRIM(PMIS_CODE)))';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_EQUIPMENTS_PMIS_CODE_NORM.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_EQUIPMENTS_PMIS_CODE_NORM da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM user_indexes WHERE index_name LIKE '%PMIS_CODE_NORM';
