-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0012_AddCleanupIndexes.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0012_AddCleanupIndexes.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0012_AddCleanupIndexes.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: thêm 2 index (không UNIQUE) cho 2 cột mốc thời gian dùng trong job dọn dẹp định kỳ (audit
-- PMIS 2026-09-24) — DeleteOlderThanAsync ở cả PmisApiCallLogRepository và SyncHistoryRepository DELETE
-- theo WHERE <cột thời gian> < SYSTIMESTAMP - :RetentionDays, trước đây không index nào khớp leading
-- column nên full table scan, ngày càng chậm khi bảng tích luỹ theo thời gian (xem comment đầy đủ trong
-- file .cs):
--   1) IX_PMIS_API_CALL_LOG_CALLED_AT ON PMIS_API_CALL_LOG (CalledAt)
--   2) IX_SYNC_HISTORY_START_TIME     ON SYNC_HISTORY (START_TIME)
-- Chỉ CỘNG THÊM, không đụng dữ liệu/constraint hiện có, an toàn chạy trên môi trường đang có dữ liệu.
--
-- SCHEMA: script này giả định chạy dưới 1 user CÓ QUYỀN nhưng KHÔNG PHẢI chủ schema ứng dụng (vd
-- SYS) — mọi tham chiếu bảng/index đều gắn tiền tố "QLSHX10." tường minh, tránh ORA-00942 "table or
-- view does not exist" (unqualified name sẽ tìm nhầm trong schema của user đang kết nối, không phải
-- QLSHX10), và kiểm tra tồn tại index dùng ALL_INDEXES lọc theo OWNER thay vì USER_INDEXES
-- (USER_INDEXES chỉ thấy schema của chính user đang kết nối). Nếu bạn kết nối THẲNG bằng user
-- QLSHX10, tiền tố này thừa nhưng vô hại.
--
-- ROLLBACK thủ công:
--   DROP INDEX QLSHX10.IX_PMIS_API_CALL_LOG_CALLED_AT;
--   DROP INDEX QLSHX10.IX_SYNC_HISTORY_START_TIME;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0012_AddCleanupIndexes%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_PMIS_API_CALL_LOG_CALLED_AT';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_PMIS_API_CALL_LOG_CALLED_AT ON QLSHX10.PMIS_API_CALL_LOG (CalledAt)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_PMIS_API_CALL_LOG_CALLED_AT.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_PMIS_API_CALL_LOG_CALLED_AT da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM all_indexes WHERE owner = 'QLSHX10' AND index_name = 'IX_SYNC_HISTORY_START_TIME';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX QLSHX10.IX_SYNC_HISTORY_START_TIME ON QLSHX10.SYNC_HISTORY (START_TIME)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IX_SYNC_HISTORY_START_TIME.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IX_SYNC_HISTORY_START_TIME da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT index_name, table_name FROM all_indexes WHERE owner = 'QLSHX10'
-- AND index_name IN ('IX_PMIS_API_CALL_LOG_CALLED_AT','IX_SYNC_HISTORY_START_TIME');
