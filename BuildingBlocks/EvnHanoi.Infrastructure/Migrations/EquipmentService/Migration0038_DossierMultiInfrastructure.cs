using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0038_DossierMultiInfrastructure : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        using (var cmd = dbCommandFactory())
        {
            void ExecuteNonQuery(string sql, params int[] ignoreErrorCodes)
            {
                try
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
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

            // 1. Tạo bảng trung gian DOSSIER_INFRASTRUCTURE
            // ORA-00955: name is already used by an existing object
            ExecuteNonQuery(@"CREATE TABLE DOSSIER_INFRASTRUCTURE (
                DossierId        VARCHAR2(36) NOT NULL,
                InfrastructureId VARCHAR2(36) NOT NULL,
                CONSTRAINT pk_dossier_infra PRIMARY KEY (DossierId, InfrastructureId),
                CONSTRAINT fk_di_dossier FOREIGN KEY (DossierId) REFERENCES DOSSIERS(Id) ON DELETE CASCADE,
                CONSTRAINT fk_di_infra FOREIGN KEY (InfrastructureId) REFERENCES INFRASTRUCTURE(ID) ON DELETE CASCADE
            )", 955);

            // 2. Tạo Indexes
            // ORA-00955: name is already used by an existing object
            ExecuteNonQuery("CREATE INDEX idx_di_infra_id ON DOSSIER_INFRASTRUCTURE(InfrastructureId)", 955);
            ExecuteNonQuery("CREATE INDEX idx_di_dossier_id ON DOSSIER_INFRASTRUCTURE(DossierId)", 955);

            // 3. Backfill dữ liệu hiện tại từ DOSSIERS.InfrastructureId sang DOSSIER_INFRASTRUCTURE
            ExecuteNonQuery(@"INSERT INTO DOSSIER_INFRASTRUCTURE (DossierId, InfrastructureId)
                SELECT Id, InfrastructureId FROM DOSSIERS d
                WHERE d.InfrastructureId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM DOSSIER_INFRASTRUCTURE di 
                      WHERE di.DossierId = d.Id AND di.InfrastructureId = d.InfrastructureId
                  )");
        }

        return string.Empty;
    }
}
