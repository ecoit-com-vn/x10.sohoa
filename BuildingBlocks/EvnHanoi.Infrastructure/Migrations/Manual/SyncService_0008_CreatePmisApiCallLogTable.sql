-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0008_CreatePmisApiCallLogTable.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0008_CreatePmisApiCallLogTable.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0008_CreatePmisApiCallLogTable.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: tạo bảng PMIS_API_CALL_LOG — ghi lại mọi lần gọi PMIS thật (thành công lẫn thất bại):
-- API nào, URL/payload thật đã gọi, trạng thái trả về, lỗi nếu có, thời gian gọi. Hiển thị qua màn
-- "Cấu hình kết nối API" (nút "Lịch sử gọi"). Dọn tự động sau 30 ngày qua Quartz job — nếu bảng phình
-- to bất thường trước khi job đó kịp chạy, xem câu DELETE mẫu ở cuối file.
--
-- ROLLBACK thủ công:
--   DROP INDEX IDX_PMIS_API_CALL_LOG_CODE_TIME;
--   DROP TABLE PMIS_API_CALL_LOG;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0008_CreatePmisApiCallLogTable%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tables WHERE table_name = 'PMIS_API_CALL_LOG';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE PMIS_API_CALL_LOG (
                Id              VARCHAR2(36)   NOT NULL PRIMARY KEY,
                ApiCode         VARCHAR2(50)   NOT NULL,
                HttpMethod      VARCHAR2(10)   NOT NULL,
                Url             VARCHAR2(2000) NOT NULL,
                RequestPayload  NVARCHAR2(2000) NULL,
                StatusCode      NUMBER         NULL,
                IsSuccess       NUMBER(1)      NOT NULL,
                ErrorMessage    NVARCHAR2(2000) NULL,
                DurationMs      NUMBER         NOT NULL,
                HttpClientName  VARCHAR2(30)   NULL,
                CalledAt        TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang PMIS_API_CALL_LOG.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang PMIS_API_CALL_LOG da ton tai, bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'IDX_PMIS_API_CALL_LOG_CODE_TIME';
    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX IDX_PMIS_API_CALL_LOG_CODE_TIME ON PMIS_API_CALL_LOG (ApiCode, CalledAt DESC)';
        DBMS_OUTPUT.PUT_LINE('Da tao index IDX_PMIS_API_CALL_LOG_CODE_TIME.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index IDX_PMIS_API_CALL_LOG_CODE_TIME da ton tai, bo qua.');
    END IF;

    COMMIT;
END;
/

-- KIỂM TRA:
-- SELECT table_name FROM user_tables WHERE table_name = 'PMIS_API_CALL_LOG';
-- SELECT COUNT(*) FROM PMIS_API_CALL_LOG;

-- DỌN TAY (nếu cần trước khi Quartz job kịp chạy) — giữ 30 ngày gần nhất:
-- DELETE FROM PMIS_API_CALL_LOG WHERE CalledAt < SYSTIMESTAMP - INTERVAL '30' DAY;
-- COMMIT;
