using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Cho phép tái sử dụng mã loại tài liệu sau khi bản ghi trước đó đã được xóa mềm.
/// Chỉ áp dụng ràng buộc duy nhất cho mã của các bản ghi chưa bị xóa.
/// </summary>
public class Migration0044_AddActiveDocumentTypeCodeUniqueIndex : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();

        command.CommandText = @"
            DECLARE
                duplicate_count NUMBER;
            BEGIN
                SELECT COUNT(*)
                  INTO duplicate_count
                  FROM (
                      SELECT UPPER(TRIM(CODE))
                        FROM DOCUMENT_TYPES
                       WHERE ISDELETED = 0
                         AND TRIM(CODE) IS NOT NULL
                       GROUP BY UPPER(TRIM(CODE))
                      HAVING COUNT(*) > 1
                  );

                IF duplicate_count > 0 THEN
                    RAISE_APPLICATION_ERROR(
                        -20001,
                        'Cannot create active document type code unique index because active normalized codes are duplicated.');
                END IF;
            END;";
        command.ExecuteNonQuery();

        try
        {
            command.CommandText = @"
                CREATE UNIQUE INDEX UQ_DOCUMENT_TYPES_ACTIVE_CODE
                    ON DOCUMENT_TYPES (
                        CASE
                            WHEN ISDELETED = 0 THEN UPPER(TRIM(CODE))
                        END
                    )";
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex.Message.Contains("ORA-00955", StringComparison.OrdinalIgnoreCase))
        {
            // The target index was created by an earlier partial execution of this migration.
        }

        command.CommandText = @"
            DECLARE
            BEGIN
                FOR unique_constraint IN (
                    SELECT uc.CONSTRAINT_NAME
                      FROM USER_CONSTRAINTS uc
                     WHERE uc.TABLE_NAME = 'DOCUMENT_TYPES'
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
                        'ALTER TABLE DOCUMENT_TYPES DROP CONSTRAINT ' ||
                        unique_constraint.CONSTRAINT_NAME;
                END LOOP;

                FOR unique_index IN (
                    SELECT ui.INDEX_NAME
                      FROM USER_INDEXES ui
                     WHERE ui.TABLE_NAME = 'DOCUMENT_TYPES'
                       AND ui.UNIQUENESS = 'UNIQUE'
                       AND ui.INDEX_NAME <> 'UQ_DOCUMENT_TYPES_ACTIVE_CODE'
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
