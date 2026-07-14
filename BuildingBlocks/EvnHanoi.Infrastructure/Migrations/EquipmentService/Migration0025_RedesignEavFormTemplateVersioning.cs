using DbUp.Engine;
using System;
using System.Collections.Generic;
using System.Data;

namespace EvnHanoi.Infrastructure.Migrations.EquipmentService;

public class Migration0025_RedesignEavFormTemplateVersioning : IScript
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
                    cmd.Parameters.Clear();
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

            // 1. Create table EavFormTemplateVersions
            ExecuteNonQuery(@"
CREATE TABLE EavFormTemplateVersions (
    Id VARCHAR2(36) NOT NULL PRIMARY KEY,
    FormTemplateId VARCHAR2(36) NOT NULL,
    Name VARCHAR2(255) NULL,
    Category VARCHAR2(100) NULL,
    Description VARCHAR2(1000) NULL,
    DescriptionInfo VARCHAR2(1000) NULL,
    FormSchema CLOB NULL,
    Version NUMBER DEFAULT 1 NOT NULL,
    IsActive NUMBER(1) DEFAULT 1 NOT NULL,
    CreatedAt TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL,
    CreatedBy VARCHAR2(100) NULL,
    Status VARCHAR2(50) DEFAULT 'Tạo mới' NOT NULL,
    IsDeleted NUMBER(1) DEFAULT 0 NOT NULL,
    CONSTRAINT FK_EAV_FORM_VER_TEMPLATE FOREIGN KEY (FormTemplateId) REFERENCES EavFormTemplates(Id)
)", 955); // ORA-00955: name is already used by an existing object

            ExecuteNonQuery("CREATE INDEX IDX_EAV_FORM_VER_TEMP ON EavFormTemplateVersions(FormTemplateId)", 955, 1408);
            ExecuteNonQuery("CREATE INDEX IDX_EAV_FORM_VER_ACTIVE ON EavFormTemplateVersions(FormTemplateId, IsActive, IsDeleted)", 955, 1408);

            // 2. Data Migration: Read existing EavFormTemplates
            var templates = new List<dynamic>();
            cmd.CommandText = "SELECT Id, Name, Code, Category, Description, DescriptionInfo, ExtractionProcess, FormSchema, EquipmentTypeId, GridTypeId, Version, IsActive, CreatedAt, CreatedBy, Status, FormType, IsDeleted FROM EavFormTemplates";
            cmd.Parameters.Clear();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    templates.Add(new {
                        Id = reader.GetString(0),
                        Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        Code = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Category = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        DescriptionInfo = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        ExtractionProcess = reader.IsDBNull(6) ? null : reader.GetString(6),
                        FormSchema = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        EquipmentTypeId = reader.IsDBNull(8) ? null : reader.GetString(8),
                        GridTypeId = reader.IsDBNull(9) ? (int?)null : Convert.ToInt32(reader.GetValue(9)),
                        Version = Convert.ToInt32(reader.GetValue(10)),
                        IsActive = Convert.ToInt32(reader.GetValue(11)),
                        CreatedAt = reader.GetDateTime(12),
                        CreatedBy = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        Status = reader.IsDBNull(14) ? "Tạo mới" : reader.GetString(14),
                        FormType = reader.IsDBNull(15) ? "FORM" : reader.GetString(15),
                        IsDeleted = Convert.ToInt32(reader.GetValue(16))
                    });
                }
            }

            // Group by Code (or Category if Code is empty, but usually they have Code)
            var groups = new Dictionary<string, List<dynamic>>();
            foreach (var t in templates)
            {
                var key = string.IsNullOrWhiteSpace(t.Code) ? t.Id : t.Code;
                if (!groups.ContainsKey(key))
                {
                    groups[key] = new List<dynamic>();
                }
                groups[key].Add(t);
            }

            foreach (var kvp in groups)
            {
                var groupTemplates = kvp.Value;
                // Sort by version desc to find the master
                groupTemplates.Sort((a, b) => b.Version.CompareTo(a.Version));
                
                var master = groupTemplates[0];
                var masterId = master.Id;

                // Update DOCUMENT_TYPES FORM_ID for all matching templates in the group to masterId
                foreach (var t in groupTemplates)
                {
                    using (var updCmd = dbCommandFactory())
                    {
                        updCmd.CommandText = "UPDATE DOCUMENT_TYPES SET FORM_ID = :MasterId WHERE FORM_ID = :OldId";
                        
                        var p1 = updCmd.CreateParameter();
                        p1.ParameterName = "MasterId";
                        p1.Value = masterId;
                        updCmd.Parameters.Add(p1);

                        var p2 = updCmd.CreateParameter();
                        p2.ParameterName = "OldId";
                        p2.Value = t.Id;
                        updCmd.Parameters.Add(p2);

                        updCmd.ExecuteNonQuery();
                    }

                    using (var updCmd = dbCommandFactory())
                    {
                        updCmd.CommandText = "UPDATE DOSSIER_TYPES SET FORM_ID = :MasterId WHERE FORM_ID = :OldId";
                        
                        var p1 = updCmd.CreateParameter();
                        p1.ParameterName = "MasterId";
                        p1.Value = masterId;
                        updCmd.Parameters.Add(p1);

                        var p2 = updCmd.CreateParameter();
                        p2.ParameterName = "OldId";
                        p2.Value = t.Id;
                        updCmd.Parameters.Add(p2);

                        updCmd.ExecuteNonQuery();
                    }
                }

                // Insert all templates of the group into EavFormTemplateVersions mapped to masterId
                foreach (var t in groupTemplates)
                {
                    using (var insCmd = dbCommandFactory())
                    {
                        insCmd.CommandText = @"
INSERT INTO EavFormTemplateVersions (Id, FormTemplateId, Name, Category, Description, DescriptionInfo, FormSchema, Version, IsActive, CreatedAt, CreatedBy, Status, IsDeleted)
VALUES (:Id, :FormTemplateId, :Name, :Category, :Description, :DescriptionInfo, :FormSchema, :Version, :IsActive, :CreatedAt, :CreatedBy, :Status, :IsDeleted)";

                        var pId = insCmd.CreateParameter();
                        pId.ParameterName = "Id";
                        pId.Value = Guid.NewGuid().ToString(); // unique version record ID
                        insCmd.Parameters.Add(pId);

                        var pFTId = insCmd.CreateParameter();
                        pFTId.ParameterName = "FormTemplateId";
                        pFTId.Value = masterId;
                        insCmd.Parameters.Add(pFTId);

                        var pName = insCmd.CreateParameter();
                        pName.ParameterName = "Name";
                        pName.Value = t.Name;
                        insCmd.Parameters.Add(pName);

                        var pCat = insCmd.CreateParameter();
                        pCat.ParameterName = "Category";
                        pCat.Value = t.Category;
                        insCmd.Parameters.Add(pCat);

                        var pDesc = insCmd.CreateParameter();
                        pDesc.ParameterName = "Description";
                        pDesc.Value = t.Description;
                        insCmd.Parameters.Add(pDesc);

                        var pDescInfo = insCmd.CreateParameter();
                        pDescInfo.ParameterName = "DescriptionInfo";
                        pDescInfo.Value = t.DescriptionInfo;
                        insCmd.Parameters.Add(pDescInfo);

                        var pSchema = insCmd.CreateParameter();
                        pSchema.ParameterName = "FormSchema";
                        pSchema.Value = t.FormSchema;
                        pSchema.DbType = DbType.String;
                        insCmd.Parameters.Add(pSchema);

                        var pVer = insCmd.CreateParameter();
                        pVer.ParameterName = "Version";
                        pVer.Value = t.Version;
                        insCmd.Parameters.Add(pVer);

                        var pAct = insCmd.CreateParameter();
                        pAct.ParameterName = "IsActive";
                        pAct.Value = t.IsActive;
                        insCmd.Parameters.Add(pAct);

                        var pCreated = insCmd.CreateParameter();
                        pCreated.ParameterName = "CreatedAt";
                        pCreated.Value = t.CreatedAt;
                        insCmd.Parameters.Add(pCreated);

                        var pCreator = insCmd.CreateParameter();
                        pCreator.ParameterName = "CreatedBy";
                        pCreator.Value = t.CreatedBy;
                        insCmd.Parameters.Add(pCreator);

                        var pStat = insCmd.CreateParameter();
                        pStat.ParameterName = "Status";
                        pStat.Value = t.Status;
                        insCmd.Parameters.Add(pStat);

                        var pDel = insCmd.CreateParameter();
                        pDel.ParameterName = "IsDeleted";
                        pDel.Value = t.IsDeleted;
                        insCmd.Parameters.Add(pDel);

                        insCmd.ExecuteNonQuery();
                    }
                }

                // Delete all other templates of this group from EavFormTemplates except master
                foreach (var t in groupTemplates)
                {
                    if (t.Id != masterId)
                    {
                        using (var delCmd = dbCommandFactory())
                        {
                            delCmd.CommandText = "DELETE FROM EavFormTemplates WHERE Id = :Id";
                            var p = delCmd.CreateParameter();
                            p.ParameterName = "Id";
                            p.Value = t.Id;
                            delCmd.Parameters.Add(p);
                            delCmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            // 3. Drop FormSchema and Version columns from EavFormTemplates
            ExecuteNonQuery("ALTER TABLE EavFormTemplates DROP COLUMN FormSchema", 904); // ORA-00904: column not found (if already run)
            ExecuteNonQuery("ALTER TABLE EavFormTemplates DROP COLUMN Version", 904);
        }

        return string.Empty;
    }
}
