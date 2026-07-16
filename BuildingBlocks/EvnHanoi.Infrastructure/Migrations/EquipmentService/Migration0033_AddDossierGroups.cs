using DbUp.Engine;
using System;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

/// <summary>
/// DOSSIER_GROUPS (nhóm hồ sơ) + DOSSIER_GROUP_ID trên DOSSIERS.
/// Dữ liệu cũ gán DOSSIER_GROUP_ID = 1 (Hồ sơ trạm).
/// </summary>
public class Migration0033_AddDossierGroups : IScript
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

        ExecuteNonQuery(@"
            CREATE TABLE DOSSIER_GROUPS (
                ID                   NUMBER PRIMARY KEY,
                CODE                 VARCHAR2(50) NOT NULL,
                NAME                 NVARCHAR2(255) NOT NULL,
                INFRA_TYPE_ID        NUMBER NOT NULL,
                IS_EQUIPMENT_DOSSIER NUMBER(1) DEFAULT 0 NOT NULL
            )", 955);

        ExecuteNonQuery(@"
            ALTER TABLE DOSSIER_GROUPS ADD CONSTRAINT FK_DOSSIER_GROUPS_INFRA_TYPE
            FOREIGN KEY (INFRA_TYPE_ID) REFERENCES INFRASTRUCTURE_TYPE(ID)", 2275, 2261, 2264);

        ExecuteNonQuery(@"
            INSERT INTO DOSSIER_GROUPS (ID, CODE, NAME, INFRA_TYPE_ID, IS_EQUIPMENT_DOSSIER)
            SELECT 1, 'Station', N'Hồ sơ trạm', 1, 0 FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM DOSSIER_GROUPS WHERE ID = 1)", 1);

        ExecuteNonQuery(@"
            INSERT INTO DOSSIER_GROUPS (ID, CODE, NAME, INFRA_TYPE_ID, IS_EQUIPMENT_DOSSIER)
            SELECT 2, 'TransmissionLine', N'Hồ sơ đường dây', 2, 0 FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM DOSSIER_GROUPS WHERE ID = 2)", 1);

        ExecuteNonQuery(@"
            INSERT INTO DOSSIER_GROUPS (ID, CODE, NAME, INFRA_TYPE_ID, IS_EQUIPMENT_DOSSIER)
            SELECT 3, 'StationEquipment', N'Hồ sơ thiết bị của trạm', 1, 1 FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM DOSSIER_GROUPS WHERE ID = 3)", 1);

        ExecuteNonQuery(@"
            INSERT INTO DOSSIER_GROUPS (ID, CODE, NAME, INFRA_TYPE_ID, IS_EQUIPMENT_DOSSIER)
            SELECT 4, 'LineEquipment', N'Hồ sơ thiết bị của đường dây', 2, 1 FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM DOSSIER_GROUPS WHERE ID = 4)", 1);

        ExecuteNonQuery(@"
            ALTER TABLE DOSSIERS ADD (DOSSIER_GROUP_ID NUMBER DEFAULT 1 NOT NULL)", 1430, 904);

        ExecuteNonQuery(@"
            UPDATE DOSSIERS SET DOSSIER_GROUP_ID = 1 WHERE DOSSIER_GROUP_ID IS NULL OR DOSSIER_GROUP_ID = 0", 904);

        ExecuteNonQuery(@"
            ALTER TABLE DOSSIERS ADD CONSTRAINT FK_DOSSIERS_GROUP
            FOREIGN KEY (DOSSIER_GROUP_ID) REFERENCES DOSSIER_GROUPS(ID)", 2275, 2261, 2264);

        ExecuteNonQuery("CREATE INDEX IDX_DOSSIERS_GROUP_ID ON DOSSIERS(DOSSIER_GROUP_ID)", 955);

        return string.Empty;
    }
}
