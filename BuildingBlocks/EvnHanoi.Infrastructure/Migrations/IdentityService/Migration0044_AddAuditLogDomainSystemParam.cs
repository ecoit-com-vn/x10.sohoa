using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

public sealed class Migration0044_AddAuditLogDomainSystemParam : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
    INSERT INTO SYSTEM_PARAM
        (ParamKey, ParamValue, Description, DataType)
    SELECT
        'AuditLogDomain',
        'https://example.com',
        'Domain dịch vụ nhật ký',
        'String'
    FROM DUAL
    WHERE NOT EXISTS (
        SELECT 1
        FROM SYSTEM_PARAM
        WHERE UPPER(TRIM(ParamKey)) = UPPER('AuditLogDomain')
    )";
        command.ExecuteNonQuery();

        return string.Empty;
    }
}
