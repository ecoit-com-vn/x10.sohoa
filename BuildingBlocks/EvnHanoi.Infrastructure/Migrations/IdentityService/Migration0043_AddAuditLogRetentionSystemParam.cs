using System;
using System.Data;
using DbUp.Engine;

namespace EvnHanoi.Infrastructure.Migrations.IdentityService;

public sealed class Migration0043_AddAuditLogRetentionSystemParam : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        //command.CommandText = @"
        //    INSERT INTO SYSTEM_PARAM (ParamKey, ParamValue, Description, DataType)
        //    SELECT 'AuditLogRetentionDays', '90', 'Số ngày lưu nhật ký thao tác trước khi xóa vật lý', 'Number'
        //    FROM DUAL
        //    WHERE NOT EXISTS (
        //        SELECT 1
        //        FROM SYSTEM_PARAM
        //        WHERE ParamKey = 'AuditLogRetentionDays'
        //    )";
        command.CommandText = @"
    INSERT INTO SYSTEM_PARAM
        (ParamKey, ParamValue, Description, DataType)
    SELECT
        'AuditLogRetentionDays',
        '90',
        'Số ngày lưu trữ nhật ký hệ thống trước khi xóa vật lý',
        'Number'
    FROM DUAL
    WHERE NOT EXISTS (
        SELECT 1
        FROM SYSTEM_PARAM
        WHERE UPPER(TRIM(ParamKey)) = UPPER('AuditLogRetentionDays')
    )";
        command.ExecuteNonQuery();

        return string.Empty;
    }
}
