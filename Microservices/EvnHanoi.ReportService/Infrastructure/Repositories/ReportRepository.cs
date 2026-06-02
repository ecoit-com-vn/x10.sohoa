// Microservices/EvnHanoi.ReportService/Infrastructure/Repositories/ReportRepository.cs
using Dapper;
using EvnHanoi.ReportService.Core.Entities;
using EvnHanoi.ReportService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Infrastructure.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly IDbConnection _connection;

        public ReportRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        // --- REPORT GROUP ---
        
        public async Task<IEnumerable<ReportGroup>> GetReportGroupsAsync()
        {
            var sqlGroup = $"SELECT {nameof(ReportGroup.Id)}, {nameof(ReportGroup.Name)}, {nameof(ReportGroup.SortOrder)}, {nameof(ReportGroup.Description)}, {nameof(ReportGroup.CreatedAt)}, {nameof(ReportGroup.CreatedBy)}, {nameof(ReportGroup.UpdatedAt)}, {nameof(ReportGroup.UpdatedBy)} FROM REPORT_GROUPS ORDER BY {nameof(ReportGroup.SortOrder)}, {nameof(ReportGroup.Name)}";
            var sqlReport = $"SELECT {nameof(DynamicReport.Id)}, {nameof(DynamicReport.GroupId)}, {nameof(DynamicReport.Name)}, {nameof(DynamicReport.SqlQuery)}, {nameof(DynamicReport.ParametersJson)}, {nameof(DynamicReport.AllowedRoles)}, {nameof(DynamicReport.IsActive)}, {nameof(DynamicReport.CreatedAt)}, {nameof(DynamicReport.CreatedBy)}, {nameof(DynamicReport.UpdatedAt)}, {nameof(DynamicReport.UpdatedBy)} FROM DYNAMIC_REPORTS ORDER BY {nameof(DynamicReport.Name)}";
            
            var groups = (await _connection.QueryAsync<ReportGroup>(sqlGroup)).ToList();
            var reports = (await _connection.QueryAsync<DynamicReport>(sqlReport)).ToList();
            
            foreach (var group in groups)
            {
                group.DynamicReports = reports.Where(r => r.GroupId == group.Id).ToList();
            }
            
            return groups;
        }

        public async Task<ReportGroup?> GetReportGroupByIdAsync(long id)
        {
            var sqlGroup = $"SELECT {nameof(ReportGroup.Id)}, {nameof(ReportGroup.Name)}, {nameof(ReportGroup.SortOrder)}, {nameof(ReportGroup.Description)}, {nameof(ReportGroup.CreatedAt)}, {nameof(ReportGroup.CreatedBy)}, {nameof(ReportGroup.UpdatedAt)}, {nameof(ReportGroup.UpdatedBy)} FROM REPORT_GROUPS WHERE {nameof(ReportGroup.Id)} = :Id";
            var sqlReport = $"SELECT {nameof(DynamicReport.Id)}, {nameof(DynamicReport.GroupId)}, {nameof(DynamicReport.Name)}, {nameof(DynamicReport.SqlQuery)}, {nameof(DynamicReport.ParametersJson)}, {nameof(DynamicReport.AllowedRoles)}, {nameof(DynamicReport.IsActive)}, {nameof(DynamicReport.CreatedAt)}, {nameof(DynamicReport.CreatedBy)}, {nameof(DynamicReport.UpdatedAt)}, {nameof(DynamicReport.UpdatedBy)} FROM DYNAMIC_REPORTS WHERE {nameof(DynamicReport.GroupId)} = :GroupId ORDER BY {nameof(DynamicReport.Name)}";
            
            var group = await _connection.QueryFirstOrDefaultAsync<ReportGroup>(sqlGroup, new { Id = id });
            if (group != null)
            {
                var reports = await _connection.QueryAsync<DynamicReport>(sqlReport, new { GroupId = id });
                group.DynamicReports = reports.ToList();
            }
            
            return group;
        }

        public async Task<long> CreateReportGroupAsync(ReportGroup group)
        {
            var sql = $@"INSERT INTO REPORT_GROUPS (
                            {nameof(ReportGroup.Name)}, 
                            {nameof(ReportGroup.SortOrder)}, 
                            {nameof(ReportGroup.Description)}, 
                            {nameof(ReportGroup.CreatedBy)}
                        ) 
                        VALUES (:Name, :SortOrder, :Description, :CreatedBy) 
                        RETURNING {nameof(ReportGroup.Id)} INTO :Id";
            
            var p = new DynamicParameters();
            p.Add("Name", group.Name);
            p.Add("SortOrder", group.SortOrder);
            p.Add("Description", group.Description);
            p.Add("CreatedBy", group.CreatedBy);
            p.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
            
            await _connection.ExecuteAsync(sql, p);
            return p.Get<long>("Id");
        }

        public async Task<bool> UpdateReportGroupAsync(ReportGroup group)
        {
            var sql = $@"
                UPDATE REPORT_GROUPS 
                SET {nameof(ReportGroup.Name)} = :Name, 
                    {nameof(ReportGroup.SortOrder)} = :SortOrder, 
                    {nameof(ReportGroup.Description)} = :Description, 
                    {nameof(ReportGroup.UpdatedAt)} = :UpdatedAt, 
                    {nameof(ReportGroup.UpdatedBy)} = :UpdatedBy 
                WHERE {nameof(ReportGroup.Id)} = :Id";
            
            var rows = await _connection.ExecuteAsync(sql, new
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
            var sql = $"DELETE FROM REPORT_GROUPS WHERE {nameof(ReportGroup.Id)} = :Id";
            var rows = await _connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        // --- DYNAMIC REPORT ---

        public async Task<IEnumerable<DynamicReport>> GetDynamicReportsByGroupIdAsync(long groupId)
        {
            var sql = $"SELECT {nameof(DynamicReport.Id)}, {nameof(DynamicReport.GroupId)}, {nameof(DynamicReport.Name)}, {nameof(DynamicReport.SqlQuery)}, {nameof(DynamicReport.ParametersJson)}, {nameof(DynamicReport.AllowedRoles)}, {nameof(DynamicReport.IsActive)}, {nameof(DynamicReport.CreatedAt)}, {nameof(DynamicReport.CreatedBy)}, {nameof(DynamicReport.UpdatedAt)}, {nameof(DynamicReport.UpdatedBy)} FROM DYNAMIC_REPORTS WHERE {nameof(DynamicReport.GroupId)} = :GroupId ORDER BY {nameof(DynamicReport.Name)}";
            return await _connection.QueryAsync<DynamicReport>(sql, new { GroupId = groupId });
        }

        public async Task<DynamicReport?> GetDynamicReportByIdAsync(long id)
        {
            var sql = $"SELECT {nameof(DynamicReport.Id)}, {nameof(DynamicReport.GroupId)}, {nameof(DynamicReport.Name)}, {nameof(DynamicReport.SqlQuery)}, {nameof(DynamicReport.ParametersJson)}, {nameof(DynamicReport.AllowedRoles)}, {nameof(DynamicReport.IsActive)}, {nameof(DynamicReport.CreatedAt)}, {nameof(DynamicReport.CreatedBy)}, {nameof(DynamicReport.UpdatedAt)}, {nameof(DynamicReport.UpdatedBy)} FROM DYNAMIC_REPORTS WHERE {nameof(DynamicReport.Id)} = :Id";
            return await _connection.QueryFirstOrDefaultAsync<DynamicReport>(sql, new { Id = id });
        }

        public async Task<long> CreateDynamicReportAsync(DynamicReport report)
        {
            var sql = $@"
                INSERT INTO DYNAMIC_REPORTS (
                    {nameof(DynamicReport.GroupId)}, 
                    {nameof(DynamicReport.Name)}, 
                    {nameof(DynamicReport.SqlQuery)}, 
                    {nameof(DynamicReport.ParametersJson)}, 
                    {nameof(DynamicReport.AllowedRoles)}, 
                    {nameof(DynamicReport.IsActive)}, 
                    {nameof(DynamicReport.CreatedBy)}
                ) 
                VALUES (:GroupId, :Name, :SqlQuery, :ParametersJson, :AllowedRoles, :IsActive, :CreatedBy) 
                RETURNING {nameof(DynamicReport.Id)} INTO :Id";
            
            var p = new DynamicParameters();
            p.Add("GroupId", report.GroupId);
            p.Add("Name", report.Name);
            p.Add("SqlQuery", report.SqlQuery);
            p.Add("ParametersJson", report.ParametersJson);
            p.Add("AllowedRoles", report.AllowedRoles);
            p.Add("IsActive", report.IsActive);
            p.Add("CreatedBy", report.CreatedBy);
            p.Add("Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
            
            await _connection.ExecuteAsync(sql, p);
            return p.Get<long>("Id");
        }

        public async Task<bool> UpdateDynamicReportAsync(DynamicReport report)
        {
            var sql = $@"
                UPDATE DYNAMIC_REPORTS 
                SET {nameof(DynamicReport.GroupId)} = :GroupId, 
                    {nameof(DynamicReport.Name)} = :Name, 
                    {nameof(DynamicReport.SqlQuery)} = :SqlQuery, 
                    {nameof(DynamicReport.ParametersJson)} = :ParametersJson, 
                    {nameof(DynamicReport.AllowedRoles)} = :AllowedRoles, 
                    {nameof(DynamicReport.IsActive)} = :IsActive, 
                    {nameof(DynamicReport.UpdatedAt)} = :UpdatedAt, 
                    {nameof(DynamicReport.UpdatedBy)} = :UpdatedBy 
                WHERE {nameof(DynamicReport.Id)} = :Id";
            
            var rows = await _connection.ExecuteAsync(sql, new
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
            var sql = $"DELETE FROM DYNAMIC_REPORTS WHERE {nameof(DynamicReport.Id)} = :Id";
            var rows = await _connection.ExecuteAsync(sql, new { Id = id });
            return rows > 0;
        }

        // --- EXECUTE SQL ---

        public async Task<IEnumerable<IDictionary<string, object>>> ExecuteDynamicQueryAsync(string sql, Dictionary<string, object>? parameters)
        {
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
            
            var result = await _connection.QueryAsync(sql, dynamicParameters);
            
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
