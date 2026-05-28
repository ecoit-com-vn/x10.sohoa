using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using EvnHanoi.DigitizationService.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace EvnHanoi.DigitizationService.Repositories
{
    public class DigitizationTaskRepository : IDigitizationTaskRepository
    {
        private readonly string _connectionString;

        public DigitizationTaskRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }

        public async Task<Guid> CreateAsync(DigitizationTask task)
        {
            var sql = @"
                INSERT INTO DIGITIZATION_TASK (
                    ID, DOSSIER_ID, WORKFLOW_STEP_ID, ASSIGNED_TO_USER_ID, STATUS, CREATED_AT, COMPLETED_AT, NOTES
                ) VALUES (
                    :Id, :DossierId, :WorkflowStepId, :AssignedToUserId, :Status, :CreatedAt, :CompletedAt, :Notes
                )";

            if (task.Id == Guid.Empty)
            {
                task.Id = Guid.NewGuid();
            }

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new {
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
            var sql = @"SELECT 
                ID as Id, 
                DOSSIER_ID as DossierId, 
                WORKFLOW_STEP_ID as WorkflowStepId, 
                ASSIGNED_TO_USER_ID as AssignedToUserId, 
                STATUS as Status, 
                CREATED_AT as CreatedAt, 
                COMPLETED_AT as CompletedAt, 
                NOTES as Notes 
                FROM DIGITIZATION_TASK WHERE ID = :Id";
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<DigitizationTask>(sql, new { Id = id.ToString() });
        }

        public async Task<IEnumerable<DigitizationTask>> GetByUserIdAsync(string userId)
        {
            var sql = @"SELECT 
                ID as Id, 
                DOSSIER_ID as DossierId, 
                WORKFLOW_STEP_ID as WorkflowStepId, 
                ASSIGNED_TO_USER_ID as AssignedToUserId, 
                STATUS as Status, 
                CREATED_AT as CreatedAt, 
                COMPLETED_AT as CompletedAt, 
                NOTES as Notes 
                FROM DIGITIZATION_TASK WHERE ASSIGNED_TO_USER_ID = :UserId";
            using var connection = CreateConnection();
            return await connection.QueryAsync<DigitizationTask>(sql, new { UserId = userId });
        }

        public async Task UpdateAsync(DigitizationTask task)
        {
            var sql = @"
                UPDATE DIGITIZATION_TASK SET 
                    STATUS = :Status,
                    COMPLETED_AT = :CompletedAt,
                    NOTES = :Notes
                WHERE ID = :Id";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, new {
                Id = task.Id.ToString(),
                Status = task.Status,
                CompletedAt = task.CompletedAt,
                Notes = task.Notes
            });
        }
    }
}
