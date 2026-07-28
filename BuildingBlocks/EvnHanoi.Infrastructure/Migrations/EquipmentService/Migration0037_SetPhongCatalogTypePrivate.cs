using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Phông là loại danh mục riêng theo đơn vị. Đây vẫn là loại hệ thống nên
/// CreatedBy được giữ là system để mọi người dùng có quyền danh mục riêng
/// nhìn thấy cùng một loại PHONG; dữ liệu bên trong được cô lập bằng UnitId.
/// </summary>
public class Migration0037_SetPhongCatalogTypePrivate : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = @"
            UPDATE CATALOG_TYPE
               SET IsPrivate = 1,
                   CreatedBy = 'system',
                   UpdatedAt = CURRENT_TIMESTAMP,
                   UpdatedBy = 'system'
             WHERE Code = 'PHONG'
               AND IsDeleted = 0";
        command.ExecuteNonQuery();
        return string.Empty;
    }
}
