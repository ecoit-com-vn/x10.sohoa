-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0073_RestoreBornGhostEquipment.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0073_RestoreBornGhostEquipment.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0073_RestoreBornGhostEquipment.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: chạy SAU EquipmentService_0059_FixEquipmentsInfraCodeUnique, _0060 (cả 2 file: unique index
-- PMIS_CODE + bảng EQUIPMENT_TRANSFER_HISTORY), _0066 (sửa DEFAULT) và NÊN SAU _0072 (xoá index cũ — nếu
-- chưa xoá, thiết bị vừa khôi phục vẫn bị index cũ chặn khi đồng bộ lại).
--
-- NỘI DUNG: khôi phục thiết bị "SINH RA ĐÃ LÀ HỒN MA" — EQUIPMENTS.STATUSTRANSITION từng có DEFAULT '0' ở mức
-- DB nên MỌI thiết bị PMIS_SYNC tạo mới trước Migration0066 đều bị gắn "Đã chuyển TBA" ngay lúc INSERT dù
-- chưa từng chuyển đi đâu. Migration0066 chỉ sửa DEFAULT cho dòng MỚI. Số liệu thật 2026-10-01: production
-- 133.904 dòng (toàn bộ CreatedBy='PMIS_SYNC', ModifiedBy/ModifiedDate NULL, EQUIPMENT_TRANSFER_HISTORY 0 dòng
-- → không có ca chuyển trạm thật nào); UAT 56.884 dòng. Production ước tính khôi phục được ~131.469 dòng,
-- ~2.435 dòng để nguyên ghost vì đã có bản "sống" cùng mã.
--
-- ĐIỀU KIỆN KHÔI PHỤC (đủ cả 5 — không đụng chuyển TBA thật; giải thích đầy đủ trong file .cs):
-- 1. StatusTransition=0, IsDeleted=0, CreatedBy='PMIS_SYNC', ModifiedBy IS NULL, ModifiedDate IS NULL.
-- 2. Không là SourceEquipmentId trong EQUIPMENT_TRANSFER_HISTORY.
-- 3. INFRASTRUCTURE_ID và CODE khác NULL.
-- 4. Chưa có dòng "sống" nào cùng (INFRASTRUCTURE_ID, CODE) hoặc cùng PMIS_CODE (UPPER/TRIM).
-- 5. Mỗi nhóm ghost cùng (INFRASTRUCTURE_ID, CODE) hoặc cùng PMIS_CODE chỉ chọn 1 dòng (CreatedAt mới nhất).
--
-- AN TOÀN: chia lô 10.000 dòng, COMMIT sau mỗi lô, tính lại điều kiện mỗi lô; gặp ORA-00001 (job đồng bộ vừa
-- tạo dòng sống trùng giữa chừng) thì ROLLBACK lô đó và thử lại tối đa 5 lần. Idempotent: chạy lại chỉ chọn dòng
-- còn thoả điều kiện. Mỗi dòng được khôi phục tự gắn ModifiedBy = 'MIGRATION_0073_RESTORE_BORN_GHOST'.
--
-- KIỂM TRA TRƯỚC KHI CHẠY (số dòng sẽ được khôi phục xấp xỉ — dòng cuối của kết quả):
--   SELECT COUNT(*) AS GHOST_SINH_RA_DA_GHOST FROM QLSHX10.EQUIPMENTS
--   WHERE StatusTransition = 0 AND IsDeleted = 0 AND CreatedBy = 'PMIS_SYNC' AND ModifiedBy IS NULL AND ModifiedDate IS NULL;
--
-- LƯU Ý SAU KHI CHẠY: chỉ mục Elasticsearch của thiết bị (NotificationService) không tự biết các dòng vừa
-- khôi phục — cần resync thiết bị lên ES để tìm kiếm thấy chúng.
--
-- SCHEMA: tiền tố "QLSHX10." tường minh cho user không phải chủ schema (xem giải thích đầy đủ ở
-- EquipmentService_0061_RestoreFalselyGhostedPmisEquipment.sql). Nếu kết nối THẲNG bằng user QLSHX10,
-- tiền tố thừa nhưng vô hại.
--
-- ROLLBACK thủ công (chính xác — mọi dòng được khôi phục đều mang nhãn riêng, bản gốc ModifiedBy/ModifiedDate = NULL):
--   UPDATE QLSHX10.EQUIPMENTS SET StatusTransition = 0, ModifiedBy = NULL, ModifiedDate = NULL
--   WHERE ModifiedBy = 'MIGRATION_0073_RESTORE_BORN_GHOST';
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0073_RestoreBornGhostEquipment%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    c_batch       CONSTANT PLS_INTEGER := 10000;
    c_max_retries CONSTANT PLS_INTEGER := 5;
    c_max_batches CONSTANT PLS_INTEGER := 1000;
    v_batch_no    PLS_INTEGER := 0;
    v_retry       PLS_INTEGER;
    v_restored    NUMBER;
    v_total       NUMBER := 0;
BEGIN
    LOOP
        v_batch_no := v_batch_no + 1;
        v_retry := 0;

        <<retry_loop>>
        LOOP
            BEGIN
                UPDATE QLSHX10.EQUIPMENTS
                SET StatusTransition = NULL,
                    ModifiedBy = 'MIGRATION_0073_RESTORE_BORN_GHOST',
                    ModifiedDate = SYSTIMESTAMP
                WHERE Id IN (
                    SELECT Id FROM (
                        SELECT x.Id FROM (
                            SELECT g.Id,
                                   ROW_NUMBER() OVER (
                                       PARTITION BY COALESCE(UPPER(TRIM(g.PMIS_CODE)), g.Id)
                                       ORDER BY g.CreatedAt DESC, g.Id DESC) AS rn_code,
                                   ROW_NUMBER() OVER (
                                       PARTITION BY g.INFRASTRUCTURE_ID, g.Code
                                       ORDER BY g.CreatedAt DESC, g.Id DESC) AS rn_infra_code
                            FROM QLSHX10.EQUIPMENTS g
                            WHERE g.StatusTransition = 0
                              AND g.IsDeleted = 0
                              AND g.CreatedBy = 'PMIS_SYNC'
                              AND g.ModifiedBy IS NULL
                              AND g.ModifiedDate IS NULL
                              AND g.INFRASTRUCTURE_ID IS NOT NULL
                              AND g.Code IS NOT NULL
                              AND NOT EXISTS (
                                  SELECT 1 FROM QLSHX10.EQUIPMENT_TRANSFER_HISTORY h WHERE h.SourceEquipmentId = g.Id)
                              AND NOT EXISTS (
                                  SELECT 1 FROM QLSHX10.EQUIPMENTS l
                                  WHERE l.IsDeleted = 0 AND l.StatusTransition IS NULL
                                    AND l.INFRASTRUCTURE_ID = g.INFRASTRUCTURE_ID AND l.Code = g.Code)
                              AND (g.PMIS_CODE IS NULL OR NOT EXISTS (
                                  SELECT 1 FROM QLSHX10.EQUIPMENTS l2
                                  WHERE l2.IsDeleted = 0 AND l2.StatusTransition IS NULL
                                    AND UPPER(TRIM(l2.PMIS_CODE)) = UPPER(TRIM(g.PMIS_CODE))))
                        ) x
                        WHERE x.rn_code = 1 AND x.rn_infra_code = 1
                        ORDER BY x.Id
                    )
                    WHERE ROWNUM <= c_batch
                );
                v_restored := SQL%ROWCOUNT;
                EXIT retry_loop;
            EXCEPTION
                WHEN DUP_VAL_ON_INDEX THEN
                    ROLLBACK;
                    v_retry := v_retry + 1;
                    DBMS_OUTPUT.PUT_LINE('ORA-00001 giua chung (lan ' || v_retry || '), tinh lai lo va thu lai.');
                    IF v_retry >= c_max_retries THEN
                        RAISE;
                    END IF;
            END;
        END LOOP retry_loop;

        COMMIT;
        EXIT WHEN v_restored = 0;

        v_total := v_total + v_restored;
        DBMS_OUTPUT.PUT_LINE('Lo ' || v_batch_no || ': khoi phuc ' || v_restored || ' thiet bi (tong ' || v_total || ').');
        EXIT WHEN v_batch_no >= c_max_batches;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('Hoan tat: da khoi phuc ' || v_total || ' thiet bi sinh ra da la hon ma.');
END;
/

-- KIỂM TRA SAU KHI CHẠY:
-- SELECT COUNT(*) AS DA_KHOI_PHUC FROM QLSHX10.EQUIPMENTS WHERE ModifiedBy = 'MIGRATION_0073_RESTORE_BORN_GHOST';
-- SELECT COUNT(*) AS CON_LAI_GHOST FROM QLSHX10.EQUIPMENTS WHERE StatusTransition = 0 AND IsDeleted = 0;
-- -- Không được còn bản trùng "sống" (cả 2 truy vấn phải trả 0 dòng):
-- SELECT INFRASTRUCTURE_ID, CODE, COUNT(*) FROM QLSHX10.EQUIPMENTS WHERE IsDeleted = 0 AND StatusTransition IS NULL AND INFRASTRUCTURE_ID IS NOT NULL GROUP BY INFRASTRUCTURE_ID, CODE HAVING COUNT(*) > 1;
-- SELECT UPPER(TRIM(PMIS_CODE)), COUNT(*) FROM QLSHX10.EQUIPMENTS WHERE IsDeleted = 0 AND StatusTransition IS NULL AND PMIS_CODE IS NOT NULL GROUP BY UPPER(TRIM(PMIS_CODE)) HAVING COUNT(*) > 1;
