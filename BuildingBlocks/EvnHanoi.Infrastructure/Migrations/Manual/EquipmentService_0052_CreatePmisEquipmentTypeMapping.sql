-- ============================================================================
-- BẢN SQL DỰ PHÒNG cho Migrations/EquipmentService/Migration0052_CreatePmisEquipmentTypeMapping.cs
-- ============================================================================
-- KHÔNG chạy tự động — xem giải thích đầy đủ ở
-- Migrations/Manual/SyncService_0003_AddRowVersionAndIsDeletedToSyncConfig.sql.
--
-- CÁCH CHẠY: sqlplus <user>/<pass>@<host>:1521/<service> @EquipmentService_0052_CreatePmisEquipmentTypeMapping.sql
-- Sau khi chạy tay, ghi journal:
--   INSERT INTO SCHEMAVERSIONS (SCRIPTNAME, APPLIED)
--   VALUES ('EvnHanoi.Infrastructure.Migrations.EquipmentService.Migration0052_CreatePmisEquipmentTypeMapping.cs', SYSTIMESTAMP);
--   COMMIT;
--
-- LƯU Ý: chạy tiếp EquipmentService_0054_FixPmisEquipmentTypeMappingUnique.sql ngay sau file này —
-- 0054 thay ràng buộc UNIQUE ở đây bằng unique index chỉ tính dòng chưa xoá (nếu không, xoá 1 ánh xạ
-- rồi thêm lại đúng cặp đó sẽ bị báo trùng).
--
-- NỘI DUNG: bảng PMIS_EQUIPMENT_TYPE_MAPPING — ánh xạ mã loại thiết bị PMIS (maLoaiTB, vd. "MBA") +
-- cấp điện áp sang EquipmentTypes.Id. Bắt buộc vì 2 bộ mã không cùng quy ước: PMIS có ~66 mã KHÔNG phân
-- biệt cấp điện áp, hệ thống có ~33 mã PHÂN BIỆT bằng hậu tố ("MC_CA"/"MC_TA") — 1 mã PMIS ứng với mã
-- hệ thống khác nhau tuỳ cấp điện áp thực tế, không thể so khớp Code = Code.
--
-- ⚠ BẮT BUỘC ĐỌC TRƯỚC KHI TRIỂN KHAI PRODUCTION:
-- Bảng này CỐ Ý để RỖNG (ánh xạ đúng cần người hiểu danh mục thiết bị, đoán máy móc sẽ gán sai loại).
-- Từ phiên bản này, đồng bộ Thiết bị từ PMIS chỉ tra loại thiết bị qua bảng này — KHÔNG còn so khớp
-- trực tiếp EquipmentTypes.Code. Nghĩa là ngay sau khi deploy, MỌI thiết bị đồng bộ sẽ báo lỗi
-- "Chưa cấu hình ánh xạ loại thiết bị PMIS '<mã>' (cấp ...)" cho tới khi Admin điền ánh xạ ở màn
-- "Quản trị hệ thống > Ánh xạ loại thiết bị PMIS". Lỗi tính theo từng thiết bị, không làm hỏng cả lượt
-- đồng bộ. Xem thêm BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md §7.
--
-- ROLLBACK thủ công:
--   DROP TABLE PMIS_EQUIPMENT_TYPE_MAPPING;
--   DELETE FROM SCHEMAVERSIONS WHERE SCRIPTNAME LIKE '%0052_CreatePmisEquipmentTypeMapping%';
--   COMMIT;
-- ============================================================================
SET SERVEROUTPUT ON

DECLARE
    v_exists NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_exists FROM user_tables WHERE table_name = 'PMIS_EQUIPMENT_TYPE_MAPPING';

    IF v_exists = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE PMIS_EQUIPMENT_TYPE_MAPPING (
                Id               VARCHAR2(36)  NOT NULL,
                PmisMaLoaiTB     VARCHAR2(50)  NOT NULL,
                GridTypeId       NUMBER        NOT NULL,
                EquipmentTypeId  VARCHAR2(36)  NOT NULL,
                RowVersion       NUMBER        DEFAULT 1 NOT NULL,
                CreatedBy        VARCHAR2(100) NULL,
                CreatedDate      TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
                ModifiedBy       VARCHAR2(100) NULL,
                ModifiedDate     TIMESTAMP     NULL,
                IsDeleted        NUMBER(1)     DEFAULT 0 NOT NULL,
                CONSTRAINT PK_PMIS_EQUIPMENT_TYPE_MAPPING PRIMARY KEY (Id),
                CONSTRAINT UQ_PMIS_EQUIPMENT_TYPE_MAPPING UNIQUE (PmisMaLoaiTB, GridTypeId),
                CONSTRAINT FK_PMIS_EQTYPE_MAPPING_GRIDTYPE FOREIGN KEY (GridTypeId)
                    REFERENCES GRIDTYPES(Id),
                CONSTRAINT FK_PMIS_EQTYPE_MAPPING_EQTYPE FOREIGN KEY (EquipmentTypeId)
                    REFERENCES EquipmentTypes(Id),
                CONSTRAINT CK_PMIS_EQTYPE_MAPPING_DEL CHECK (IsDeleted IN (0, 1))
            )';
        DBMS_OUTPUT.PUT_LINE('Da tao bang PMIS_EQUIPMENT_TYPE_MAPPING (rong, cho Admin cau hinh).');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Bang PMIS_EQUIPMENT_TYPE_MAPPING da ton tai — bo qua.');
    END IF;
END;
/

-- ============================================================================
-- THÊM ÁNH XẠ THỦ CÔNG (nếu muốn nhập bằng SQL thay vì qua màn hình).
-- Thay <MA_LOAI_TB_PMIS>, <CAP_DIEN_AP: 1=Cao áp, 2=Trung áp, 3=Hạ áp>, <CODE_LOAI_TB_HE_THONG>:
-- ============================================================================
-- INSERT INTO PMIS_EQUIPMENT_TYPE_MAPPING (Id, PmisMaLoaiTB, GridTypeId, EquipmentTypeId, CreatedBy)
-- SELECT LOWER(REGEXP_REPLACE(RAWTOHEX(SYS_GUID()), '(.{8})(.{4})(.{4})(.{4})(.{12})', '\1-\2-\3-\4-\5')),
--        '<MA_LOAI_TB_PMIS>', <CAP_DIEN_AP>, et.Id, 'MANUAL'
-- FROM EquipmentTypes et WHERE et.Code = '<CODE_LOAI_TB_HE_THONG>' AND et.IsDeleted = 0;
-- COMMIT;

-- Xem các mã loại thiết bị hệ thống hiện có để chọn (kèm cấp điện áp của từng mã):
-- SELECT et.Code, et.Name, et.GridTypeId, gt.Name AS GridTypeName
-- FROM EquipmentTypes et LEFT JOIN GridTypes gt ON gt.Id = et.GridTypeId
-- WHERE et.IsDeleted = 0 ORDER BY et.GridTypeId, et.Code;
