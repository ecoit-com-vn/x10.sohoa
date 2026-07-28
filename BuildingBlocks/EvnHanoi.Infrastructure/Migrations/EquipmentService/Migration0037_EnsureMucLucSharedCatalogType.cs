using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// MUC_LUC là loại danh mục hệ thống dùng chung; dữ liệu con được cô lập theo UnitId.
/// </summary>
public class Migration0037_EnsureMucLucSharedCatalogType : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            MERGE INTO CATALOG_TYPE target
            USING (SELECT 'MUC_LUC' AS Code FROM DUAL) source
               ON (target.Code = source.Code)
            WHEN MATCHED THEN UPDATE SET
                target.Name = N'Mục lục hồ sơ',
                target.HasParent = 1,
                target.Description = N'Danh mục mục lục hồ sơ',
                target.IsPrivate = 0,
                target.Status = 1,
                target.IsDeleted = 0,
                target.UpdatedAt = CURRENT_TIMESTAMP,
                target.UpdatedBy = 'system'
            WHEN NOT MATCHED THEN INSERT
                (Id, Code, Name, HasParent, Description, IsPrivate, Status, IsDeleted, CreatedBy)
            VALUES
                (SEQ_CATALOG_TYPE_ID.NEXTVAL, 'MUC_LUC', N'Mục lục hồ sơ', 1,
                 N'Danh mục mục lục hồ sơ', 0, 1, 0, 'system')";
        command.ExecuteNonQuery();
        return string.Empty;
    }
}
