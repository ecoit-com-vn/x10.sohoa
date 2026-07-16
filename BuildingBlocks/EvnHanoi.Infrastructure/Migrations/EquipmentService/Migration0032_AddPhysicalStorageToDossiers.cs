using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// Thêm vị trí lưu trữ vật lý (kệ / tầng / hộp) vào hồ sơ — không bắt buộc.
/// Chỉ có ý nghĩa nghiệp vụ khi đã chọn đến hộp (PHYSICAL_BOX).
/// </summary>
public class Migration0032_AddPhysicalStorageToDossiers : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using var cmd = dbCommandFactory();

        void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
        {
            try
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                var ignored = false;
                foreach (var code in ignoreErrorCodes)
                {
                    if (ex.Message.Contains($"ORA-{code:D5}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-0{code}", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains($"ORA-{code}", StringComparison.OrdinalIgnoreCase))
                    {
                        ignored = true;
                        break;
                    }
                }

                if (!ignored)
                    throw new Exception($"Failed executing SQL: {sql}. Error: {ex.Message}", ex);
            }
        }

        ExecuteNonQuery("ALTER TABLE DOSSIERS ADD ShelfId NUMBER NULL", 1430);
        ExecuteNonQuery("ALTER TABLE DOSSIERS ADD FloorId NUMBER NULL", 1430);
        ExecuteNonQuery("ALTER TABLE DOSSIERS ADD BoxId NUMBER NULL", 1430);

        ExecuteNonQuery(
            @"ALTER TABLE DOSSIERS ADD CONSTRAINT fk_dossier_phys_shelf
              FOREIGN KEY (ShelfId) REFERENCES PHYSICAL_SHELF(Id)",
            2275);
        ExecuteNonQuery(
            @"ALTER TABLE DOSSIERS ADD CONSTRAINT fk_dossier_phys_floor
              FOREIGN KEY (FloorId) REFERENCES PHYSICAL_FLOOR(Id)",
            2275);
        ExecuteNonQuery(
            @"ALTER TABLE DOSSIERS ADD CONSTRAINT fk_dossier_phys_box
              FOREIGN KEY (BoxId) REFERENCES PHYSICAL_BOX(Id)",
            2275);

        ExecuteNonQuery("CREATE INDEX IDX_DOSSIERS_BOX_ID ON DOSSIERS(BoxId)", 955);
        ExecuteNonQuery("CREATE INDEX IDX_DOSSIERS_SHELF_ID ON DOSSIERS(ShelfId)", 955);

        return string.Empty;
    }
}
