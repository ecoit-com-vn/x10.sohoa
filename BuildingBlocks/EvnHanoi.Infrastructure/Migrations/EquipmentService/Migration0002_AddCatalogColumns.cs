using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0002_AddCatalogColumns : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            // Helper method to execute DDL and ignore specific Oracle error codes
            void ExecuteDDL(string sql, params int[] ignoreErrorCodes)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    // Check if exception contains any of the Oracle error codes to ignore
                    bool ignored = false;
                    foreach (var code in ignoreErrorCodes)
                    {
                        if (ex.Message.Contains($"ORA-{code:D5}") || ex.Message.Contains($"ORA-0{code}") || ex.Message.Contains($"ORA-{code}"))
                        {
                            ignored = true;
                            break;
                        }
                    }
                    if (!ignored)
                    {
                        throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
                    }
                }
            }

            // 1. Drop the existing unique constraint/key on CATALOG.Code
            // Ignore ORA-02442 (Cannot drop nonexistent unique key) and ORA-02443 (Cannot drop constraint - nonexistent constraint)
            ExecuteDDL("ALTER TABLE CATALOG DROP UNIQUE (Code)", 2442, 2443, 1918);

            // 2. Add Priority and Status columns
            // Ignore ORA-01430 (column being added already exists)
            ExecuteDDL("ALTER TABLE CATALOG ADD Priority NUMBER DEFAULT 1 NOT NULL", 1430);
            ExecuteDDL("ALTER TABLE CATALOG ADD Status NUMBER(1) DEFAULT 1 NOT NULL", 1430);

            // 3. Add a composite unique constraint on (CatalogType, Code)
            // Ignore ORA-02261 (unique or primary key already exists in the table)
            ExecuteDDL("ALTER TABLE CATALOG ADD CONSTRAINT uc_catalog_type_code UNIQUE (CatalogType, Code)", 2261);

            // 4. Create CATALOG_TYPE table
            // Ignore ORA-00955 (name is already used by an existing object)
            ExecuteDDL(@"
                CREATE TABLE CATALOG_TYPE (
                    Code VARCHAR2(50) NOT NULL PRIMARY KEY,
                    Name VARCHAR2(255) NOT NULL,
                    HasParent NUMBER(1) DEFAULT 0 NOT NULL,
                    Description VARCHAR2(1000) NULL,
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    CreatedBy VARCHAR2(50) DEFAULT 'system'
                )", 955);

            // 5. Seed/Merge the 9 system catalog types
            string[] codes = { "PHONG", "MUC_LUC", "LOAI_HO_SO", "KE", "TANG", "HOP", "CHUC_VU", "LINH_VUC", "TINH_TRANG_VAT_LY" };
            string[] names = { "Phông", "Mục lục hồ sơ", "Loại hồ sơ", "Kệ hồ sơ", "Tầng hồ sơ", "Hộp hồ sơ", "Chức vụ", "Lĩnh vực", "Tình trạng vật lý" };
            int[] hasParents = { 1, 1, 0, 0, 0, 0, 1, 1, 0 };
            string[] descriptions = { "Danh mục Phông hồ sơ", "Danh mục Mục lục hồ sơ", "Danh mục Loại hồ sơ", "Danh mục Kệ hồ sơ", "Danh mục Tầng hồ sơ", "Danh mục Hộp hồ sơ", "Danh mục Chức vụ", "Danh mục Lĩnh vực", "Danh mục Tình trạng vật lý" };

            for (int i = 0; i < codes.Length; i++)
            {
                try
                {
                    cmd.CommandText = $"MERGE INTO CATALOG_TYPE t USING DUAL ON (t.Code = '{codes[i]}') " +
                                      $"WHEN MATCHED THEN UPDATE SET t.Name = '{names[i]}', t.HasParent = {hasParents[i]}, t.Description = '{descriptions[i]}' " +
                                      $"WHEN NOT MATCHED THEN INSERT (Code, Name, HasParent, Description) VALUES ('{codes[i]}', '{names[i]}', {hasParents[i]}, '{descriptions[i]}')";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error seeding catalog type '{codes[i]}': {ex.Message}", ex);
                }
            }
        }

        return string.Empty;
    }
}
