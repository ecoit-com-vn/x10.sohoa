// Microservices/EvnHanoi.ReportService/Infrastructure/Repositories/ReportRepository.cs
using Dapper;
using EvnHanoi.ReportService.Core.Entities;
using EvnHanoi.ReportService.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Infrastructure.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly IConfiguration _configuration;

        public ReportRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection GetConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            return new OracleConnection(connectionString);
        }

        // --- REPORT GROUP ---
        
        public async Task<IEnumerable<ReportGroup>> GetReportGroupsAsync()
        {
            using var connection = GetConnection();
            var sqlGroup = "SELECT Id, Name, SortOrder, Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM REPORT_GROUPS ORDER BY SortOrder, Name";
            var sqlReport = "SELECT Id, GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM DYNAMIC_REPORTS ORDER BY Name";
            
            var groups = (await connection.QueryAsync<ReportGroup>(sqlGroup)).ToList();
            var reports = (await connection.QueryAsync<DynamicReport>(sqlReport)).ToList();
            
            foreach (var group in groups)
            {
                group.DynamicReports = reports.Where(r => r.GroupId == group.Id).ToList();
            }
            
            return groups;
        }

        public async Task<ReportGroup?> GetReportGroupByIdAsync(long id)
        {
            using var connection = GetConnection();
            var sqlGroup = "SELECT Id, Name, SortOrder, Description, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM REPORT_GROUPS WHERE Id = :Id";
            var sqlReport = "SELECT Id, GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM DYNAMIC_REPORTS WHERE GroupId = :GroupId ORDER BY Name";
            
            var group = await connection.QueryFirstOrDefaultAsync<ReportGroup>(sqlGroup, new { Id = id });
            if (group != null)
            {
                var reports = await connection.QueryAsync<DynamicReport>(sqlReport, new { GroupId = id });
                group.DynamicReports = reports.ToList();
            }
            
            return group;
        }

        public async Task<long> CreateReportGroupAsync(ReportGroup group)
        {
            using var connection = GetConnection();
            var sql = "INSERT INTO REPORT_GROUPS (Name, SortOrder, Description, CreatedBy) VALUES (:Name, :SortOrder, :Description, :CreatedBy) RETURNING Id INTO :Id";
            
            var p = new DynamicParameters();
            p.Add("Name", group.Name);
            p.Add("SortOrder", group.SortOrder);
            p.Add("Description", group.Description);
            p.Add("CreatedBy", group.CreatedBy);
            p.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
            
            await connection.ExecuteAsync(sql, p);
            return p.Get<long>("Id");
        }

        public async Task<bool> UpdateReportGroupAsync(ReportGroup group)
        {
            using var connection = GetConnection();
            var sql = @"
                UPDATE REPORT_GROUPS 
                SET Name = :Name, 
                    SortOrder = :SortOrder, 
                    Description = :Description, 
                    UpdatedAt = :UpdatedAt, 
                    UpdatedBy = :UpdatedBy 
                WHERE Id = :Id";
            
            var rows = await connection.ExecuteAsync(sql, new
            {
                group.Name,
                group.SortOrder,
                group.Description,
                UpdatedAt = DateTime.UtcNow,
                group.UpdatedBy,
                group.Id
            });
            return rows > 0;
        }

        public async Task<bool> DeleteReportGroupAsync(long id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM REPORT_GROUPS WHERE Id = :Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        // --- DYNAMIC REPORT ---

        public async Task<IEnumerable<DynamicReport>> GetDynamicReportsByGroupIdAsync(long groupId)
        {
            using var connection = GetConnection();
            var sql = "SELECT Id, GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM DYNAMIC_REPORTS WHERE GroupId = :GroupId ORDER BY Name";
            return await connection.QueryAsync<DynamicReport>(sql, new { GroupId = groupId });
        }

        public async Task<DynamicReport?> GetDynamicReportByIdAsync(long id)
        {
            using var connection = GetConnection();
            var sql = "SELECT Id, GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy FROM DYNAMIC_REPORTS WHERE Id = :Id";
            return await connection.QueryFirstOrDefaultAsync<DynamicReport>(sql, new { Id = id });
        }

        public async Task<long> CreateDynamicReportAsync(DynamicReport report)
        {
            using var connection = GetConnection();
            var sql = @"
                INSERT INTO DYNAMIC_REPORTS (GroupId, Name, SqlQuery, ParametersJson, AllowedRoles, IsActive, CreatedBy) 
                VALUES (:GroupId, :Name, :SqlQuery, :ParametersJson, :AllowedRoles, :IsActive, :CreatedBy) 
                RETURNING Id INTO :Id";
            
            var p = new DynamicParameters();
            p.Add("GroupId", report.GroupId);
            p.Add("Name", report.Name);
            p.Add("SqlQuery", report.SqlQuery);
            p.Add("ParametersJson", report.ParametersJson);
            p.Add("AllowedRoles", report.AllowedRoles);
            p.Add("IsActive", report.IsActive);
            p.Add("CreatedBy", report.CreatedBy);
            p.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
            
            await connection.ExecuteAsync(sql, p);
            return p.Get<long>("Id");
        }

        public async Task<bool> UpdateDynamicReportAsync(DynamicReport report)
        {
            using var connection = GetConnection();
            var sql = @"
                UPDATE DYNAMIC_REPORTS 
                SET GroupId = :GroupId, 
                    Name = :Name, 
                    SqlQuery = :SqlQuery, 
                    ParametersJson = :ParametersJson, 
                    AllowedRoles = :AllowedRoles, 
                    IsActive = :IsActive, 
                    UpdatedAt = :UpdatedAt, 
                    UpdatedBy = :UpdatedBy 
                WHERE Id = :Id";
            
            var rows = await connection.ExecuteAsync(sql, new
            {
                report.GroupId,
                report.Name,
                report.SqlQuery,
                report.ParametersJson,
                report.AllowedRoles,
                report.IsActive,
                UpdatedAt = DateTime.UtcNow,
                report.UpdatedBy,
                report.Id
            });
            return rows > 0;
        }

        public async Task<bool> DeleteDynamicReportAsync(long id)
        {
            using var connection = GetConnection();
            var sql = "DELETE FROM DYNAMIC_REPORTS WHERE Id = :Id";
            var rows = await connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        // --- EXECUTE SQL ---

        public async Task<IEnumerable<IDictionary<string, object>>> ExecuteDynamicQueryAsync(string sql, Dictionary<string, object>? parameters)
        {
            using var connection = GetConnection();
            var dynamicParameters = new DynamicParameters();
            
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    // Chuyển đổi kiểu dữ liệu cho tham số nếu cần thiết
                    var val = param.Value;
                    if (val is System.Text.Json.JsonElement jsonEl)
                    {
                        switch (jsonEl.ValueKind)
                        {
                            case System.Text.Json.JsonValueKind.String:
                                val = jsonEl.GetString();
                                break;
                            case System.Text.Json.JsonValueKind.Number:
                                if (jsonEl.TryGetInt64(out long lVal)) val = lVal;
                                else if (jsonEl.TryGetDouble(out double dVal)) val = dVal;
                                break;
                            case System.Text.Json.JsonValueKind.True:
                                val = 1;
                                break;
                            case System.Text.Json.JsonValueKind.False:
                                val = 0;
                                break;
                            case System.Text.Json.JsonValueKind.Null:
                                val = null;
                                break;
                        }
                    }
                    dynamicParameters.Add(param.Key, val);
                }
            }
            
            var result = await connection.QueryAsync(sql, dynamicParameters);
            
            var list = new List<IDictionary<string, object>>();
            foreach (var row in result)
            {
                var dict = new Dictionary<string, object>();
                // DapperRow implement IDictionary<string, object>
                var dapperRow = (IDictionary<string, object>)row;
                foreach (var kvp in dapperRow)
                {
                    dict[kvp.Key] = kvp.Value;
                }
                list.Add(dict);
            }
            return list;
        }
    }
}
