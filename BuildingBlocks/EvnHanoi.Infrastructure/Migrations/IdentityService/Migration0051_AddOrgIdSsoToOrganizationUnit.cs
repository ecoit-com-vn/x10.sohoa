using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

/// <summary>
/// ORGANIZATION_UNIT.ORGIDSSO — OrganizationUnitRepository (CreateAsync/UpdateAsync/GetAllAsync/
/// GetByIdAsync/GetOrganizationUnitsHierarchicalAsync) và SsoAccountService đã tham chiếu cột này từ
/// trước, nhưng CHƯA từng có migration nào thật sự tạo nó — cột chỉ được ALTER TABLE thủ công 1 lần
/// trên 1 máy Oracle dùng chung (xem scratch/temp_oracle_test/Program.cs, không nằm trong pipeline
/// migration) nên môi trường nào chỉ chạy đúng migration chính thức sẽ thiếu cột này, gây
/// ORA-00904 (invalid identifier "ORGIDSSO") mỗi khi tạo/sửa/xem đơn vị.
/// </summary>
public sealed class Migration0051_AddOrgIdSsoToOrganizationUnit : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        AddColumn(command, "ORGANIZATION_UNIT", "ORGIDSSO", "VARCHAR2(100) NULL");
        return string.Empty;
    }

    private static void AddColumn(IDbCommand command, string table, string column, string definition)
    {
        command.CommandText = @"
            SELECT COUNT(*) FROM USER_TAB_COLUMNS
            WHERE TABLE_NAME = :TableName AND COLUMN_NAME = :ColumnName";
        AddParameter(command, "TableName", table);
        AddParameter(command, "ColumnName", column);
        var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        if (exists) return;
        command.CommandText = $"ALTER TABLE {table} ADD {column} {definition}";
        command.ExecuteNonQuery();
    }

    private static void AddParameter(IDbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
