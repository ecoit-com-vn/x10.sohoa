-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0051_CreatePmisUnitCodeMapping.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0051_CreatePmisUnitCodeMapping.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0051_CreatePmisUnitCodeMapping.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- NỘI DUNG: bảng PMIS_UNIT_CODE_MAPPING — ánh xạ mã đơn vị PMIS (maDonVi, vd. "HN0200") sang
-- ORGANIZATION_UNIT.Id. Bắt buộc phải có vì 2 bộ mã KHÔNG khớp trực tiếp (PMIS "HN0200" ≠ hệ thống
-- "HN02") — đây là nguyên nhân gốc khiến EQUIPMENTS.UnitId / INFRASTRUCTURE.UNIT_ID luôn NULL sau khi
-- đồng bộ PMIS. Xác nhận bằng dữ liệu thật, xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md.
--
-- Script này TỰ SEED các đơn vị có Code dạng 'HN%' theo quy luật PmisUnitCode = Code || '00'.
-- ⚠ KIỂM TRA SAU KHI CHẠY TRÊN PRODUCTION: nếu danh mục đơn vị production dùng quy ước mã khác 'HN%'
-- thì phần seed sẽ ra 0 dòng và mọi lần đồng bộ sẽ để UnitId = NULL (KHÔNG báo lỗi) — phải tự INSERT
-- ánh xạ đúng (xem phần "SEED THỦ CÔNG" ở cuối file).
-- Ngoại lệ đã biết: 'PD6800 - Công ty lưới điện cao thế' không theo quy luật, không được tự seed.
--
-- ROLLBACK thủ công:
--   DROP TABLE PMIS_UNIT_CODE_MAPPING;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0051_CreatePmisUnitCodeMapping%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
    v_seeded NUMBER := 0;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tables WHERE table_name = 'PMIS_UNIT_CODE_MAPPING';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE PMIS_UNIT_CODE_MAPPING (
                Id            VARCHAR2(36)  NOT NULL,
                PmisUnitCode  VARCHAR2(50)  NOT NULL,
                UnitId        NUMBER        NOT NULL,
                Note          VARCHAR2(500) NULL,
                RowVersion    NUMBER        DEFAULT 1 NOT NULL,
                CreatedBy     VARCHAR2(100) NULL,
                CreatedDate   TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                ModifiedBy    VARCHAR2(100) NULL,
                ModifiedDate  TIMESTAMP     NULL,
                IsDeleted     NUMBER(1)     DEFAULT 0 NOT NULL,
                CONSTRAINT PK_PMIS_UNIT_CODE_MAPPING PRIMARY KEY (Id),
                CONSTRAINT UQ_PMIS_UNIT_CODE_MAPPING_CODE UNIQUE (PmisUnitCode),
                CONSTRAINT FK_PMIS_UNIT_CODE_MAPPING_UNIT FOREIGN KEY (UnitId)
                    REFERENCES ORGANIZATION_UNIT(Id),
                CONSTRAINT CK_PMIS_UNIT_CODE_MAPPING_DEL CHECK (IsDeleted IN (0, 1))
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang PMIS_UNIT_CODE_MAPPING.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang PMIS_UNIT_CODE_MAPPING da ton tai — bo qua buoc tao.');
    END IF;

    -- Seed theo quy luật đã xác nhận thật: maDonVi PMIS = ORGANIZATION_UNIT.Code || '00'.
    FOR u IN (
        SELECT Id, Code FROM ORGANIZATION_UNIT WHERE Code LIKE 'HN%' AND IsDeleted = 0
    ) LOOP
        BEGIN
            INSERT INTO PMIS_UNIT_CODE_MAPPING (Id, PmisUnitCode, UnitId, Note, CreatedBy)
            VALUES (
                LOWER(REGEXP_REPLACE(RAWTOHEX(SYS_GUID()),
                    '(.{8})(.{4})(.{4})(.{4})(.{12})', '\1-\2-\3-\4-\5')),
                u.Code || '00',
                u.Id,
                'Tu suy ra tu quy luat PMIS "HN" + 2 so + "00", xac nhan bang du lieu that — xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md.',
                'MIGRATION_AUTO_SEED');
            v_seeded := v_seeded + 1;
        EXCEPTION
            WHEN DUP_VAL_ON_INDEX THEN NULL;  -- Mã đã có (chạy lại script) — bỏ qua.
        END;
    END LOOP;

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('So dong anh xa don vi da seed moi: ' || v_seeded);
    DBMS_OUTPUT.PUT_LINE('!! Kiem tra lai bang cau lenh o cuoi file truoc khi dong bo PMIS.');
END;
/

-- ============================================================================
-- KIỂM TRA SAU KHI CHẠY (chạy tay, đối chiếu với mã đơn vị PMIS thật đang dùng)
-- ============================================================================
-- SELECT m.PmisUnitCode, u.Code AS SystemCode, u.Name
-- FROM PMIS_UNIT_CODE_MAPPING m JOIN ORGANIZATION_UNIT u ON u.Id = m.UnitId
-- WHERE m.IsDeleted = 0 ORDER BY m.PmisUnitCode;

-- ============================================================================
-- SEED THỦ CÔNG (dùng khi mã đơn vị production không theo quy luật 'HN%' + '00',
-- hoặc để thêm ngoại lệ như PD6800). Thay <MA_PMIS> và <CODE_HE_THONG> rồi chạy:
-- ============================================================================
-- INSERT INTO PMIS_UNIT_CODE_MAPPING (Id, PmisUnitCode, UnitId, Note, CreatedBy)
-- SELECT LOWER(REGEXP_REPLACE(RAWTOHEX(SYS_GUID()), '(.{8})(.{4})(.{4})(.{4})(.{12})', '\1-\2-\3-\4-\5')),
--        '<MA_PMIS>', u.Id, 'Them tay khi trien khai production', 'MANUAL'
-- FROM ORGANIZATION_UNIT u WHERE u.Code = '<CODE_HE_THONG>' AND u.IsDeleted = 0;
-- COMMIT;
