using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// Thêm cột thứ tự sắp xếp nullable cho ORGANIZATION_UNIT.
/// </summary>
public sealed class Migration0045_AddSortOrderToOrganizationUnit : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            SELECT COUNT(*)
            FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = 'ORGANIZATION_UNIT'
              AND COLUMN_NAME = 'SORTORDER'";

        var columnExists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        if (!columnExists)
        {
            command.CommandText =
                "ALTER TABLE ORGANIZATION_UNIT ADD SORTORDER NUMBER(10) NULL";
            command.ExecuteNonQuery();
        }

        return string.Empty;
    }
}
