using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class DigitizationTaskRepository : IDigitizationTaskRepository
    {
        private readonly IDbConnection _connection;

        public DigitizationTaskRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<Guid> CreateAsync(DigitizationTask task)
        {
            var sql = $@"
                INSERT INTO DIGITIZATION_TASK (
                    ID, DOSSIER_ID, WORKFLOW_STEP_ID, ASSIGNED_TO_USER_ID, STATUS, CREATED_AT, COMPLETED_AT, NOTES
                ) VALUES (
                    :{nameof(DigitizationTask.Id)}, :{nameof(DigitizationTask.DossierId)}, :{nameof(DigitizationTask.WorkflowStepId)}, :{nameof(DigitizationTask.AssignedToUserId)}, :{nameof(DigitizationTask.Status)}, :{nameof(DigitizationTask.CreatedAt)}, :{nameof(DigitizationTask.CompletedAt)}, :{nameof(DigitizationTask.Notes)}
                )";

            if (task.Id == Guid.Empty)
            {
                task.Id = Guid.NewGuid();
            }

            await _connection.ExecuteAsync(sql, new {
                Id = task.Id.ToString(),
                DossierId = task.DossierId,
                WorkflowStepId = task.WorkflowStepId.ToString(),
                AssignedToUserId = task.AssignedToUserId,
                Status = task.Status,
                CreatedAt = task.CreatedAt,
                CompletedAt = task.CompletedAt,
                Notes = task.Notes
            });
            
            return task.Id;
        }

        public async Task<DigitizationTask?> GetByIdAsync(Guid id)
        {
            var sql = $@"SELECT 
                ID as {nameof(DigitizationTask.Id)}, 
                DOSSIER_ID as {nameof(DigitizationTask.DossierId)}, 
                WORKFLOW_STEP_ID as {nameof(DigitizationTask.WorkflowStepId)}, 
                ASSIGNED_TO_USER_ID as {nameof(DigitizationTask.AssignedToUserId)}, 
                STATUS as {nameof(DigitizationTask.Status)}, 
                CREATED_AT as {nameof(DigitizationTask.CreatedAt)}, 
                COMPLETED_AT as {nameof(DigitizationTask.CompletedAt)}, 
                NOTES as {nameof(DigitizationTask.Notes)} 
                FROM DIGITIZATION_TASK WHERE ID = :Id";
            return await _connection.QueryFirstOrDefaultAsync<DigitizationTask>(sql, new { Id = id.ToString() });
        }

        public async Task<IEnumerable<DigitizationTask>> GetByUserIdAsync(string userId)
        {
            var sql = $@"SELECT 
                ID as {nameof(DigitizationTask.Id)}, 
                DOSSIER_ID as {nameof(DigitizationTask.DossierId)}, 
                WORKFLOW_STEP_ID as {nameof(DigitizationTask.WorkflowStepId)}, 
                ASSIGNED_TO_USER_ID as {nameof(DigitizationTask.AssignedToUserId)}, 
                STATUS as {nameof(DigitizationTask.Status)}, 
                CREATED_AT as {nameof(DigitizationTask.CreatedAt)}, 
                COMPLETED_AT as {nameof(DigitizationTask.CompletedAt)}, 
                NOTES as {nameof(DigitizationTask.Notes)} 
                FROM DIGITIZATION_TASK WHERE ASSIGNED_TO_USER_ID = :UserId";
            return await _connection.QueryAsync<DigitizationTask>(sql, new { UserId = userId });
        }

        public async Task<IEnumerable<DigitizationTask>> GetAllAsync()
        {
            var sql = $@"SELECT 
                ID as {nameof(DigitizationTask.Id)}, 
                DOSSIER_ID as {nameof(DigitizationTask.DossierId)}, 
                WORKFLOW_STEP_ID as {nameof(DigitizationTask.WorkflowStepId)}, 
                ASSIGNED_TO_USER_ID as {nameof(DigitizationTask.AssignedToUserId)}, 
                STATUS as {nameof(DigitizationTask.Status)}, 
                CREATED_AT as {nameof(DigitizationTask.CreatedAt)}, 
                COMPLETED_AT as {nameof(DigitizationTask.CompletedAt)}, 
                NOTES as {nameof(DigitizationTask.Notes)} 
                FROM DIGITIZATION_TASK";
            return await _connection.QueryAsync<DigitizationTask>(sql);
        }

        public async Task<(IEnumerable<DigitizationTask> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            
            var whereClause = "";
            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                whereClause = "WHERE (UPPER(DOSSIER_ID) LIKE UPPER(:Keyword) OR UPPER(NOTES) LIKE UPPER(:Keyword) OR UPPER(ASSIGNED_TO_USER_ID) LIKE UPPER(:Keyword))";
                parameters.Add("Keyword", $"%{keyword}%");
            }
            
            var countSql = $"SELECT COUNT(*) FROM DIGITIZATION_TASK {whereClause}";
            var offset = (page - 1) * pageSize;
            
            var sql = $@"
                SELECT * FROM (
                    SELECT 
                        ID as {nameof(DigitizationTask.Id)}, 
                        DOSSIER_ID as {nameof(DigitizationTask.DossierId)}, 
                        WORKFLOW_STEP_ID as {nameof(DigitizationTask.WorkflowStepId)}, 
                        ASSIGNED_TO_USER_ID as {nameof(DigitizationTask.AssignedToUserId)}, 
                        STATUS as {nameof(DigitizationTask.Status)}, 
                        CREATED_AT as {nameof(DigitizationTask.CreatedAt)}, 
                        COMPLETED_AT as {nameof(DigitizationTask.CompletedAt)}, 
                        NOTES as {nameof(DigitizationTask.Notes)},
                        ROW_NUMBER() OVER (ORDER BY CREATED_AT DESC) AS RN
                    FROM DIGITIZATION_TASK
                    {whereClause}
                ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";
                
            parameters.Add("Offset", offset);
            parameters.Add("OffsetPlusSize", offset + pageSize);
            
            var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
            var items = await _connection.QueryAsync<DigitizationTask>(sql, parameters);
            
            return (items, totalCount);
        }

        public async Task UpdateAsync(DigitizationTask task)
        {
            var sql = $@"
                UPDATE DIGITIZATION_TASK SET 
                    STATUS = :{nameof(DigitizationTask.Status)},
                    COMPLETED_AT = :{nameof(DigitizationTask.CompletedAt)},
                    NOTES = :{nameof(DigitizationTask.Notes)}
                WHERE ID = :{nameof(DigitizationTask.Id)}";
            await _connection.ExecuteAsync(sql, new {
                Id = task.Id.ToString(),
                Status = task.Status,
                CompletedAt = task.CompletedAt,
                Notes = task.Notes
            });
        }
    }
}
