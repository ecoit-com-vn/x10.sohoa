using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Chuẩn hóa tên loại MUC_LUC để các màn hình hiển thị thống nhất.
/// </summary>
public class Migration0042_NormalizeMucLucCatalogTypeName : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            UPDATE CATALOG_TYPE
               SET Name = N'Danh mục mục lục hồ sơ',
                   HasParent = 1,
                   Status = 1,
                   IsDeleted = 0,
                   UpdatedAt = CURRENT_TIMESTAMP,
                   UpdatedBy = 'system'
             WHERE Code = 'MUC_LUC'";
        command.ExecuteNonQuery();
        return string.Empty;
    }
}
