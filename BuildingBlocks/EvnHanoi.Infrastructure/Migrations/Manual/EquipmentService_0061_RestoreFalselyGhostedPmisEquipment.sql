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
-- Trước khi chạy tay trên production, nên SELECT trước để xem trước số dòng/nội dung sẽ bị đổi:
--
-- SELECT e.Id, e.Code, e.Name, e.PMIS_CODE, e.INFRASTRUCTURE_ID, e.StatusTransition, e.ModifiedBy
-- FROM EQUIPMENTS e
-- WHERE e.StatusTransition = 0
--   AND e.IsDeleted = 0
--   AND e.ModifiedBy = 'PMIS_SYNC'
--   AND e.PMIS_CODE IS NOT NULL
--   AND NOT EXISTS (
--       SELECT 1 FROM EQUIPMENTS e2
--       WHERE UPPER(TRIM(e2.PMIS_CODE)) = UPPER(TRIM(e.PMIS_CODE))
--         AND e2.IsDeleted = 0 AND e2.StatusTransition IS NULL AND e2.Id <> e.Id
--   )
--   AND NOT EXISTS (
--       SELECT 1 FROM EQUIPMENTS e3
--       WHERE e3.INFRASTRUCTURE_ID = e.INFRASTRUCTURE_ID AND e3.Code = e.Code
--         AND e3.IsDeleted = 0 AND e3.StatusTransition IS NULL AND e3.Id <> e.Id
--   );

UPDATE EQUIPMENTS e
SET StatusTransition = NULL,
    ModifiedBy = 'MIGRATION_0061_RESTORE_GHOST',
    ModifiedDate = SYSTIMESTAMP
WHERE e.StatusTransition = 0
  AND e.IsDeleted = 0
  AND e.ModifiedBy = 'PMIS_SYNC'
  AND e.PMIS_CODE IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM EQUIPMENTS e2
      WHERE UPPER(TRIM(e2.PMIS_CODE)) = UPPER(TRIM(e.PMIS_CODE))
        AND e2.IsDeleted = 0
        AND e2.StatusTransition IS NULL
        AND e2.Id <> e.Id
  )
  AND NOT EXISTS (
      SELECT 1 FROM EQUIPMENTS e3
      WHERE e3.INFRASTRUCTURE_ID = e.INFRASTRUCTURE_ID
        AND e3.Code = e.Code
        AND e3.IsDeleted = 0
        AND e3.StatusTransition IS NULL
        AND e3.Id <> e.Id
  );
