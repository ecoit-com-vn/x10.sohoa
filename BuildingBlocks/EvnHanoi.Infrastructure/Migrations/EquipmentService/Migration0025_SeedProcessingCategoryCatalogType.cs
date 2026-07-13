using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0025_SeedProcessingCategoryCatalogType : IScript
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

        ExecuteNonQuery(@"
            MERGE INTO CATALOG_TYPE t USING DUAL ON (t.Code = 'PROCESSING_CATEGORY')
            WHEN MATCHED THEN UPDATE SET
                t.Name = N'Quy trình xử lý',
                t.HasParent = 0,
                t.Description = N'Danh mục Quy trình xử lý',
                t.IsPrivate = 0,
                t.Status = 1,
                t.IsDeleted = 0
            WHEN NOT MATCHED THEN INSERT (Id, Code, Name, HasParent, Description, IsPrivate, Status, IsDeleted, CreatedBy)
                VALUES (SEQ_CATALOG_TYPE_ID.NEXTVAL, 'PROCESSING_CATEGORY', N'Quy trình xử lý', 0, N'Danh mục Quy trình xử lý', 0, 1, 0, 'system')
        ");

        return string.Empty;
    }
}
