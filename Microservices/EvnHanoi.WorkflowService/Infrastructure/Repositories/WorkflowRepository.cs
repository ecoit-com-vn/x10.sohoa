using Dapper;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Enums;
using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.WorkflowService.Infrastructure.Repositories
{
    public class WorkflowRepository : IWorkflowRepository
    {
        private readonly IDbConnection _connection;

        public WorkflowRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<IEnumerable<WorkflowDefinition>> GetAllDefinitionsAsync(string? keyword, bool? isActive)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT wd.{nameof(WorkflowDefinition.Id)}, 
                                wd.{nameof(WorkflowDefinition.Name)}, 
                                wd.{nameof(WorkflowDefinition.Description)}, 
                                wd.{nameof(WorkflowDefinition.Version)}, 
                                wd.{nameof(WorkflowDefinition.ForceActivate)}, 
                                wd.{nameof(WorkflowDefinition.CreatedAt)}, 
                                wd.{nameof(WorkflowDefinition.UpdatedAt)}, 
                                wd.{nameof(WorkflowDefinition.CreatedBy)}, 
                                u1.UserName AS {nameof(WorkflowDefinition.CreatedByUsername)}, 
                                u1.FullName AS {nameof(WorkflowDefinition.CreatedByFullName)}, 
                                wd.{nameof(WorkflowDefinition.UpdatedBy)}, 
                                u2.UserName AS {nameof(WorkflowDefinition.UpdatedByUsername)}, 
                                u2.FullName AS {nameof(WorkflowDefinition.UpdatedByFullName)}, 
                                wd.{nameof(WorkflowDefinition.IsActive)}, 
                                wd.WORKFLOW_TYPE_ID AS {nameof(WorkflowDefinition.WorkflowTypeId)}
                        FROM WORKFLOWDEFINITIONS wd
                        LEFT JOIN APP_USER u1 ON wd.CreatedBy = u1.Id
                        LEFT JOIN APP_USER u2 ON wd.UpdatedBy = u2.Id
                        WHERE 1=1";
            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                sql += $@" AND (wd.{nameof(WorkflowDefinition.Name)} LIKE :Keyword OR wd.{nameof(WorkflowDefinition.Description)} LIKE :Keyword)";
                parameters.Add("Keyword", $"%{keyword}%");
            }
            if (isActive.HasValue)
            {
                sql += $@" AND wd.{nameof(WorkflowDefinition.IsActive)} = :IsActive";
                parameters.Add("IsActive", isActive.Value ? 1 : 0);
            }
            sql += $@" ORDER BY wd.{nameof(WorkflowDefinition.CreatedAt)} DESC";

            var definitions = await _connection.QueryAsync<WorkflowDefinition>(sql, parameters);
            return definitions.ToList();
        }

        public async Task<IEnumerable<WorkflowDefinition>> GetDefinitionsByWorkflowTypeIdAsync(int workflowTypeId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT wd.{nameof(WorkflowDefinition.Id)}, 
                                wd.{nameof(WorkflowDefinition.Name)}, 
                                wd.{nameof(WorkflowDefinition.Description)}, 
                                wd.{nameof(WorkflowDefinition.Version)}, 
                                wd.{nameof(WorkflowDefinition.ForceActivate)}, 
                                wd.{nameof(WorkflowDefinition.CreatedAt)}, 
                                wd.{nameof(WorkflowDefinition.UpdatedAt)}, 
                                wd.{nameof(WorkflowDefinition.CreatedBy)}, 
                                u1.UserName AS {nameof(WorkflowDefinition.CreatedByUsername)}, 
                                u1.FullName AS {nameof(WorkflowDefinition.CreatedByFullName)}, 
                                wd.{nameof(WorkflowDefinition.UpdatedBy)}, 
                                u2.UserName AS {nameof(WorkflowDefinition.UpdatedByUsername)}, 
                                u2.FullName AS {nameof(WorkflowDefinition.UpdatedByFullName)}, 
                                wd.{nameof(WorkflowDefinition.IsActive)}, 
                                wd.WORKFLOW_TYPE_ID AS {nameof(WorkflowDefinition.WorkflowTypeId)}
                        FROM WORKFLOWDEFINITIONS wd
                        LEFT JOIN APP_USER u1 ON wd.CreatedBy = u1.Id
                        LEFT JOIN APP_USER u2 ON wd.UpdatedBy = u2.Id
                        WHERE wd.WORKFLOW_TYPE_ID = :WorkflowTypeId
                        ORDER BY wd.{nameof(WorkflowDefinition.CreatedAt)} DESC";
            
            var definitions = await _connection.QueryAsync<WorkflowDefinition>(sql, new { WorkflowTypeId = workflowTypeId });
            return definitions.ToList();
        }

        public async Task<bool> ExistsDefinitionByWorkflowTypeIdAsync(int workflowTypeId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            const string sql = @"SELECT 1 FROM WORKFLOWDEFINITIONS WHERE WORKFLOW_TYPE_ID = :WorkflowTypeId AND ROWNUM = 1";
            var found = await _connection.QueryFirstOrDefaultAsync<int?>(sql, new { WorkflowTypeId = workflowTypeId });
            return found.HasValue;
        }

        /// <summary>
        /// Lấy definition đang active mới nhất theo WorkflowTypeId — chỉ dùng khi khởi tạo instance mới (submit).
        /// Instance đang Running luôn giữ WorkflowDefinitionId gốc, không bị chuyển sang bản mới.
        /// </summary>
        public async Task<WorkflowDefinition?> GetActiveDefinitionByWorkflowTypeIdAsync(int workflowTypeId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT wd.{nameof(WorkflowDefinition.Id)},
                                wd.{nameof(WorkflowDefinition.Name)},
                                wd.{nameof(WorkflowDefinition.Description)},
                                wd.{nameof(WorkflowDefinition.Version)},
                                wd.WORKFLOW_TYPE_ID AS {nameof(WorkflowDefinition.WorkflowTypeId)},
                                wd.{nameof(WorkflowDefinition.ForceActivate)},
                                wd.{nameof(WorkflowDefinition.CreatedAt)},
                                wd.{nameof(WorkflowDefinition.UpdatedAt)},
                                wd.{nameof(WorkflowDefinition.CreatedBy)},
                                wd.{nameof(WorkflowDefinition.UpdatedBy)},
                                wd.{nameof(WorkflowDefinition.IsActive)},
                                wd.{nameof(WorkflowDefinition.BpmnXml)}
                        FROM WORKFLOWDEFINITIONS wd
                        WHERE wd.WORKFLOW_TYPE_ID = :WorkflowTypeId
                          AND wd.{nameof(WorkflowDefinition.IsActive)} = 1
                        ORDER BY wd.{nameof(WorkflowDefinition.CreatedAt)} DESC
                        FETCH FIRST 1 ROWS ONLY";

            var def = await _connection.QueryFirstOrDefaultAsync<WorkflowDefinition>(sql, new { WorkflowTypeId = workflowTypeId });
            if (def == null) return null;

            await AttachStepsToDefinitionAsync(def);
            return def;
        }

        public async Task<bool> ExistsRunningInstanceAsync(string entityId, int workflowTypeId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT 1 AS Found
                        FROM WORKFLOWINSTANCES
                        WHERE {nameof(WorkflowInstance.TargetEntityId)} = :EntityId
                          AND WORKFLOW_TYPE_ID = :WorkflowTypeId
                          AND {nameof(WorkflowInstance.Status)} = 'Running'
                        FETCH FIRST 1 ROWS ONLY";
            var found = await _connection.QueryFirstOrDefaultAsync<int?>(sql, new { EntityId = entityId, WorkflowTypeId = workflowTypeId });
            return found.HasValue;
        }

        public async Task<(IEnumerable<WorkflowDefinition> Items, int TotalCount)> GetPagedDefinitionsAsync(int page, int pageSize, string? keyword = null, bool? isActive = null)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            
            var filterSql = " WHERE w.RN_LATEST = 1";
            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                filterSql += $@" AND (w.{nameof(WorkflowDefinition.Name)} LIKE :Keyword OR w.{nameof(WorkflowDefinition.Description)} LIKE :Keyword)";
                parameters.Add("Keyword", $"%{keyword}%");
            }
            if (isActive.HasValue)
            {
                filterSql += $@" AND w.{nameof(WorkflowDefinition.IsActive)} = :IsActive";
                parameters.Add("IsActive", isActive.Value ? 1 : 0);
            }
            
            var countSql = $@"SELECT COUNT(*) FROM (
                SELECT w.*, 
                       ROW_NUMBER() OVER (PARTITION BY w.WORKFLOW_TYPE_ID ORDER BY w.{nameof(WorkflowDefinition.IsActive)} DESC, w.{nameof(WorkflowDefinition.CreatedAt)} DESC) AS RN_LATEST
                FROM WORKFLOWDEFINITIONS w
            ) w {filterSql}";
            
            var offset = (page - 1) * pageSize;
            var pagedSql = $@"
                SELECT * FROM (
                    SELECT w.{nameof(WorkflowDefinition.Id)}, 
                           w.{nameof(WorkflowDefinition.Name)}, 
                           w.{nameof(WorkflowDefinition.Description)}, 
                           w.{nameof(WorkflowDefinition.Version)}, 
                           w.{nameof(WorkflowDefinition.ForceActivate)}, 
                           w.{nameof(WorkflowDefinition.CreatedAt)}, 
                           w.{nameof(WorkflowDefinition.UpdatedAt)}, 
                           w.{nameof(WorkflowDefinition.CreatedBy)}, 
                           u1.UserName AS {nameof(WorkflowDefinition.CreatedByUsername)}, 
                           u1.FullName AS {nameof(WorkflowDefinition.CreatedByFullName)}, 
                           w.{nameof(WorkflowDefinition.UpdatedBy)}, 
                           u2.UserName AS {nameof(WorkflowDefinition.UpdatedByUsername)}, 
                           u2.FullName AS {nameof(WorkflowDefinition.UpdatedByFullName)}, 
                           w.{nameof(WorkflowDefinition.IsActive)}, 
                           w.WORKFLOW_TYPE_ID AS {nameof(WorkflowDefinition.WorkflowTypeId)},
                           ROW_NUMBER() OVER (ORDER BY w.{nameof(WorkflowDefinition.CreatedAt)} DESC) AS RN
                    FROM (
                        SELECT w.*,
                               ROW_NUMBER() OVER (PARTITION BY w.WORKFLOW_TYPE_ID ORDER BY w.{nameof(WorkflowDefinition.IsActive)} DESC, w.{nameof(WorkflowDefinition.CreatedAt)} DESC) AS RN_LATEST
                        FROM WORKFLOWDEFINITIONS w
                    ) w
                    LEFT JOIN APP_USER u1 ON w.CreatedBy = u1.Id
                    LEFT JOIN APP_USER u2 ON w.UpdatedBy = u2.Id
                    {filterSql}
                ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";
                
            parameters.Add("Offset", offset);
            parameters.Add("OffsetPlusSize", offset + pageSize);
            
            var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
            var items = await _connection.QueryAsync<WorkflowDefinition>(pagedSql, parameters);
            var resultList = items.ToList();
            foreach (var def in resultList)
                def.Steps = new List<WorkflowStep>();

            return (resultList, totalCount);
        }

        public async Task<WorkflowDefinition?> GetDefinitionByIdAsync(Guid id, bool includeBpmnXml = true)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            
            var bpmnSelect = includeBpmnXml ? $", wd.{nameof(WorkflowDefinition.BpmnXml)}" : "";
            
            var sqlDef = $@"SELECT wd.{nameof(WorkflowDefinition.Id)},
                                   wd.{nameof(WorkflowDefinition.Name)}, 
                                   wd.{nameof(WorkflowDefinition.Description)}, 
                                   wd.{nameof(WorkflowDefinition.Version)}, 
                                   wd.{nameof(WorkflowDefinition.ForceActivate)}, 
                                   wd.{nameof(WorkflowDefinition.CreatedAt)}, 
                                   wd.{nameof(WorkflowDefinition.UpdatedAt)}, 
                                   wd.{nameof(WorkflowDefinition.CreatedBy)}, 
                                   u1.UserName AS {nameof(WorkflowDefinition.CreatedByUsername)}, 
                                   u1.FullName AS {nameof(WorkflowDefinition.CreatedByFullName)}, 
                                   wd.{nameof(WorkflowDefinition.UpdatedBy)}, 
                                   u2.UserName AS {nameof(WorkflowDefinition.UpdatedByUsername)}, 
                                   u2.FullName AS {nameof(WorkflowDefinition.UpdatedByFullName)}, 
                                   wd.{nameof(WorkflowDefinition.IsActive)},
                                   wd.WORKFLOW_TYPE_ID AS {nameof(WorkflowDefinition.WorkflowTypeId)}{bpmnSelect} 
                           FROM WORKFLOWDEFINITIONS wd
                           LEFT JOIN APP_USER u1 ON wd.CreatedBy = u1.Id
                           LEFT JOIN APP_USER u2 ON wd.UpdatedBy = u2.Id
                           WHERE wd.{nameof(WorkflowDefinition.Id)} = :Id";
            var def = await _connection.QuerySingleOrDefaultAsync<WorkflowDefinition>(sqlDef, new { Id = id.ToString() });
            if (def == null) return null;

            var sqlSteps = $@"SELECT {nameof(WorkflowStep.Id)},
                                     {nameof(WorkflowStep.WorkflowDefinitionId)},
                                     {nameof(WorkflowStep.StepName)},
                                     ""{nameof(WorkflowStep.Order)}"",
                                     {nameof(WorkflowStep.RequiredRole)},
                                     {nameof(WorkflowStep.ActionType)},
                                     {nameof(WorkflowStep.AllowEdit)},
                                     {nameof(WorkflowStep.RequireSignature)},
                                     SYSTEM_PERMISSION_GROUP_IDS AS {nameof(WorkflowStep.SystemPermissionGroupIds)},
                                     UNIT_PERMISSION_GROUP_IDS   AS {nameof(WorkflowStep.UnitPermissionGroupIds)},
                                     REQUIRE_SAME_UNIT           AS {nameof(WorkflowStep.RequireSameUnit)},
                                     ASSIGNEE_ID                 AS {nameof(WorkflowStep.AssigneeId)}
                              FROM WORKFLOWSTEPS
                              WHERE {nameof(WorkflowStep.WorkflowDefinitionId)} = :Id
                              ORDER BY ""{nameof(WorkflowStep.Order)}""";
            var steps = await _connection.QueryAsync<WorkflowStep>(sqlSteps, new { Id = id.ToString() });
            def.Steps = steps.ToList();
            foreach (var step in def.Steps)
            {
                step.WorkflowDefinition = def;
            }
            return def;
        }

        public async Task<WorkflowStep?> GetStepByIdAsync(Guid id)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT {nameof(WorkflowStep.Id)},
                                {nameof(WorkflowStep.WorkflowDefinitionId)},
                                {nameof(WorkflowStep.StepName)},
                                ""{nameof(WorkflowStep.Order)}"",
                                {nameof(WorkflowStep.RequiredRole)},
                                {nameof(WorkflowStep.ActionType)},
                                {nameof(WorkflowStep.AllowEdit)},
                                {nameof(WorkflowStep.RequireSignature)},
                                SYSTEM_PERMISSION_GROUP_IDS AS {nameof(WorkflowStep.SystemPermissionGroupIds)},
                                UNIT_PERMISSION_GROUP_IDS   AS {nameof(WorkflowStep.UnitPermissionGroupIds)},
                                REQUIRE_SAME_UNIT           AS {nameof(WorkflowStep.RequireSameUnit)},
                                ASSIGNEE_ID                 AS {nameof(WorkflowStep.AssigneeId)}
                        FROM WORKFLOWSTEPS WHERE {nameof(WorkflowStep.Id)} = :Id";
            return await _connection.QuerySingleOrDefaultAsync<WorkflowStep>(sql, new { Id = id.ToString() });
        }

        public async Task<bool> CreateDefinitionAsync(WorkflowDefinition definition)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                if (definition.Id == Guid.Empty)
                {
                    definition.Id = Guid.CreateVersion7();
                }
                var now = DateTime.UtcNow;
                definition.CreatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
                definition.UpdatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);

                if (definition.ForceActivate)
                {
                    var sqlDeactivate = $@"UPDATE WORKFLOWDEFINITIONS 
                                           SET {nameof(WorkflowDefinition.IsActive)} = 0 
                                           WHERE WORKFLOW_TYPE_ID = :WorkflowTypeId AND {nameof(WorkflowDefinition.IsActive)} = 1 AND Id != :Id";
                    await _connection.ExecuteAsync(sqlDeactivate, new { WorkflowTypeId = definition.WorkflowTypeId, Id = definition.Id.ToString() }, transaction);
                }

                var sqlInsertDef = $@"INSERT INTO WORKFLOWDEFINITIONS (
                                        {nameof(WorkflowDefinition.Id)}, 
                                        {nameof(WorkflowDefinition.Name)}, 
                                        {nameof(WorkflowDefinition.Description)}, 
                                        {nameof(WorkflowDefinition.Version)}, 
                                        {nameof(WorkflowDefinition.ForceActivate)}, 
                                        {nameof(WorkflowDefinition.CreatedAt)}, 
                                        {nameof(WorkflowDefinition.UpdatedAt)}, 
                                        {nameof(WorkflowDefinition.CreatedBy)}, 
                                        {nameof(WorkflowDefinition.UpdatedBy)}, 
                                        {nameof(WorkflowDefinition.IsActive)}, 
                                        {nameof(WorkflowDefinition.BpmnXml)},
                                        WORKFLOW_TYPE_ID
                                     )
                                     VALUES (:Id, :Name, :Description, :Version, :ForceActivate, :CreatedAt, :UpdatedAt, :CreatedBy, :UpdatedBy, :IsActive, :BpmnXml, :WorkflowTypeId)";

                var parameters = new DynamicParameters();
                parameters.Add("Id", definition.Id.ToString());
                parameters.Add("Name", string.IsNullOrEmpty(definition.Name) ? null : definition.Name);
                parameters.Add("Description", string.IsNullOrEmpty(definition.Description) ? null : definition.Description);
                parameters.Add("Version", string.IsNullOrEmpty(definition.Version) ? null : definition.Version);
                parameters.Add("ForceActivate", definition.ForceActivate ? 1 : 0);
                parameters.Add("CreatedAt", definition.CreatedAt);
                parameters.Add("UpdatedAt", definition.UpdatedAt);
                parameters.Add("CreatedBy", string.IsNullOrEmpty(definition.CreatedBy) ? "System" : definition.CreatedBy);
                parameters.Add("UpdatedBy", string.IsNullOrEmpty(definition.UpdatedBy) ? "System" : definition.UpdatedBy);
                parameters.Add("IsActive", definition.IsActive ? 1 : 0);
                parameters.Add("BpmnXml", OracleClob.Param(definition.BpmnXml));
                parameters.Add("WorkflowTypeId", definition.WorkflowTypeId);

                await _connection.ExecuteAsync(sqlInsertDef, parameters, transaction);

                if (definition.Steps != null && definition.Steps.Count > 0)
                {
                    var sqlInsertStep = $@"INSERT INTO WORKFLOWSTEPS (
                                            {nameof(WorkflowStep.Id)}, 
                                            {nameof(WorkflowStep.WorkflowDefinitionId)}, 
                                            {nameof(WorkflowStep.StepName)}, 
                                            ""{nameof(WorkflowStep.Order)}"", 
                                            {nameof(WorkflowStep.RequiredRole)}, 
                                            {nameof(WorkflowStep.ActionType)},
                                            {nameof(WorkflowStep.AllowEdit)},
                                            {nameof(WorkflowStep.RequireSignature)},
                                            SYSTEM_PERMISSION_GROUP_IDS,
                                            UNIT_PERMISSION_GROUP_IDS,
                                            REQUIRE_SAME_UNIT,
                                            ASSIGNEE_ID
                                         )
                                         VALUES (:Id, :WorkflowDefinitionId, :StepName, :OrderVal, :RequiredRole, :ActionType, :AllowEdit, :RequireSignature, :SystemPermissionGroupIds, :UnitPermissionGroupIds, :RequireSameUnit, :AssigneeId)";
                    foreach (var step in definition.Steps)
                    {
                        if (step.Id == Guid.Empty)
                        {
                            step.Id = Guid.CreateVersion7();
                        }
                        step.WorkflowDefinitionId = definition.Id;

                        var stepParams = new DynamicParameters();
                        stepParams.Add("Id", step.Id.ToString());
                        stepParams.Add("WorkflowDefinitionId", step.WorkflowDefinitionId.ToString());
                        stepParams.Add("StepName", string.IsNullOrEmpty(step.StepName) ? null : step.StepName);
                        stepParams.Add("OrderVal", step.Order);
                        stepParams.Add("RequiredRole", string.IsNullOrEmpty(step.RequiredRole) ? null : step.RequiredRole);
                        stepParams.Add("ActionType", string.IsNullOrEmpty(step.ActionType) ? null : step.ActionType);
                        stepParams.Add("AllowEdit", step.AllowEdit ? 1 : 0);
                        stepParams.Add("RequireSignature", step.RequireSignature ? 1 : 0);
                        stepParams.Add("SystemPermissionGroupIds", string.IsNullOrEmpty(step.SystemPermissionGroupIds) ? null : step.SystemPermissionGroupIds);
                        stepParams.Add("UnitPermissionGroupIds", string.IsNullOrEmpty(step.UnitPermissionGroupIds) ? null : step.UnitPermissionGroupIds);
                        stepParams.Add("RequireSameUnit", step.RequireSameUnit ? 1 : 0);
                        stepParams.Add("AssigneeId", string.IsNullOrEmpty(step.AssigneeId) ? null : step.AssigneeId);

                        await _connection.ExecuteAsync(sqlInsertStep, stepParams, transaction);
                    }
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateDefinitionAsync(Guid id, WorkflowDefinition definition)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                definition.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                if (definition.ForceActivate && definition.IsActive)
                {
                    var sqlDeactivate = $@"UPDATE WORKFLOWDEFINITIONS 
                                           SET {nameof(WorkflowDefinition.IsActive)} = 0 
                                           WHERE WORKFLOW_TYPE_ID = :WorkflowTypeId AND {nameof(WorkflowDefinition.IsActive)} = 1 AND {nameof(WorkflowDefinition.Id)} != :Id";
                    await _connection.ExecuteAsync(sqlDeactivate, new { WorkflowTypeId = definition.WorkflowTypeId, Id = id.ToString() }, transaction);
                }

                var sqlUpdateDef = $@"UPDATE WORKFLOWDEFINITIONS
                                     SET {nameof(WorkflowDefinition.Name)} = :Name, 
                                         {nameof(WorkflowDefinition.Description)} = :Description, 
                                         {nameof(WorkflowDefinition.Version)} = :Version,
                                         {nameof(WorkflowDefinition.ForceActivate)} = :ForceActivate, 
                                         {nameof(WorkflowDefinition.IsActive)} = :IsActive, 
                                         {nameof(WorkflowDefinition.BpmnXml)} = :BpmnXml, 
                                         {nameof(WorkflowDefinition.UpdatedAt)} = :UpdatedAt,
                                         {nameof(WorkflowDefinition.UpdatedBy)} = :UpdatedBy,
                                         WORKFLOW_TYPE_ID = :WorkflowTypeId
                                     WHERE {nameof(WorkflowDefinition.Id)} = :Id";
                var defParams = new DynamicParameters();
                defParams.Add("Name", string.IsNullOrEmpty(definition.Name) ? null : definition.Name);
                defParams.Add("Description", string.IsNullOrEmpty(definition.Description) ? null : definition.Description);
                defParams.Add("Version", string.IsNullOrEmpty(definition.Version) ? null : definition.Version);
                defParams.Add("ForceActivate", definition.ForceActivate ? 1 : 0);
                defParams.Add("IsActive", definition.IsActive ? 1 : 0);
                defParams.Add("BpmnXml", OracleClob.Param(definition.BpmnXml));
                defParams.Add("UpdatedAt", definition.UpdatedAt);
                defParams.Add("UpdatedBy", string.IsNullOrEmpty(definition.UpdatedBy) ? "System" : definition.UpdatedBy);
                defParams.Add("WorkflowTypeId", definition.WorkflowTypeId);
                defParams.Add("Id", id.ToString());

                await _connection.ExecuteAsync(sqlUpdateDef, defParams, transaction);

                // Fetch existing steps
                var sqlGetExistingSteps = $@"SELECT {nameof(WorkflowStep.Id)} 
                                             FROM WORKFLOWSTEPS 
                                             WHERE {nameof(WorkflowStep.WorkflowDefinitionId)} = :Id";
                var existingStepIds = (await _connection.QueryAsync<Guid>(sqlGetExistingSteps, new { Id = id.ToString() }, transaction)).ToHashSet();

                var incomingSteps = definition.Steps ?? new List<WorkflowStep>();
                var incomingStepIds = incomingSteps.Where(s => s.Id != Guid.Empty).Select(s => s.Id).ToHashSet();

                // 1. Delete steps that are not in incoming list
                var stepIdsToDelete = existingStepIds.Where(eid => !incomingStepIds.Contains(eid)).ToList();
                if (stepIdsToDelete.Any())
                {
                    var sqlDeleteStep = $@"DELETE FROM WORKFLOWSTEPS WHERE {nameof(WorkflowStep.Id)} = :Id";
                    foreach (var deleteId in stepIdsToDelete)
                    {
                        await _connection.ExecuteAsync(sqlDeleteStep, new { Id = deleteId.ToString() }, transaction);
                    }
                }

                // 2. Insert or Update incoming steps
                var sqlInsertStep = $@"INSERT INTO WORKFLOWSTEPS (
                                        {nameof(WorkflowStep.Id)}, 
                                        {nameof(WorkflowStep.WorkflowDefinitionId)}, 
                                        {nameof(WorkflowStep.StepName)}, 
                                        ""{nameof(WorkflowStep.Order)}"", 
                                        {nameof(WorkflowStep.RequiredRole)}, 
                                        {nameof(WorkflowStep.ActionType)},
                                        {nameof(WorkflowStep.AllowEdit)},
                                        {nameof(WorkflowStep.RequireSignature)},
                                        SYSTEM_PERMISSION_GROUP_IDS,
                                        UNIT_PERMISSION_GROUP_IDS,
                                        REQUIRE_SAME_UNIT,
                                        ASSIGNEE_ID
                                     )
                                     VALUES (:Id, :WorkflowDefinitionId, :StepName, :OrderVal, :RequiredRole, :ActionType, :AllowEdit, :RequireSignature, :SystemPermissionGroupIds, :UnitPermissionGroupIds, :RequireSameUnit, :AssigneeId)";

                var sqlUpdateStep = $@"UPDATE WORKFLOWSTEPS 
                                      SET {nameof(WorkflowStep.StepName)} = :StepName, 
                                          ""{nameof(WorkflowStep.Order)}"" = :OrderVal, 
                                          {nameof(WorkflowStep.RequiredRole)} = :RequiredRole, 
                                          {nameof(WorkflowStep.ActionType)} = :ActionType,
                                          {nameof(WorkflowStep.AllowEdit)} = :AllowEdit,
                                          {nameof(WorkflowStep.RequireSignature)} = :RequireSignature,
                                          SYSTEM_PERMISSION_GROUP_IDS = :SystemPermissionGroupIds,
                                          UNIT_PERMISSION_GROUP_IDS = :UnitPermissionGroupIds,
                                          REQUIRE_SAME_UNIT = :RequireSameUnit,
                                          ASSIGNEE_ID = :AssigneeId
                                      WHERE {nameof(WorkflowStep.Id)} = :Id";

                foreach (var step in incomingSteps)
                {
                    var stepParams = new DynamicParameters();
                    stepParams.Add("StepName", string.IsNullOrEmpty(step.StepName) ? null : step.StepName);
                    stepParams.Add("OrderVal", step.Order);
                    stepParams.Add("RequiredRole", string.IsNullOrEmpty(step.RequiredRole) ? null : step.RequiredRole);
                    stepParams.Add("ActionType", string.IsNullOrEmpty(step.ActionType) ? null : step.ActionType);
                    stepParams.Add("AllowEdit", step.AllowEdit ? 1 : 0);
                    stepParams.Add("RequireSignature", step.RequireSignature ? 1 : 0);
                    stepParams.Add("SystemPermissionGroupIds", string.IsNullOrEmpty(step.SystemPermissionGroupIds) ? null : step.SystemPermissionGroupIds);
                    stepParams.Add("UnitPermissionGroupIds", string.IsNullOrEmpty(step.UnitPermissionGroupIds) ? null : step.UnitPermissionGroupIds);
                    stepParams.Add("RequireSameUnit", step.RequireSameUnit ? 1 : 0);
                    stepParams.Add("AssigneeId", string.IsNullOrEmpty(step.AssigneeId) ? null : step.AssigneeId);

                    if (step.Id != Guid.Empty && existingStepIds.Contains(step.Id))
                    {
                        // Cập nhật step đã tồn tại
                        stepParams.Add("Id", step.Id.ToString());
                        await _connection.ExecuteAsync(sqlUpdateStep, stepParams, transaction);
                    }
                    else
                    {
                        // Thêm step mới
                        if (step.Id == Guid.Empty)
                        {
                            step.Id = Guid.CreateVersion7();
                        }
                        stepParams.Add("Id", step.Id.ToString());
                        stepParams.Add("WorkflowDefinitionId", id.ToString());
                        await _connection.ExecuteAsync(sqlInsertStep, stepParams, transaction);
                    }
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteDefinitionAsync(Guid id)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                var sqlDeleteSteps = $@"DELETE FROM WORKFLOWSTEPS WHERE {nameof(WorkflowStep.WorkflowDefinitionId)} = :Id";
                await _connection.ExecuteAsync(sqlDeleteSteps, new { Id = id.ToString() }, transaction);

                var sqlDeleteDef = $@"DELETE FROM WORKFLOWDEFINITIONS WHERE {nameof(WorkflowDefinition.Id)} = :Id";
                var affected = await _connection.ExecuteAsync(sqlDeleteDef, new { Id = id.ToString() }, transaction);

                transaction.Commit();
                return affected > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<bool?> ToggleDefinitionStatusAsync(Guid id)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sqlSelect = $@"SELECT {nameof(WorkflowDefinition.IsActive)} FROM WORKFLOWDEFINITIONS WHERE {nameof(WorkflowDefinition.Id)} = :Id";
            var currentStatus = await _connection.QuerySingleOrDefaultAsync<int?>(sqlSelect, new { Id = id.ToString() });
            if (!currentStatus.HasValue) return null;

            var newStatus = currentStatus.Value == 1 ? 0 : 1;
            var sqlUpdate = $@"UPDATE WORKFLOWDEFINITIONS SET {nameof(WorkflowDefinition.IsActive)} = :IsActive, {nameof(WorkflowDefinition.UpdatedAt)} = :UpdatedAt WHERE {nameof(WorkflowDefinition.Id)} = :Id";
            await _connection.ExecuteAsync(sqlUpdate, new { IsActive = newStatus, UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), Id = id.ToString() });
            return newStatus == 1;
        }

        public async Task<WorkflowInstance?> GetInstanceByEntityAsync(string entityId, int workflowTypeId, bool includeBpmnXml = true)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT {nameof(WorkflowInstance.Id)}, 
                                {nameof(WorkflowInstance.WorkflowDefinitionId)}, 
                                {nameof(WorkflowInstance.TargetEntityId)}, 
                                WORKFLOW_TYPE_ID AS {nameof(WorkflowInstance.WorkflowTypeId)}, 
                                {nameof(WorkflowInstance.Status)}, 
                                {nameof(WorkflowInstance.CurrentStepOrder)}, 
                                {nameof(WorkflowInstance.CurrentNodeId)}, 
                                {nameof(WorkflowInstance.CurrentNodeName)}, 
                                {nameof(WorkflowInstance.CreatedAt)}, 
                                {nameof(WorkflowInstance.UpdatedAt)}
                        FROM WORKFLOWINSTANCES
                        WHERE {nameof(WorkflowInstance.TargetEntityId)} = :EntityId AND WORKFLOW_TYPE_ID = :WorkflowTypeId
                        ORDER BY {nameof(WorkflowInstance.CreatedAt)} DESC";
            var instance = await _connection.QueryFirstOrDefaultAsync<WorkflowInstance>(sql, new { EntityId = entityId, WorkflowTypeId = workflowTypeId });
            if (instance == null) return null;

            instance.WorkflowDefinition = await GetDefinitionByIdAsync(instance.WorkflowDefinitionId, includeBpmnXml);

            var sqlTasks = $@"SELECT {nameof(WorkflowTask.Id)}, 
                                     {nameof(WorkflowTask.WorkflowInstanceId)}, 
                                     {nameof(WorkflowTask.StepId)}, 
                                     {nameof(WorkflowTask.StepName)}, 
                                     {nameof(WorkflowTask.AssignedRole)}, 
                                     {nameof(WorkflowTask.AssigneeUserId)}, 
                                     {nameof(WorkflowTask.Status)}, 
                                     {nameof(WorkflowTask.CreatedAt)}, 
                                     {nameof(WorkflowTask.CompletedAt)}
                             FROM WORKFLOWTASKS
                             WHERE {nameof(WorkflowTask.WorkflowInstanceId)} = :InstanceId";
            var tasks = await _connection.QueryAsync<WorkflowTask>(sqlTasks, new { InstanceId = instance.Id.ToString() });
            instance.Tasks = tasks.ToList();
            foreach (var task in instance.Tasks)
            {
                task.WorkflowInstance = instance;
                if (instance.WorkflowDefinition != null)
                {
                    task.Step = instance.WorkflowDefinition.Steps.FirstOrDefault(s => s.Id == task.StepId);
                }
            }
            return instance;
        }

        public async Task<WorkflowInstance?> GetInstanceByIdAsync(Guid instanceId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT {nameof(WorkflowInstance.Id)}, 
                                {nameof(WorkflowInstance.WorkflowDefinitionId)}, 
                                {nameof(WorkflowInstance.TargetEntityId)}, 
                                WORKFLOW_TYPE_ID AS {nameof(WorkflowInstance.WorkflowTypeId)}, 
                                {nameof(WorkflowInstance.Status)}, 
                                {nameof(WorkflowInstance.CurrentStepOrder)}, 
                                {nameof(WorkflowInstance.CurrentNodeId)}, 
                                {nameof(WorkflowInstance.CurrentNodeName)}, 
                                {nameof(WorkflowInstance.CreatedAt)}, 
                                {nameof(WorkflowInstance.UpdatedAt)}
                        FROM WORKFLOWINSTANCES
                        WHERE {nameof(WorkflowInstance.Id)} = :Id";
            var instance = await _connection.QuerySingleOrDefaultAsync<WorkflowInstance>(sql, new { Id = instanceId.ToString() });
            if (instance == null) return null;

            instance.WorkflowDefinition = await GetDefinitionByIdAsync(instance.WorkflowDefinitionId);

            var sqlTasks = $@"SELECT {nameof(WorkflowTask.Id)}, 
                                     {nameof(WorkflowTask.WorkflowInstanceId)}, 
                                     {nameof(WorkflowTask.StepId)}, 
                                     {nameof(WorkflowTask.StepName)}, 
                                     {nameof(WorkflowTask.AssignedRole)}, 
                                     {nameof(WorkflowTask.AssigneeUserId)}, 
                                     {nameof(WorkflowTask.Status)}, 
                                     {nameof(WorkflowTask.CreatedAt)}, 
                                     {nameof(WorkflowTask.CompletedAt)}
                             FROM WORKFLOWTASKS
                             WHERE {nameof(WorkflowTask.WorkflowInstanceId)} = :InstanceId";
            var tasks = await _connection.QueryAsync<WorkflowTask>(sqlTasks, new { InstanceId = instance.Id.ToString() });
            instance.Tasks = tasks.ToList();
            foreach (var task in instance.Tasks)
            {
                task.WorkflowInstance = instance;
                if (instance.WorkflowDefinition != null)
                {
                    task.Step = instance.WorkflowDefinition.Steps.FirstOrDefault(s => s.Id == task.StepId);
                }
            }
            return instance;
        }

        public async Task<bool> CreateInstanceAsync(WorkflowInstance instance)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var now = DateTime.UtcNow;
            instance.CreatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
            instance.UpdatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
            var sql = $@"INSERT INTO WORKFLOWINSTANCES (
                            {nameof(WorkflowInstance.Id)}, 
                            {nameof(WorkflowInstance.WorkflowDefinitionId)}, 
                            {nameof(WorkflowInstance.TargetEntityId)}, 
                            WORKFLOW_TYPE_ID, 
                            {nameof(WorkflowInstance.Status)}, 
                            {nameof(WorkflowInstance.CurrentStepOrder)}, 
                            {nameof(WorkflowInstance.CurrentNodeId)}, 
                            {nameof(WorkflowInstance.CurrentNodeName)}, 
                            {nameof(WorkflowInstance.CreatedAt)}, 
                            {nameof(WorkflowInstance.UpdatedAt)}
                        )
                        VALUES (:Id, :WorkflowDefinitionId, :TargetEntityId, :WorkflowTypeId, :Status, :CurrentStepOrder, :CurrentNodeId, :CurrentNodeName, :CreatedAt, :UpdatedAt)";
            var parameters = new DynamicParameters();
            parameters.Add("Id", instance.Id.ToString());
            parameters.Add("WorkflowDefinitionId", instance.WorkflowDefinitionId.ToString());
            parameters.Add("TargetEntityId", string.IsNullOrEmpty(instance.TargetEntityId) ? null : instance.TargetEntityId);
            parameters.Add("WorkflowTypeId", instance.WorkflowTypeId);
            parameters.Add("Status", string.IsNullOrEmpty(instance.Status) ? null : instance.Status);
            parameters.Add("CurrentStepOrder", instance.CurrentStepOrder);
            parameters.Add("CurrentNodeId", string.IsNullOrEmpty(instance.CurrentNodeId) ? null : instance.CurrentNodeId);
            parameters.Add("CurrentNodeName", string.IsNullOrEmpty(instance.CurrentNodeName) ? null : instance.CurrentNodeName);
            parameters.Add("CreatedAt", instance.CreatedAt);
            parameters.Add("UpdatedAt", instance.UpdatedAt);

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        public async Task<bool> UpdateInstanceAsync(WorkflowInstance instance)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            instance.CreatedAt = DateTime.SpecifyKind(instance.CreatedAt, DateTimeKind.Unspecified);
            instance.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var sql = $@"UPDATE WORKFLOWINSTANCES
                        SET {nameof(WorkflowInstance.WorkflowDefinitionId)} = :WorkflowDefinitionId, 
                        {nameof(WorkflowInstance.TargetEntityId)} = :TargetEntityId, 
                        WORKFLOW_TYPE_ID = :WorkflowTypeId,
                        {nameof(WorkflowInstance.Status)} = :Status, 
                        {nameof(WorkflowInstance.CurrentStepOrder)} = :CurrentStepOrder, 
                        {nameof(WorkflowInstance.CurrentNodeId)} = :CurrentNodeId, 
                        {nameof(WorkflowInstance.CurrentNodeName)} = :CurrentNodeName, 
                        {nameof(WorkflowInstance.CreatedAt)} = :CreatedAt, 
                        {nameof(WorkflowInstance.UpdatedAt)} = :UpdatedAt
                        WHERE {nameof(WorkflowInstance.Id)} = :Id";
            var parameters = new DynamicParameters();
            parameters.Add("WorkflowDefinitionId", instance.WorkflowDefinitionId.ToString());
            parameters.Add("TargetEntityId", string.IsNullOrEmpty(instance.TargetEntityId) ? null : instance.TargetEntityId);
            parameters.Add("WorkflowTypeId", instance.WorkflowTypeId);
            parameters.Add("Status", string.IsNullOrEmpty(instance.Status) ? null : instance.Status);
            parameters.Add("CurrentStepOrder", instance.CurrentStepOrder);
            parameters.Add("CurrentNodeId", string.IsNullOrEmpty(instance.CurrentNodeId) ? null : instance.CurrentNodeId);
            parameters.Add("CurrentNodeName", string.IsNullOrEmpty(instance.CurrentNodeName) ? null : instance.CurrentNodeName);
            parameters.Add("CreatedAt", instance.CreatedAt);
            parameters.Add("UpdatedAt", instance.UpdatedAt);
            parameters.Add("Id", instance.Id.ToString());

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        public async Task<bool> DeleteInstancePhysicalAsync(Guid instanceId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                var idStr = instanceId.ToString();
                
                // 1. Xóa history
                var sqlDeleteHistory = "DELETE FROM WORKFLOWHISTORY WHERE WORKFLOWINSTANCEID = :InstanceId";
                await _connection.ExecuteAsync(sqlDeleteHistory, new { InstanceId = idStr }, transaction);
                
                // 2. Xóa tasks
                var sqlDeleteTasks = "DELETE FROM WORKFLOWTASKS WHERE WORKFLOWINSTANCEID = :InstanceId";
                await _connection.ExecuteAsync(sqlDeleteTasks, new { InstanceId = idStr }, transaction);
                
                // 3. Xóa instance
                var sqlDeleteInstance = "DELETE FROM WORKFLOWINSTANCES WHERE ID = :InstanceId";
                var affected = await _connection.ExecuteAsync(sqlDeleteInstance, new { InstanceId = idStr }, transaction);
                
                transaction.Commit();
                return affected > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<WorkflowTask?> GetTaskByIdAsync(Guid id)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT {nameof(WorkflowTask.Id)}, 
                                {nameof(WorkflowTask.WorkflowInstanceId)}, 
                                {nameof(WorkflowTask.StepId)}, 
                                {nameof(WorkflowTask.StepName)}, 
                                {nameof(WorkflowTask.AssignedRole)}, 
                                {nameof(WorkflowTask.AssigneeUserId)}, 
                                {nameof(WorkflowTask.Status)}, 
                                {nameof(WorkflowTask.CreatedAt)}, 
                                {nameof(WorkflowTask.CompletedAt)}
                        FROM WORKFLOWTASKS
                        WHERE {nameof(WorkflowTask.Id)} = :Id";
            var task = await _connection.QuerySingleOrDefaultAsync<WorkflowTask>(sql, new { Id = id.ToString() });
            if (task == null) return null;

            task.WorkflowInstance = await GetInstanceByIdAsync(task.WorkflowInstanceId);
            if (task.WorkflowInstance?.WorkflowDefinition != null)
            {
                task.Step = task.WorkflowInstance.WorkflowDefinition.Steps.FirstOrDefault(s => s.Id == task.StepId);
            }
            return task;
        }

        public async Task<IEnumerable<WorkflowTask>> GetPendingTasksByRolesAsync(List<string> roles, bool isAdmin, string userId, Guid? workflowInstanceId = null)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();

            var sql = $@"SELECT
                                t.{nameof(WorkflowTask.Id)},
                                t.{nameof(WorkflowTask.WorkflowInstanceId)},
                                t.{nameof(WorkflowTask.StepId)},
                                t.{nameof(WorkflowTask.StepName)},
                                t.{nameof(WorkflowTask.AssignedRole)},
                                t.{nameof(WorkflowTask.AssigneeUserId)},
                                t.{nameof(WorkflowTask.Status)},
                                t.{nameof(WorkflowTask.CreatedAt)},
                                t.{nameof(WorkflowTask.CompletedAt)},
                                wi.{nameof(WorkflowInstance.WorkflowDefinitionId)} AS InstanceDefinitionId,
                                wi.{nameof(WorkflowInstance.TargetEntityId)} AS TargetEntityId,
                                wi.WORKFLOW_TYPE_ID AS WorkflowTypeId,
                                wi.{nameof(WorkflowInstance.Status)} AS InstanceStatus,
                                wd.{nameof(WorkflowDefinition.Name)} AS DefinitionName,
                                ws.{nameof(WorkflowStep.ActionType)} AS StepActionType,
                                ws.{nameof(WorkflowStep.RequiredRole)} AS StepRequiredRole,
                                ws.""{nameof(WorkflowStep.Order)}"" AS StepOrder,
                                ws.{nameof(WorkflowStep.AllowEdit)} AS StepAllowEdit,
                                ws.{nameof(WorkflowStep.RequireSignature)} AS StepRequireSignature
                        FROM WORKFLOWTASKS t
                        INNER JOIN WORKFLOWINSTANCES wi ON t.{nameof(WorkflowTask.WorkflowInstanceId)} = wi.{nameof(WorkflowInstance.Id)}
                        LEFT JOIN WORKFLOWDEFINITIONS wd ON wi.{nameof(WorkflowInstance.WorkflowDefinitionId)} = wd.{nameof(WorkflowDefinition.Id)}
                        LEFT JOIN WORKFLOWSTEPS ws ON t.{nameof(WorkflowTask.StepId)} = ws.{nameof(WorkflowStep.Id)}
                        WHERE t.{nameof(WorkflowTask.Status)} = 'Pending'";

            var parameters = new DynamicParameters();
            if (workflowInstanceId.HasValue)
            {
                sql += $@" AND t.{nameof(WorkflowTask.WorkflowInstanceId)} = :WorkflowInstanceId";
                parameters.Add("WorkflowInstanceId", workflowInstanceId.Value.ToString());
            }

            if (!isAdmin)
            {
                sql += $@" AND t.{nameof(WorkflowTask.AssigneeUserId)} = :AssigneeUserId";
                parameters.Add("AssigneeUserId", userId);
            }

            sql += $@" ORDER BY t.{nameof(WorkflowTask.CreatedAt)} DESC";

            var rows = await _connection.QueryAsync<PendingWorkflowTaskRow>(sql, parameters);
            return rows.Select(MapPendingTaskRow).ToList();
        }

        public async Task<bool> CreateTaskAsync(WorkflowTask task)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            task.CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            if (task.CompletedAt.HasValue) task.CompletedAt = DateTime.SpecifyKind(task.CompletedAt.Value, DateTimeKind.Unspecified);
            var sql = $@"INSERT INTO WORKFLOWTASKS (
                            {nameof(WorkflowTask.Id)}, 
                            {nameof(WorkflowTask.WorkflowInstanceId)}, 
                            {nameof(WorkflowTask.StepId)}, 
                            {nameof(WorkflowTask.StepName)}, 
                            {nameof(WorkflowTask.AssignedRole)}, 
                            {nameof(WorkflowTask.AssigneeUserId)}, 
                            {nameof(WorkflowTask.Status)}, 
                            {nameof(WorkflowTask.CreatedAt)}, 
                            {nameof(WorkflowTask.CompletedAt)}
                        )
                        VALUES (:Id, :WorkflowInstanceId, :StepId, :StepName, :AssignedRole, :AssigneeUserId, :Status, :CreatedAt, :CompletedAt)";
            var parameters = new DynamicParameters();
            parameters.Add("Id", task.Id.ToString());
            parameters.Add("WorkflowInstanceId", task.WorkflowInstanceId.ToString());
            parameters.Add("StepId", task.StepId.ToString());
            parameters.Add("StepName", string.IsNullOrEmpty(task.StepName) ? null : task.StepName);
            parameters.Add("AssignedRole", string.IsNullOrEmpty(task.AssignedRole) ? null : task.AssignedRole);
            parameters.Add("AssigneeUserId", string.IsNullOrEmpty(task.AssigneeUserId) ? null : task.AssigneeUserId);
            parameters.Add("Status", string.IsNullOrEmpty(task.Status) ? null : task.Status);
            parameters.Add("CreatedAt", task.CreatedAt);
            parameters.Add("CompletedAt", task.CompletedAt);

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        public async Task<bool> UpdateTaskAsync(WorkflowTask task)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            task.CreatedAt = DateTime.SpecifyKind(task.CreatedAt, DateTimeKind.Unspecified);
            if (task.CompletedAt.HasValue) task.CompletedAt = DateTime.SpecifyKind(task.CompletedAt.Value, DateTimeKind.Unspecified);
            var sql = $@"UPDATE WORKFLOWTASKS
                        SET {nameof(WorkflowTask.WorkflowInstanceId)} = :WorkflowInstanceId, 
                            {nameof(WorkflowTask.StepId)} = :StepId, 
                            {nameof(WorkflowTask.StepName)} = :StepName,
                            {nameof(WorkflowTask.AssignedRole)} = :AssignedRole, 
                            {nameof(WorkflowTask.AssigneeUserId)} = :AssigneeUserId, 
                            {nameof(WorkflowTask.Status)} = :Status,
                            {nameof(WorkflowTask.CreatedAt)} = :CreatedAt, 
                            {nameof(WorkflowTask.CompletedAt)} = :CompletedAt
                        WHERE {nameof(WorkflowTask.Id)} = :Id";
            var parameters = new DynamicParameters();
            parameters.Add("WorkflowInstanceId", task.WorkflowInstanceId.ToString());
            parameters.Add("StepId", task.StepId.ToString());
            parameters.Add("StepName", string.IsNullOrEmpty(task.StepName) ? null : task.StepName);
            parameters.Add("AssignedRole", string.IsNullOrEmpty(task.AssignedRole) ? null : task.AssignedRole);
            parameters.Add("AssigneeUserId", string.IsNullOrEmpty(task.AssigneeUserId) ? null : task.AssigneeUserId);
            parameters.Add("Status", string.IsNullOrEmpty(task.Status) ? null : task.Status);
            parameters.Add("CreatedAt", task.CreatedAt);
            parameters.Add("CompletedAt", task.CompletedAt);
            parameters.Add("Id", task.Id.ToString());

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        public async Task<IEnumerable<WorkflowHistory>> GetHistoryByInstanceIdAsync(Guid instanceId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT h.Id, 
                                h.WorkflowInstanceId, 
                                h.StepName, 
                                h.""ACTION"" AS ""Action"", 
                                h.ActionByUserId, 
                                u.UserName AS ActionByUsername,
                                u.FullName AS ActionByFullName,
                                h.""Comment"", 
                                h.ActionDate
                        FROM WORKFLOWHISTORY h
                        LEFT JOIN APP_USER u ON h.ActionByUserId = u.Id
                        WHERE h.WorkflowInstanceId = :InstanceId
                        ORDER BY h.ActionDate ASC";
            return await _connection.QueryAsync<WorkflowHistory>(sql, new { InstanceId = instanceId.ToString() });
        }

        public async Task<bool> AddHistoryAsync(WorkflowHistory history)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            history.ActionDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var sql = $@"INSERT INTO WORKFLOWHISTORY (
                            Id, 
                            WorkflowInstanceId, 
                            StepName, 
                            ""ACTION"", 
                            ActionByUserId, 
                            ""Comment"", 
                            ActionDate
                        )
                        VALUES (:Id, :WorkflowInstanceId, :StepName, :ActionVal, :ActionByUserId, :CommentVal, :ActionDate)";
            var parameters = new DynamicParameters();
            parameters.Add("Id", history.Id.ToString());
            parameters.Add("WorkflowInstanceId", history.WorkflowInstanceId.ToString());
            parameters.Add("StepName", string.IsNullOrEmpty(history.StepName) ? null : history.StepName);
            parameters.Add("ActionVal", string.IsNullOrEmpty(history.Action) ? null : history.Action);
            parameters.Add("ActionByUserId", string.IsNullOrEmpty(history.ActionByUserId) ? null : history.ActionByUserId);
            parameters.Add("CommentVal", string.IsNullOrEmpty(history.Comment) ? null : history.Comment);
            parameters.Add("ActionDate", history.ActionDate);

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        public Task<bool> SaveChangesAsync()
        {
            return Task.FromResult(true);
        }

        public async Task<string?> GetLastHistoryActionAsync(Guid instanceId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            const string sql = @"
                SELECT h.""ACTION"" AS ""Action""
                FROM WORKFLOWHISTORY h
                WHERE h.WorkflowInstanceId = :InstanceId
                ORDER BY h.ActionDate DESC
                FETCH FIRST 1 ROWS ONLY";
            return await _connection.QueryFirstOrDefaultAsync<string>(
                sql, new { InstanceId = instanceId.ToString() });
        }

        public async Task<string?> GetPendingStepNameAsync(Guid instanceId)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            const string sql = @"
                SELECT wt.StepName
                FROM WORKFLOWTASKS wt
                WHERE wt.WorkflowInstanceId = :InstanceId
                  AND wt.Status = 'Pending'
                ORDER BY wt.CreatedAt DESC
                FETCH FIRST 1 ROWS ONLY";
            return await _connection.QueryFirstOrDefaultAsync<string>(
                sql, new { InstanceId = instanceId.ToString() });
        }

        public async Task<string?> GetPriorStepAssigneeAsync(Guid instanceId, Guid stepId, string stepName)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            const string sql = @"
                SELECT wt.AssigneeUserId
                FROM WORKFLOWTASKS wt
                WHERE wt.WorkflowInstanceId = :InstanceId
                  AND wt.Status <> 'Pending'
                  AND wt.AssigneeUserId IS NOT NULL
                  AND (
                      (:StepId <> '00000000-0000-0000-0000-000000000000' AND wt.StepId = :StepId)
                      OR LOWER(wt.StepName) = LOWER(:StepName)
                  )
                ORDER BY wt.CreatedAt DESC
                FETCH FIRST 1 ROWS ONLY";
            return await _connection.QueryFirstOrDefaultAsync<string>(
                sql,
                new
                {
                    InstanceId = instanceId.ToString(),
                    StepId = stepId.ToString(),
                    StepName = stepName
                });
        }

        public async Task ExecuteMoveBatchAsync(
            WorkflowTask updatedTask,
            WorkflowInstance updatedInstance,
            WorkflowTask? newPendingTask,
            WorkflowHistory history)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();

            updatedTask.CompletedAt = updatedTask.CompletedAt.HasValue
                ? DateTime.SpecifyKind(updatedTask.CompletedAt.Value, DateTimeKind.Unspecified)
                : null;
            updatedInstance.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            history.ActionDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            using var tx = _connection.BeginTransaction();
            try
            {
                var updateTaskSql = $@"UPDATE WORKFLOWTASKS
                        SET {nameof(WorkflowTask.Status)} = :Status,
                            {nameof(WorkflowTask.AssigneeUserId)} = :AssigneeUserId,
                            {nameof(WorkflowTask.CompletedAt)} = :CompletedAt
                        WHERE {nameof(WorkflowTask.Id)} = :Id";
                await _connection.ExecuteAsync(updateTaskSql, new
                {
                    Status = updatedTask.Status,
                    AssigneeUserId = string.IsNullOrEmpty(updatedTask.AssigneeUserId) ? null : updatedTask.AssigneeUserId,
                    CompletedAt = updatedTask.CompletedAt,
                    Id = updatedTask.Id.ToString()
                }, tx);

                var updateInstanceSql = $@"UPDATE WORKFLOWINSTANCES
                        SET {nameof(WorkflowInstance.Status)} = :Status,
                            {nameof(WorkflowInstance.CurrentStepOrder)} = :CurrentStepOrder,
                            {nameof(WorkflowInstance.CurrentNodeId)} = :CurrentNodeId,
                            {nameof(WorkflowInstance.CurrentNodeName)} = :CurrentNodeName,
                            {nameof(WorkflowInstance.UpdatedAt)} = :UpdatedAt
                        WHERE {nameof(WorkflowInstance.Id)} = :Id";
                await _connection.ExecuteAsync(updateInstanceSql, new
                {
                    Status = updatedInstance.Status,
                    CurrentStepOrder = updatedInstance.CurrentStepOrder,
                    CurrentNodeId = updatedInstance.CurrentNodeId,
                    CurrentNodeName = updatedInstance.CurrentNodeName,
                    UpdatedAt = updatedInstance.UpdatedAt,
                    Id = updatedInstance.Id.ToString()
                }, tx);

                if (newPendingTask is not null)
                {
                    newPendingTask.CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    var insertTaskSql = $@"INSERT INTO WORKFLOWTASKS (
                            {nameof(WorkflowTask.Id)},
                            {nameof(WorkflowTask.WorkflowInstanceId)},
                            {nameof(WorkflowTask.StepId)},
                            {nameof(WorkflowTask.StepName)},
                            {nameof(WorkflowTask.AssignedRole)},
                            {nameof(WorkflowTask.AssigneeUserId)},
                            {nameof(WorkflowTask.Status)},
                            {nameof(WorkflowTask.CreatedAt)},
                            {nameof(WorkflowTask.CompletedAt)}
                        )
                        VALUES (:Id, :WorkflowInstanceId, :StepId, :StepName, :AssignedRole, :AssigneeUserId, :Status, :CreatedAt, :CompletedAt)";
                    await _connection.ExecuteAsync(insertTaskSql, new
                    {
                        Id = newPendingTask.Id.ToString(),
                        WorkflowInstanceId = newPendingTask.WorkflowInstanceId.ToString(),
                        StepId = newPendingTask.StepId.ToString(),
                        StepName = newPendingTask.StepName,
                        AssignedRole = newPendingTask.AssignedRole,
                        AssigneeUserId = string.IsNullOrEmpty(newPendingTask.AssigneeUserId) ? null : newPendingTask.AssigneeUserId,
                        Status = newPendingTask.Status,
                        CreatedAt = newPendingTask.CreatedAt,
                        CompletedAt = newPendingTask.CompletedAt
                    }, tx);
                }

                var historySql = @"INSERT INTO WORKFLOWHISTORY (
                            Id,
                            WorkflowInstanceId,
                            StepName,
                            ""ACTION"",
                            ActionByUserId,
                            ""Comment"",
                            ActionDate
                        )
                        VALUES (:Id, :WorkflowInstanceId, :StepName, :ActionVal, :ActionByUserId, :CommentVal, :ActionDate)";
                await _connection.ExecuteAsync(historySql, new
                {
                    Id = history.Id.ToString(),
                    WorkflowInstanceId = history.WorkflowInstanceId.ToString(),
                    StepName = history.StepName,
                    ActionVal = history.Action,
                    ActionByUserId = history.ActionByUserId,
                    CommentVal = history.Comment,
                    ActionDate = history.ActionDate
                }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task CreateSubmitBatchAsync(WorkflowInstance instance, WorkflowTask task, WorkflowHistory history)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();

            var now = DateTime.UtcNow;
            instance.CreatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
            instance.UpdatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
            task.CreatedAt = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
            history.ActionDate = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);

            using var tx = _connection.BeginTransaction();
            try
            {
                var instanceSql = $@"INSERT INTO WORKFLOWINSTANCES (
                            {nameof(WorkflowInstance.Id)}, 
                            {nameof(WorkflowInstance.WorkflowDefinitionId)}, 
                            {nameof(WorkflowInstance.TargetEntityId)}, 
                            WORKFLOW_TYPE_ID, 
                            {nameof(WorkflowInstance.Status)}, 
                            {nameof(WorkflowInstance.CurrentStepOrder)}, 
                            {nameof(WorkflowInstance.CurrentNodeId)}, 
                            {nameof(WorkflowInstance.CurrentNodeName)}, 
                            {nameof(WorkflowInstance.CreatedAt)}, 
                            {nameof(WorkflowInstance.UpdatedAt)}
                        )
                        VALUES (:Id, :WorkflowDefinitionId, :TargetEntityId, :WorkflowTypeId, :Status, :CurrentStepOrder, :CurrentNodeId, :CurrentNodeName, :CreatedAt, :UpdatedAt)";

                await _connection.ExecuteAsync(instanceSql, new
                {
                    Id = instance.Id.ToString(),
                    WorkflowDefinitionId = instance.WorkflowDefinitionId.ToString(),
                    TargetEntityId = instance.TargetEntityId,
                    WorkflowTypeId = instance.WorkflowTypeId,
                    Status = instance.Status,
                    CurrentStepOrder = instance.CurrentStepOrder,
                    CurrentNodeId = instance.CurrentNodeId,
                    CurrentNodeName = instance.CurrentNodeName,
                    CreatedAt = instance.CreatedAt,
                    UpdatedAt = instance.UpdatedAt
                }, tx);

                var taskSql = $@"INSERT INTO WORKFLOWTASKS (
                            {nameof(WorkflowTask.Id)}, 
                            {nameof(WorkflowTask.WorkflowInstanceId)}, 
                            {nameof(WorkflowTask.StepId)}, 
                            {nameof(WorkflowTask.StepName)}, 
                            {nameof(WorkflowTask.AssignedRole)}, 
                            {nameof(WorkflowTask.AssigneeUserId)}, 
                            {nameof(WorkflowTask.Status)}, 
                            {nameof(WorkflowTask.CreatedAt)}, 
                            {nameof(WorkflowTask.CompletedAt)}
                        )
                        VALUES (:Id, :WorkflowInstanceId, :StepId, :StepName, :AssignedRole, :AssigneeUserId, :Status, :CreatedAt, :CompletedAt)";

                await _connection.ExecuteAsync(taskSql, new
                {
                    Id = task.Id.ToString(),
                    WorkflowInstanceId = task.WorkflowInstanceId.ToString(),
                    StepId = task.StepId.ToString(),
                    StepName = task.StepName,
                    AssignedRole = task.AssignedRole,
                    AssigneeUserId = task.AssigneeUserId,
                    Status = task.Status,
                    CreatedAt = task.CreatedAt,
                    CompletedAt = task.CompletedAt
                }, tx);

                var historySql = @"INSERT INTO WORKFLOWHISTORY (
                            Id, 
                            WorkflowInstanceId, 
                            StepName, 
                            ""ACTION"", 
                            ActionByUserId, 
                            ""Comment"", 
                            ActionDate
                        )
                        VALUES (:Id, :WorkflowInstanceId, :StepName, :ActionVal, :ActionByUserId, :CommentVal, :ActionDate)";

                await _connection.ExecuteAsync(historySql, new
                {
                    Id = history.Id.ToString(),
                    WorkflowInstanceId = history.WorkflowInstanceId.ToString(),
                    StepName = history.StepName,
                    ActionVal = history.Action,
                    ActionByUserId = history.ActionByUserId,
                    CommentVal = history.Comment,
                    ActionDate = history.ActionDate
                }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private Task AttachStepsToDefinitionAsync(WorkflowDefinition definition) =>
            AttachStepsToDefinitionsAsync(new List<WorkflowDefinition> { definition });

        private async Task AttachStepsToDefinitionsAsync(IList<WorkflowDefinition> definitions)
        {
            if (definitions.Count == 0) return;

            var definitionIds = definitions.Select(d => d.Id.ToString()).Distinct().ToList();
            var sqlSteps = $@"SELECT {nameof(WorkflowStep.Id)},
                                     {nameof(WorkflowStep.WorkflowDefinitionId)},
                                     {nameof(WorkflowStep.StepName)},
                                     ""{nameof(WorkflowStep.Order)}"",
                                     {nameof(WorkflowStep.RequiredRole)},
                                     {nameof(WorkflowStep.ActionType)},
                                     {nameof(WorkflowStep.AllowEdit)},
                                     {nameof(WorkflowStep.RequireSignature)},
                                     SYSTEM_PERMISSION_GROUP_IDS AS {nameof(WorkflowStep.SystemPermissionGroupIds)},
                                     UNIT_PERMISSION_GROUP_IDS   AS {nameof(WorkflowStep.UnitPermissionGroupIds)},
                                     REQUIRE_SAME_UNIT           AS {nameof(WorkflowStep.RequireSameUnit)},
                                     ASSIGNEE_ID                 AS {nameof(WorkflowStep.AssigneeId)}
                              FROM WORKFLOWSTEPS
                              WHERE {nameof(WorkflowStep.WorkflowDefinitionId)} IN :DefinitionIds
                              ORDER BY {nameof(WorkflowStep.WorkflowDefinitionId)}, ""{nameof(WorkflowStep.Order)}""";

            var allSteps = (await _connection.QueryAsync<WorkflowStep>(sqlSteps, new { DefinitionIds = definitionIds })).ToList();
            var stepsByDefinitionId = allSteps.GroupBy(s => s.WorkflowDefinitionId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var definition in definitions)
            {
                if (stepsByDefinitionId.TryGetValue(definition.Id, out var steps))
                {
                    definition.Steps = steps;
                    foreach (var step in steps)
                        step.WorkflowDefinition = definition;
                }
                else
                {
                    definition.Steps = new List<WorkflowStep>();
                }
            }
        }

        private static WorkflowTask MapPendingTaskRow(PendingWorkflowTaskRow row)
        {
            var definition = new WorkflowDefinition
            {
                Id = row.InstanceDefinitionId,
                Name = row.DefinitionName ?? string.Empty,
            };

            var instance = new WorkflowInstance
            {
                Id = row.WorkflowInstanceId,
                WorkflowDefinitionId = row.InstanceDefinitionId,
                TargetEntityId = row.TargetEntityId,
                WorkflowTypeId = row.WorkflowTypeId,
                Status = row.InstanceStatus,
                WorkflowDefinition = definition,
            };

            var step = new WorkflowStep
            {
                Id = row.StepId,
                WorkflowDefinitionId = row.InstanceDefinitionId,
                StepName = row.StepName,
                ActionType = row.StepActionType ?? string.Empty,
                RequiredRole = row.StepRequiredRole ?? row.AssignedRole,
                Order = row.StepOrder,
                AllowEdit = row.StepAllowEdit,
                RequireSignature = row.StepRequireSignature,
                WorkflowDefinition = definition,
            };

            return new WorkflowTask
            {
                Id = row.Id,
                WorkflowInstanceId = row.WorkflowInstanceId,
                StepId = row.StepId,
                StepName = row.StepName,
                AssignedRole = row.AssignedRole,
                AssigneeUserId = row.AssigneeUserId,
                Status = row.Status,
                CreatedAt = row.CreatedAt,
                CompletedAt = row.CompletedAt,
                WorkflowInstance = instance,
                Step = step,
            };
        }

        public async Task<bool> ReactivateDefinitionAsync(Guid id, int workflowTypeId, string name)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            using var transaction = _connection.BeginTransaction();
            try
            {
                // 1. Deactivate all versions of the same workflowTypeId
                var sqlDeactivate = @"UPDATE WORKFLOWDEFINITIONS 
                                      SET IsActive = 0, UpdatedAt = :UpdatedAt
                                      WHERE WORKFLOW_TYPE_ID = :WorkflowTypeId AND IsActive = 1";
                await _connection.ExecuteAsync(sqlDeactivate, new { WorkflowTypeId = workflowTypeId, UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) }, transaction);

                // 2. Activate the specified version
                var sqlActivate = @"UPDATE WORKFLOWDEFINITIONS 
                                    SET IsActive = 1, UpdatedAt = :UpdatedAt
                                    WHERE Id = :Id";
                var affected = await _connection.ExecuteAsync(sqlActivate, new { Id = id.ToString(), UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) }, transaction);

                // Instance đang Running giữ nguyên WorkflowDefinitionId gốc; chỉ submit mới dùng definition active.

                transaction.Commit();
                return affected > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private sealed class PendingWorkflowTaskRow
        {
            public Guid Id { get; set; }
            public Guid WorkflowInstanceId { get; set; }
            public Guid StepId { get; set; }
            public string StepName { get; set; } = string.Empty;
            public string AssignedRole { get; set; } = string.Empty;
            public string? AssigneeUserId { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public Guid InstanceDefinitionId { get; set; }
            public string TargetEntityId { get; set; } = string.Empty;
            public int WorkflowTypeId { get; set; }
            public string InstanceStatus { get; set; } = string.Empty;
            public string? DefinitionName { get; set; }
            public string? StepActionType { get; set; }
            public string? StepRequiredRole { get; set; }
            public int StepOrder { get; set; }
            public bool StepAllowEdit { get; set; }
            public bool StepRequireSignature { get; set; }
        }
    }
}
