-- Migration0061_RestoreFalselyGhostedPmisEquipment
-- Khôi phục thiết bị bị đánh oan StatusTransition=0 ("Đã chuyển TBA") do bug PMIS_CODE lệch chuẩn
-- (khoảng trắng/hoa-thường) trước khi UPPER(TRIM(...)) được thêm vào UpsertFromPmisAsync — xem
-- Migration0059_FixEquipmentsInfraCodeUnique / Migration0060_AddActivePmisCodeUniqueIndexes.
--
-- CHỈ khôi phục khi: (1) ModifiedBy = 'PMIS_SYNC' (không đụng chuyển TBA thật do người dùng thao tác
-- tay), (2) không còn bản ghi "sống" nào khác trùng PMIS_CODE đã chuẩn hoá, (3) không còn bản ghi
-- "sống" nào khác trùng (INFRASTRUCTURE_ID, Code) — tránh vi phạm UX_EQUIPMENTS_ACTIVE_PMIS_CODE /
-- UX_EQUIPMENTS_ACTIVE_INFRA_CODE.
--
-- SỬA LẦN 2 (sau khi chạy tay gặp ORA-00001 trên UX_EQUIPMENTS_ACTIVE_PMIS_CODE): 2 điều kiện
-- NOT EXISTS chỉ loại xung đột với dòng ĐANG SỐNG SẴN — nếu 2 dòng "hồn ma" khác nhau cùng trùng 1
-- PMIS_CODE đều không xung đột với dòng sống nào, cả 2 sẽ được UPDATE cùng lúc rồi tự đụng độ NGAY
-- VỚI NHAU. Thêm ROW_NUMBER() để mỗi nhóm PMIS_CODE và mỗi nhóm (INFRASTRUCTURE_ID, Code) chỉ chọn
-- đúng 1 dòng (ưu tiên ModifiedDate mới nhất). Oracle rollback toàn bộ statement khi lỗi — lần chạy
-- trước KHÔNG ghi gì vào DB, an toàn để chạy lại bản đã sửa này.
--
-- Trước khi chạy tay trên production, nên SELECT trước để xem trước số dòng/nội dung sẽ bị đổi:
--
-- SELECT x.* FROM (
--     SELECT c.Id, c.Code, c.Name, c.PMIS_CODE, c.INFRASTRUCTURE_ID, c.ModifiedDate,
--            ROW_NUMBER() OVER (PARTITION BY UPPER(TRIM(c.PMIS_CODE)) ORDER BY c.ModifiedDate DESC, c.Id DESC) AS rn_code,
--            ROW_NUMBER() OVER (PARTITION BY c.INFRASTRUCTURE_ID, c.Code ORDER BY c.ModifiedDate DESC, c.Id DESC) AS rn_infra_code
--     FROM EQUIPMENTS c
--     WHERE c.StatusTransition = 0
--       AND c.IsDeleted = 0
--       AND c.ModifiedBy = 'PMIS_SYNC'
--       AND c.PMIS_CODE IS NOT NULL
--       AND NOT EXISTS (
--           SELECT 1 FROM EQUIPMENTS e2
--           WHERE UPPER(TRIM(e2.PMIS_CODE)) = UPPER(TRIM(c.PMIS_CODE))
--             AND e2.IsDeleted = 0 AND e2.StatusTransition IS NULL AND e2.Id <> c.Id
--       )
--       AND NOT EXISTS (
--           SELECT 1 FROM EQUIPMENTS e3
--           WHERE e3.INFRASTRUCTURE_ID = c.INFRASTRUCTURE_ID AND e3.Code = c.Code
--             AND e3.IsDeleted = 0 AND e3.StatusTransition IS NULL AND e3.Id <> c.Id
--       )
-- ) x
-- WHERE x.rn_code = 1 AND x.rn_infra_code = 1;

UPDATE EQUIPMENTS e
SET StatusTransition = NULL,
    ModifiedBy = 'MIGRATION_0061_RESTORE_GHOST',
    ModifiedDate = SYSTIMESTAMP
WHERE e.Id IN (
    SELECT x.Id FROM (
        SELECT c.Id,
               ROW_NUMBER() OVER (
                   PARTITION BY UPPER(TRIM(c.PMIS_CODE))
                   ORDER BY c.ModifiedDate DESC, c.Id DESC) AS rn_code,
               ROW_NUMBER() OVER (
                   PARTITION BY c.INFRASTRUCTURE_ID, c.Code
                   ORDER BY c.ModifiedDate DESC, c.Id DESC) AS rn_infra_code
        FROM EQUIPMENTS c
        WHERE c.StatusTransition = 0
          AND c.IsDeleted = 0
          AND c.ModifiedBy = 'PMIS_SYNC'
          AND c.PMIS_CODE IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM EQUIPMENTS e2
              WHERE UPPER(TRIM(e2.PMIS_CODE)) = UPPER(TRIM(c.PMIS_CODE))
                AND e2.IsDeleted = 0
                AND e2.StatusTransition IS NULL
                AND e2.Id <> c.Id
          )
          AND NOT EXISTS (
              SELECT 1 FROM EQUIPMENTS e3
              WHERE e3.INFRASTRUCTURE_ID = c.INFRASTRUCTURE_ID
                AND e3.Code = c.Code
                AND e3.IsDeleted = 0
                AND e3.StatusTransition IS NULL
                AND e3.Id <> c.Id
          )
    ) x
    WHERE x.rn_code = 1 AND x.rn_infra_code = 1
);
