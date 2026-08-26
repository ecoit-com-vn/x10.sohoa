using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Ánh xạ mã loại thiết bị PMIS (maLoaiTB, vd. "MBA") sang EquipmentTypeId của EquipmentTypes — bắt buộc
/// vì 2 bộ mã không cùng quy ước: PMIS có ~66 mã loại thiết bị KHÔNG phân biệt cấp điện áp, hệ thống có
/// ~33 mã loại thiết bị PHÂN BIỆT cấp điện áp bằng hậu tố (vd. "MC_CA"/"MC_TA") — 1 mã PMIS có thể ứng
/// với 1 trong 2 mã hệ thống khác nhau tuỳ cấp điện áp thực tế của thiết bị, không thể so khớp trực tiếp
/// Code = Code. Xác nhận bằng dữ liệu thật — xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md.
///
/// KHÔNG tự seed nội dung — cần người hiểu danh mục thiết bị (Admin) tự cấu hình qua màn "Ánh xạ loại
/// thiết bị PMIS", vì suy đoán máy móc có rủi ro gán sai loại thiết bị. Chỉ tạo bảng rỗng ở đây.
/// </summary>
public class Migration0052_CreatePmisEquipmentTypeMapping : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();
        try
        {
            cmd.CommandText = @"
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
                )";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Bảng đã tồn tại.
        }

        return string.Empty;
    }
}
