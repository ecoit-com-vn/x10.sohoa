using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Migration0031_AddUnitIdToPhysicalShelf
/// - Thêm cột UnitId (1 kệ thuộc đúng 1 đơn vị).
/// - Gỡ ràng buộc FondsId / PHYSICAL_FONDS còn sót từ thiết kế cũ.
/// </summary>
public class Migration0031_AddUnitIdToPhysicalShelf : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                    {
                        ignored = true;
                        break;
                    }
                }

                if (!ignored)
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        // 1. Thêm UnitId (nullable để không phá dữ liệu cũ)
        ExecuteNonQuery("ALTER TABLE PHYSICAL_SHELF ADD UnitId NUMBER NULL", 1430);

        // 2. FK tới ORGANIZATION_UNIT
        ExecuteNonQuery(
            @"ALTER TABLE PHYSICAL_SHELF ADD CONSTRAINT fk_phys_shelf_unit
              FOREIGN KEY (UnitId) REFERENCES ORGANIZATION_UNIT(Id)",
            2275); // ORA-02275: such a referential constraint already exists

        // 3. Index lọc theo đơn vị
        ExecuteNonQuery("CREATE INDEX IDX_PHYSICAL_SHELF_UNIT_ID ON PHYSICAL_SHELF(UnitId)", 955);

        // 4. Gỡ FK / cột FondsId cũ (nếu còn)
        ExecuteNonQuery("ALTER TABLE PHYSICAL_SHELF DROP CONSTRAINT fk_phys_shelf_fonds", 2443, 2322);
        ExecuteNonQuery("ALTER TABLE PHYSICAL_SHELF DROP COLUMN FondsId", 904);

        // 5. Xóa bảng PHYSICAL_FONDS nếu còn
        ExecuteNonQuery("DROP TABLE PHYSICAL_FONDS CASCADE CONSTRAINTS", 942);

        return string.Empty;
    }
}
