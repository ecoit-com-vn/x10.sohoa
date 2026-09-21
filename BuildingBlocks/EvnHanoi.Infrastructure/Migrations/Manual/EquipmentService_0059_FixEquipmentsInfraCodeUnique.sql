-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0059_FixEquipmentsInfraCodeUnique.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0059_FixEquipmentsInfraCodeUnique.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0059_FixEquipmentsInfraCodeUnique.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- PHỤ THUỘC: chạy SAU EquipmentService_0038 (tạo UQ_EQUIPMENTS_INFRA_CODE).
--
-- NỘI DUNG: sửa lỗi UQ_EQUIPMENTS_INFRA_CODE (tạo ở 0038) tính cả dòng đã xoá mềm (IsDeleted=1) VÀ dòng
-- "đã chuyển TBA" (StatusTransition=0) — khi 1 thiết bị PMIS bị chuyển sang Trạm/Đường dây khác, dòng CŨ
-- chỉ được đánh StatusTransition=0 (giữ nguyên INFRASTRUCTURE_ID/CODE), "hồn ma" này vẫn chiếm vĩnh viễn ô
-- (INFRASTRUCTURE_ID, CODE) trong index dù mọi truy vấn nghiệp vụ đều coi nó vô hình. Nếu thiết bị đó quay
-- lại đúng trạm đã từng chuyển đi, INSERT dòng thay thế mới đâm vào ô hồn ma cũ đang chiếm -> ORA-00001.
-- Thay bằng unique index hàm chỉ tính dòng "sống" (IsDeleted=0 AND StatusTransition IS NULL): khi không
-- sống, CẢ 2 biểu thức đều NULL, Oracle bỏ qua khoá toàn NULL trong unique index nên hồn ma/đã xoá không
-- còn chặn nhau, còn trùng thật (2 thiết bị đang sống) vẫn bị chặn đúng như thiết kế.
--
-- ROLLBACK thủ công (chỉ chạy được nếu không còn hồn ma nào đang trùng slot với dòng sống):
--   DROP INDEX UX_EQUIPMENTS_ACTIVE_INFRA_CODE;
--   ALTER TABLE EQUIPMENTS ADD CONSTRAINT UQ_EQUIPMENTS_INFRA_CODE UNIQUE (INFRASTRUCTURE_ID, CODE);
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0059_FixEquipmentsInfraCodeUnique%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

-- An toàn: chặn sớm với thông báo rõ nghĩa nếu hiện đang có >=2 dòng "sống" thật sự trùng
-- (InfrastructureId, Code) — CREATE UNIQUE INDEX bên dưới sẽ tự thất bại với ORA-01452 nếu không kiểm
-- trước, khó chẩn đoán hơn nhiều so với báo lỗi rõ nghĩa ngay tại đây.
DECLARE
    duplicate_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO duplicate_count FROM (
        SELECT INFRASTRUCTURE_ID, CODE FROM EQUIPMENTS
        WHERE ISDELETED = 0 AND STATUSTRANSITION IS NULL
          AND INFRASTRUCTURE_ID IS NOT NULL AND CODE IS NOT NULL
        GROUP BY INFRASTRUCTURE_ID, CODE HAVING COUNT(*) > 1
    );
    IF duplicate_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001,
            'Cannot create active EQUIPMENTS infra+code unique index: active duplicates exist.');
    END IF;
END;
/

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists
    FROM user_constraints
    WHERE constraint_name = 'UQ_EQUIPMENTS_INFRA_CODE'
      AND table_name = 'EQUIPMENTS';

    IF v_exists > 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE EQUIPMENTS DROP CONSTRAINT UQ_EQUIPMENTS_INFRA_CODE';
        DBMS_OUTPUT.PUT_LINE('Da bo constraint UQ_EQUIPMENTS_INFRA_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Constraint UQ_EQUIPMENTS_INFRA_CODE khong ton tai — bo qua.');
    END IF;

    SELECT COUNT(*) INTO v_exists FROM user_indexes WHERE index_name = 'UX_EQUIPMENTS_ACTIVE_INFRA_CODE';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE UNIQUE INDEX UX_EQUIPMENTS_ACTIVE_INFRA_CODE ON EQUIPMENTS (
                CASE WHEN ISDELETED = 0 AND STATUSTRANSITION IS NULL THEN INFRASTRUCTURE_ID END,
                CASE WHEN ISDELETED = 0 AND STATUSTRANSITION IS NULL THEN CODE END
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao unique index UX_EQUIPMENTS_ACTIVE_INFRA_CODE.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index UX_EQUIPMENTS_ACTIVE_INFRA_CODE da ton tai — bo qua.');
    END IF;
END;
/

-- KIỂM TRA: chỉ còn PK + unique index mới, không còn UQ_EQUIPMENTS_INFRA_CODE.
-- SELECT constraint_name, constraint_type FROM user_constraints WHERE table_name = 'EQUIPMENTS';
-- SELECT index_name, uniqueness FROM user_indexes WHERE table_name = 'EQUIPMENTS';
