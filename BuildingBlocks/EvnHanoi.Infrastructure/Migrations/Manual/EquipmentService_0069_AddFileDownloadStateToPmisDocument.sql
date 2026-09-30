-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0069_AddFileDownloadStateToPmisDocument.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0069_AddFileDownloadStateToPmisDocument.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0069_AddFileDownloadStateToPmisDocument.cs', SYSTIMESTAMP);
--   COMMIT;
--   (nếu bảng SCHEMAVERSIONS báo ORA-01400 thiếu SCHEMAVERSIONID: thêm cột SCHEMAVERSIONID vào INSERT)
--
-- NỘI DUNG: tách đồng bộ danh sách tài liệu PMIS khỏi tải file vật lý — PMIS_DOCUMENT thành hàng đợi
-- tải file. FILE_STATUS: NO_URL | PENDING | DONE | FAILED. Dòng đã có ObjectKey được backfill DONE.
--
-- SCHEMA: mọi tham chiếu gắn tiền tố QLSHX10, kiểm tra tồn tại dùng ALL_TAB_COLUMNS/ALL_INDEXES lọc OWNER.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IDX_PMIS_DOCUMENT_FILE_QUEUE;
--   ALTER TABLE QLSHX10.PMIS_DOCUMENT DROP (FILE_URL, FILE_SOURCE_API, FILE_STATUS, FILE_ATTEMPTS, FILE_LAST_ERROR, FILE_NEXT_RETRY_AT);
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0069_AddFileDownloadStateToPmisDocument%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    PROCEDURE add_col(p_col VARCHAR2, p_def VARCHAR2) IS
        v_exists NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_exists FROM all_tab_columns
        WHERE owner = 'QLSHX10' AND table_name = 'PMIS_DOCUMENT' AND column_name = p_col;
        IF v_exists = 0 THEN
            EXECUTE IMMEDIATE 'ALTER TABLE QLSHX10.PMIS_DOCUMENT ADD ' || p_col || ' ' || p_def;
            DBMS_OUTPUT.PUT_LINE('Da them cot ' || p_col);
        ELSE
            DBMS_OUTPUT.PUT_LINE('Cot ' || p_col || ' da ton tai, bo qua.');
        END IF;
    END;
BEGIN
    add_col('FILE_URL', 'VARCHAR2(1000) NULL');
    add_col('FILE_SOURCE_API', 'VARCHAR2(50) NULL');
    add_col('FILE_STATUS', 'VARCHAR2(20) DEFAULT ''NO_URL'' NOT NULL');
    add_col('FILE_ATTEMPTS', 'NUMBER DEFAULT 0 NOT NULL');
    add_col('FILE_LAST_ERROR', 'NVARCHAR2(2000) NULL');
    add_col('FILE_NEXT_RETRY_AT', 'TIMESTAMP NULL');

    EXECUTE IMMEDIATE 'UPDATE QLSHX10.PMIS_DOCUMENT SET FILE_STATUS = ''DONE'' WHERE ObjectKey IS NOT NULL AND FILE_STATUS = ''NO_URL''';

    DECLARE v_idx NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_idx FROM all_indexes
        WHERE owner = 'QLSHX10' AND index_name = 'IDX_PMIS_DOCUMENT_FILE_QUEUE';
        IF v_idx = 0 THEN
            EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IDX_PMIS_DOCUMENT_FILE_QUEUE ON QLSHX10.PMIS_DOCUMENT (FILE_STATUS, FILE_NEXT_RETRY_AT)';
        END IF;
    END;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT FILE_STATUS, COUNT(*) FROM QLSHX10.PMIS_DOCUMENT GROUP BY FILE_STATUS;
