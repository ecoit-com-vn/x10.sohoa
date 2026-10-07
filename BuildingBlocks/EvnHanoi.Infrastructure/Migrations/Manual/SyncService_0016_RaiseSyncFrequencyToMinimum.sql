-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/SyncService/Migration0016_RaiseSyncFrequencyToMinimum.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @SyncService_0016_RaiseSyncFrequencyToMinimum.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.SyncService.Migration0016_RaiseSyncFrequencyToMinimum.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: nâng tần suất đồng bộ tự động thấp hơn 2 giờ lên 2 giờ (mức tối thiểu mới). Idempotent.
-- SCHEMA: tiền tố "QLSHX10." tường minh (xem EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql).
--
-- ROLLBACK: không hoàn tác được giá trị cũ (không lưu lại) — đặt lại bằng UPDATE tay nếu cần.
-- ============================================================================
SET SERVEROUTPUT ON

UPDATE QLSHX10.SYNC_CONFIG
SET FREQUENCY_VALUE = 2, FREQUENCY_UNIT = 'HOUR', ROW_VERSION = ROW_VERSION + 1
WHERE (CASE FREQUENCY_UNIT WHEN 'MINUTE' THEN FREQUENCY_VALUE
                           WHEN 'HOUR' THEN FREQUENCY_VALUE * 60
                           WHEN 'DAY' THEN FREQUENCY_VALUE * 1440
                           ELSE FREQUENCY_VALUE END) < 120;

COMMIT;
