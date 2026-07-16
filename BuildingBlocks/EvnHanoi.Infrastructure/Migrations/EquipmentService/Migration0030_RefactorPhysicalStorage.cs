using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService
{
    /// <summary>
    /// Migration0030_RefactorPhysicalStorage
    /// - Xóa bảng PHYSICAL_FONDS.
    /// - Thêm các cột Status, IsDeleted, Priority vào PHYSICAL_SHELF, PHYSICAL_FLOOR, PHYSICAL_BOX.
    /// </summary>
    public class Migration0030_RefactorPhysicalStorage : IScript
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
                        if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase) ||
                            ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase) ||
                            ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                        {
                            ignored = true;
                            break;
                        }
                    }
                    if (!ignored)
                        throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
                }
            }
            // 2. Add new columns to PHYSICAL_SHELF
            ExecuteNonQuery(@"ALTER TABLE PHYSICAL_SHELF ADD (
                STATUS      NUMBER(1)   DEFAULT 1 NOT NULL,
                IS_DELETED  NUMBER(1)   DEFAULT 0 NOT NULL,
                PRIORITY    NUMBER(3)   DEFAULT 1 NOT NULL
            )");

            // 3. Add new columns to PHYSICAL_FLOOR
            ExecuteNonQuery(@"ALTER TABLE PHYSICAL_FLOOR ADD (
                STATUS      NUMBER(1)   DEFAULT 1 NOT NULL,
                IS_DELETED  NUMBER(1)   DEFAULT 0 NOT NULL,
                PRIORITY    NUMBER(3)   DEFAULT 1 NOT NULL
            )");

            // 4. Add new columns to PHYSICAL_BOX
            ExecuteNonQuery(@"ALTER TABLE PHYSICAL_BOX ADD (
                STATUS      NUMBER(1)   DEFAULT 1 NOT NULL,
                IS_DELETED  NUMBER(1)   DEFAULT 0 NOT NULL,
                PRIORITY    NUMBER(3)   DEFAULT 1 NOT NULL
            )");

            return string.Empty;
        }
    }
}
