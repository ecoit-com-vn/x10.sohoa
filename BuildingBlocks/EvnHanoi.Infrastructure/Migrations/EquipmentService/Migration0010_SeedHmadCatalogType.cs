using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0010_SeedHmadCatalogType : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
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

            // 1. Seed HMAD into CATALOG_TYPE
            ExecuteNonQuery(@"
                MERGE INTO CATALOG_TYPE t USING DUAL ON (t.Code = 'HMAD')
                WHEN MATCHED THEN UPDATE SET t.Name = 'Hạng mục áp dụng', t.HasParent = 0
                WHEN NOT MATCHED THEN INSERT (Id, Code, Name, HasParent, Description, IsPrivate, Status, IsDeleted)
                    VALUES (SEQ_CATALOG_TYPE_ID.NEXTVAL, 'HMAD', 'Hạng mục áp dụng', 0, 'Danh mục Hạng mục áp dụng', 0, 1, 0)
            ");

            // 2. Seed the 9 categories under HMAD catalog type into CATALOG
            string[] codes = { "MAY_BIEN_AP", "MAY_CAT", "DAO_CACH_LY", "BIEN_DIEN_AP", "BIEN_DONG_DIEN", "CAP_DIEN_LUC", "TU_TRUNG_THE", "THIET_BI_DO_LUONG", "KHAC" };
            string[] names = { "Máy biến áp", "Máy cắt", "Dao cách ly", "Biến điện áp (TU)", "Biến dòng điện (TI)", "Cáp điện lực", "Tủ trung thế", "Thiết bị đo lường", "Hạng mục khác" };

            for (int i = 0; i < codes.Length; i++)
            {
                string mergeSql = $@"
                    MERGE INTO CATALOG t USING (
                        SELECT '{codes[i]}' AS Code, '{names[i]}' AS Name, (SELECT Id FROM CATALOG_TYPE WHERE Code = 'HMAD') AS CatalogTypeId FROM DUAL
                    ) s ON (t.Code = s.Code AND t.CatalogTypeId = s.CatalogTypeId)
                    WHEN MATCHED THEN UPDATE SET t.Name = s.Name
                    WHEN NOT MATCHED THEN INSERT (Code, Name, CatalogTypeId, Priority, Status, IsDeleted, CreatedBy)
                        VALUES (s.Code, s.Name, s.CatalogTypeId, 1, 1, 0, 'system')
                ";
                ExecuteNonQuery(mergeSql);
            }
        }

        return string.Empty;
    }
}
