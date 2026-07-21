using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0036_SeedEquipmentStatusCatalogType : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        // 1. Seed EQUIPMENT_STATUS vào CATALOG_TYPE
        ExecuteNonQuery(@"
            MERGE INTO CATALOG_TYPE t USING DUAL ON (t.Code = 'EQUIPMENT_STATUS')
            WHEN MATCHED THEN UPDATE SET
                t.Name = N'Tình trạng thiết bị',
                t.HasParent = 0,
                t.Description = N'Danh mục Tình trạng thiết bị',
                t.IsPrivate = 0,
                t.Status = 1,
                t.IsDeleted = 0
            WHEN NOT MATCHED THEN INSERT (Id, Code, Name, HasParent, Description, IsPrivate, Status, IsDeleted, CreatedBy)
                VALUES (SEQ_CATALOG_TYPE_ID.NEXTVAL, 'EQUIPMENT_STATUS', N'Tình trạng thiết bị', 0, N'Danh mục Tình trạng thiết bị', 0, 1, 0, 'system')
        ");

        // 2. Seed giá trị mặc định vào CATALOG
        string[] codes = { "DANG_VAN_HANH", "NGUNG_VAN_HANH" };
        string[] names = { "Đang vận hành", "Ngừng vận hành" };

        for (int i = 0; i < codes.Length; i++)
        {
            var mergeSql = $@"
                MERGE INTO CATALOG t USING (
                    SELECT '{codes[i]}' AS Code, N'{names[i]}' AS Name, (SELECT Id FROM CATALOG_TYPE WHERE Code = 'EQUIPMENT_STATUS') AS CatalogTypeId FROM DUAL
                ) s ON (t.Code = s.Code AND t.CatalogTypeId = s.CatalogTypeId)
                WHEN MATCHED THEN UPDATE SET t.Name = s.Name
                WHEN NOT MATCHED THEN INSERT (Code, Name, CatalogTypeId, Priority, Status, IsDeleted, CreatedBy)
                    VALUES (s.Code, s.Name, s.CatalogTypeId, {i + 1}, 1, 0, 'system')
            ";
            ExecuteNonQuery(mergeSql);
        }

        return string.Empty;
    }
}
