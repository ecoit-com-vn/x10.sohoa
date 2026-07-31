using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0041_ConfigurePhongCatalog : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"BEGIN
            UPDATE CATALOG_TYPE
               SET IsPrivate = 1, HasParent = 0,
                   UpdatedAt = CURRENT_TIMESTAMP, UpdatedBy = 'system'
             WHERE Code = 'PHONG' AND IsDeleted = 0;

            UPDATE CATALOG
               SET ParentId = NULL, UpdatedAt = CURRENT_TIMESTAMP, UpdatedBy = 'system'
             WHERE ParentId IS NOT NULL
               AND CatalogTypeId IN (SELECT Id FROM CATALOG_TYPE WHERE Code = 'PHONG' AND IsDeleted = 0);
        END;";
        command.ExecuteNonQuery();
        return string.Empty;
    }
}
