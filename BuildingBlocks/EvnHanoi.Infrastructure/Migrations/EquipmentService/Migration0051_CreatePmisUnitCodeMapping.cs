using DbUp.Engine;
using System;
using System.Collections.Generic;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Ánh xạ mã đơn vị PMIS (maDonVi, vd. "HN0200") sang UnitId của ORGANIZATION_UNIT — bắt buộc phải có
/// bảng ánh xạ riêng vì 2 bộ mã không khớp trực tiếp (PMIS "HN0200" ≠ ORGANIZATION_UNIT.Code "HN02"),
/// xác nhận bằng dữ liệu thật gọi trực tiếp gateway PMIS (xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md) —
/// đây là nguyên nhân gốc khiến EQUIPMENTS.UnitId/INFRASTRUCTURE.UNIT_ID luôn NULL sau khi đồng bộ.
///
/// Tự seed 12 đơn vị theo đúng quy luật đã xác nhận thật (maDonVi PMIS = Code hệ thống + "00") — đúng
/// cho toàn bộ 12 đơn vị "HN%" hiện có. Có 1 ngoại lệ đã biết không theo quy luật này ("PD6800 - Công ty
/// lưới điện cao thế") — KHÔNG tự seed, cần EVN/Admin xác nhận + tự thêm dòng riêng khi đơn vị đó tồn
/// tại trong ORGANIZATION_UNIT.
/// </summary>
public class Migration0051_CreatePmisUnitCodeMapping : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            cmd.CommandText = @"
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
                )";
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
            {
                // Bảng đã tồn tại.
            }
        }

        var units = new List<(long UnitId, string Code)>();
        using (var selectCmd = dbCommandFactory())
        {
            selectCmd.CommandText = "SELECT Id, Code FROM ORGANIZATION_UNIT WHERE Code LIKE 'HN%' AND IsDeleted = 0";
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                units.Add((Convert.ToInt64(reader["Id"]), reader["Code"].ToString() ?? string.Empty));
            }
        }

        foreach (var (unitId, code) in units)
        {
            using var insertCmd = dbCommandFactory();
            insertCmd.CommandText = @"
                INSERT INTO PMIS_UNIT_CODE_MAPPING (Id, PmisUnitCode, UnitId, Note, CreatedBy)
                VALUES (:Id, :PmisUnitCode, :UnitId, :Note, 'MIGRATION_AUTO_SEED')";

            AddParameter(insertCmd, "Id", Guid.CreateVersion7().ToString());
            AddParameter(insertCmd, "PmisUnitCode", code + "00");
            AddParameter(insertCmd, "UnitId", unitId);
            AddParameter(insertCmd, "Note",
                "Tự suy ra từ quy luật PMIS \"HN\" + 2 số + \"00\", xác nhận bằng dữ liệu thật — xem BAO_CAO_TEST_API_PMIS_GATEWAY_THAT.md.");

            try
            {
                insertCmd.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex.Message.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase))
            {
                // Mã đã tồn tại (chạy lại migration thủ công) — bỏ qua.
            }
        }

        return string.Empty;
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
