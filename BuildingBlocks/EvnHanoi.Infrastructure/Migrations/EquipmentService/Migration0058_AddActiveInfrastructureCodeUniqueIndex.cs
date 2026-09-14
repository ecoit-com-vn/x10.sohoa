using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Cho phép tái sử dụng mã cơ sở hạ tầng (Trạm biến áp, Đường dây) sau khi bản ghi trước đó đã được xóa mềm.
/// Thay thế ràng buộc UNIQUE toàn cục (SYS_C0032443 / unnamed UNIQUE trên cột CODE) bằng UNIQUE INDEX hàm
/// chỉ áp dụng cho các bản ghi chưa bị xóa (ISDELETED = 0).
/// </summary>
public class Migration0058_AddActiveInfrastructureCodeUniqueIndex : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();

        // 1. Kiểm tra nếu có bản ghi trùng mã trong số các bản ghi đang active (ISDELETED = 0)
        command.CommandText = @"
            DECLARE
                duplicate_count NUMBER;
            BEGIN
                SELECT COUNT(*)
                  INTO duplicate_count
                  FROM (
                      SELECT UPPER(TRIM(CODE))
                        FROM INFRASTRUCTURE
                       WHERE ISDELETED = 0
                         AND TRIM(CODE) IS NOT NULL
                       GROUP BY UPPER(TRIM(CODE))
                      HAVING COUNT(*) > 1
                  );

                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(
                        -20001,
                        'Cannot create active infrastructure code unique index because active normalized codes are duplicated.');
                END IF;
            END;";
        command.ExecuteNonQuery();

        // 2. Tạo Unique Index cho các bản ghi chưa xóa mềm (ISDELETED = 0)
        try
        {
            command.CommandText = @"
                CREATE UNIQUE INDEX UQ_INFRASTRUCTURE_ACTIVE_CODE
                    ON INFRASTRUCTURE (
                        CASE
                            WHEN ISDELETED = 0 THEN UPPER(TRIM(CODE))
                        END
                    )";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // Target index đã tồn tại từ lần chạy trước
        }

        // 3. Xóa mọi Unique Constraint cũ trên cột CODE của bảng INFRASTRUCTURE (ví dụ SYS_C0032443)
        // và các Unique Index cũ chỉ gồm cột CODE (trừ UQ_INFRASTRUCTURE_ACTIVE_CODE vừa tạo)
        command.CommandText = @"
            DECLARE
            BEGIN
                FOR unique_constraint IN (
                    SELECT uc.CONSTRAINT_NAME
                      FROM USER_CONSTRAINTS uc
                     WHERE uc.TABLE_NAME = 'INFRASTRUCTURE'
                       AND uc.CONSTRAINT_TYPE = 'U'
                       AND EXISTS (
                           SELECT 1
                             FROM USER_CONS_COLUMNS ucc
                            WHERE ucc.CONSTRAINT_NAME = uc.CONSTRAINT_NAME
                              AND ucc.TABLE_NAME = uc.TABLE_NAME
                              AND ucc.COLUMN_NAME = 'CODE'
                       )
                       AND NOT EXISTS (
                           SELECT 1
                             FROM USER_CONS_COLUMNS ucc
                            WHERE ucc.CONSTRAINT_NAME = uc.CONSTRAINT_NAME
                              AND ucc.TABLE_NAME = uc.TABLE_NAME
                              AND ucc.COLUMN_NAME <> 'CODE'
                       )
                ) LOOP
                    EXECUTE IMMEDIATE
                        'ALTER TABLE INFRASTRUCTURE DROP CONSTRAINT ' ||
                        unique_constraint.CONSTRAINT_NAME;
                END LOOP;

                FOR unique_index IN (
                    SELECT ui.INDEX_NAME
                      FROM USER_INDEXES ui
                     WHERE ui.TABLE_NAME = 'INFRASTRUCTURE'
                       AND ui.UNIQUENESS = 'UNIQUE'
                       AND ui.INDEX_NAME <> 'UQ_INFRASTRUCTURE_ACTIVE_CODE'
                       AND EXISTS (
                           SELECT 1
                             FROM USER_IND_COLUMNS uic
                            WHERE uic.INDEX_NAME = ui.INDEX_NAME
                              AND uic.TABLE_NAME = ui.TABLE_NAME
                              AND uic.COLUMN_NAME = 'CODE'
                       )
                       AND NOT EXISTS (
                           SELECT 1
                             FROM USER_IND_COLUMNS uic
                            WHERE uic.INDEX_NAME = ui.INDEX_NAME
                              AND uic.TABLE_NAME = ui.TABLE_NAME
                              AND uic.COLUMN_NAME <> 'CODE'
                       )
                ) LOOP
                    EXECUTE IMMEDIATE
                        'DROP INDEX ' ||
                        unique_index.INDEX_NAME;
                END LOOP;
            END;";
        command.ExecuteNonQuery();

        return string.Empty;
    }
}
