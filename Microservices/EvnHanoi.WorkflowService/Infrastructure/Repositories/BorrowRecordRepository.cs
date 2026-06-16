using EvnHanoi.WorkflowService.Core.Interfaces;
using EvnHanoi.WorkflowService.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace EvnHanoi.WorkflowService.Infrastructure.Repositories
{
    public class BorrowRecordRepository : IBorrowRecordRepository
    {
        private readonly IDbConnection _connection;

        public BorrowRecordRepository(IDbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<IEnumerable<BorrowRecord>> GetAllAsync()
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT {nameof(BorrowRecord.Id)}, 
                                {nameof(BorrowRecord.DossierId)}, 
                                {nameof(BorrowRecord.RequesterId)}, 
                                {nameof(BorrowRecord.Reason)}, 
                                {nameof(BorrowRecord.State)}, 
                                {nameof(BorrowRecord.RequestDate)}, 
                                {nameof(BorrowRecord.ApprovedDate)}, 
                                {nameof(BorrowRecord.BorrowedDate)}, 
                                {nameof(BorrowRecord.ReturnedDate)},
                                {nameof(BorrowRecord.WorkflowInstanceId)},
                                {nameof(BorrowRecord.WorkflowStatusName)}
                        FROM BORROWRECORDS";
            return await _connection.QueryAsync<BorrowRecord>(sql);
        }

        public async Task<(IEnumerable<BorrowRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? keyword = null, BorrowState? state = null)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            
            var conditions = new List<string>();
            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                conditions.Add("(UPPER(b.DossierId) LIKE UPPER(:Keyword) OR UPPER(b.RequesterId) LIKE UPPER(:Keyword) OR UPPER(b.Reason) LIKE UPPER(:Keyword))");
                parameters.Add("Keyword", $"%{keyword}%");
            }
            if (state.HasValue)
            {
                conditions.Add("b.State = :State");
                parameters.Add("State", state.Value.ToString());
            }
            
            var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            
            var countSql = $"SELECT COUNT(*) FROM BORROWRECORDS b {whereClause}";
            var offset = (page - 1) * pageSize;
            
            var sql = $@"
                SELECT * FROM (
                    SELECT b.{nameof(BorrowRecord.Id)}, 
                           b.{nameof(BorrowRecord.DossierId)}, 
                           b.{nameof(BorrowRecord.RequesterId)}, 
                           b.{nameof(BorrowRecord.Reason)}, 
                           b.{nameof(BorrowRecord.State)}, 
                           b.{nameof(BorrowRecord.RequestDate)}, 
                           b.{nameof(BorrowRecord.ApprovedDate)}, 
                           b.{nameof(BorrowRecord.BorrowedDate)}, 
                           b.{nameof(BorrowRecord.ReturnedDate)},
                           b.{nameof(BorrowRecord.WorkflowInstanceId)},
                           b.{nameof(BorrowRecord.WorkflowStatusName)},
                           ROW_NUMBER() OVER (ORDER BY b.{nameof(BorrowRecord.RequestDate)} DESC) AS RN
                    FROM BORROWRECORDS b
                    {whereClause}
                ) WHERE RN > :Offset AND RN <= :OffsetPlusSize";
                
            parameters.Add("Offset", offset);
            parameters.Add("OffsetPlusSize", offset + pageSize);
            
            var totalCount = await _connection.ExecuteScalarAsync<int>(countSql, parameters);
            var items = await _connection.QueryAsync<BorrowRecord>(sql, parameters);
            
            return (items, totalCount);
        }

        public async Task<BorrowRecord?> GetByIdAsync(Guid id)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            var sql = $@"SELECT {nameof(BorrowRecord.Id)}, 
                                {nameof(BorrowRecord.DossierId)}, 
                                {nameof(BorrowRecord.RequesterId)}, 
                                {nameof(BorrowRecord.Reason)}, 
                                {nameof(BorrowRecord.State)}, 
                                {nameof(BorrowRecord.RequestDate)}, 
                                {nameof(BorrowRecord.ApprovedDate)}, 
                                {nameof(BorrowRecord.BorrowedDate)}, 
                                {nameof(BorrowRecord.ReturnedDate)},
                                {nameof(BorrowRecord.WorkflowInstanceId)},
                                {nameof(BorrowRecord.WorkflowStatusName)}
                        FROM BORROWRECORDS 
                        WHERE {nameof(BorrowRecord.Id)} = :Id";
            return await _connection.QuerySingleOrDefaultAsync<BorrowRecord>(sql, new { Id = id.ToString() });
        }

        public async Task<bool> CreateAsync(BorrowRecord record)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            record.RequestDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            if (record.ApprovedDate.HasValue) record.ApprovedDate = DateTime.SpecifyKind(record.ApprovedDate.Value, DateTimeKind.Unspecified);
            if (record.BorrowedDate.HasValue) record.BorrowedDate = DateTime.SpecifyKind(record.BorrowedDate.Value, DateTimeKind.Unspecified);
            if (record.ReturnedDate.HasValue) record.ReturnedDate = DateTime.SpecifyKind(record.ReturnedDate.Value, DateTimeKind.Unspecified);
            var sql = $@"INSERT INTO BORROWRECORDS (
                            {nameof(BorrowRecord.Id)}, 
                            {nameof(BorrowRecord.DossierId)}, 
                            {nameof(BorrowRecord.RequesterId)}, 
                            {nameof(BorrowRecord.Reason)}, 
                            {nameof(BorrowRecord.State)}, 
                            {nameof(BorrowRecord.RequestDate)}, 
                            {nameof(BorrowRecord.ApprovedDate)}, 
                            {nameof(BorrowRecord.BorrowedDate)}, 
                            {nameof(BorrowRecord.ReturnedDate)},
                            {nameof(BorrowRecord.WorkflowInstanceId)},
                            {nameof(BorrowRecord.WorkflowStatusName)}
                        )
                        VALUES (:Id, :DossierId, :RequesterId, :Reason, :State, :RequestDate, :ApprovedDate, :BorrowedDate, :ReturnedDate, :WorkflowInstanceId, :WorkflowStatusName)";
            var parameters = new DynamicParameters();
            parameters.Add("Id", record.Id.ToString());
            parameters.Add("DossierId", string.IsNullOrEmpty(record.DossierId) ? null : record.DossierId);
            parameters.Add("RequesterId", string.IsNullOrEmpty(record.RequesterId) ? null : record.RequesterId);
            parameters.Add("Reason", string.IsNullOrEmpty(record.Reason) ? null : record.Reason);
            parameters.Add("State", record.State.ToString());
            parameters.Add("RequestDate", record.RequestDate);
            parameters.Add("ApprovedDate", record.ApprovedDate);
            parameters.Add("BorrowedDate", record.BorrowedDate);
            parameters.Add("ReturnedDate", record.ReturnedDate);
            parameters.Add("WorkflowInstanceId", record.WorkflowInstanceId?.ToString());
            parameters.Add("WorkflowStatusName", string.IsNullOrEmpty(record.WorkflowStatusName) ? null : record.WorkflowStatusName);

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        public async Task<bool> UpdateAsync(BorrowRecord record)
        {
            if (_connection.State != ConnectionState.Open) _connection.Open();
            record.RequestDate = DateTime.SpecifyKind(record.RequestDate, DateTimeKind.Unspecified);
            if (record.ApprovedDate.HasValue) record.ApprovedDate = DateTime.SpecifyKind(record.ApprovedDate.Value, DateTimeKind.Unspecified);
            if (record.BorrowedDate.HasValue) record.BorrowedDate = DateTime.SpecifyKind(record.BorrowedDate.Value, DateTimeKind.Unspecified);
            if (record.ReturnedDate.HasValue) record.ReturnedDate = DateTime.SpecifyKind(record.ReturnedDate.Value, DateTimeKind.Unspecified);
            var sql = $@"UPDATE BORROWRECORDS
                        SET {nameof(BorrowRecord.DossierId)} = :DossierId, 
                            {nameof(BorrowRecord.RequesterId)} = :RequesterId, 
                            {nameof(BorrowRecord.Reason)} = :Reason, 
                            {nameof(BorrowRecord.State)} = :State,
                            {nameof(BorrowRecord.RequestDate)} = :RequestDate, 
                            {nameof(BorrowRecord.ApprovedDate)} = :ApprovedDate, 
                            {nameof(BorrowRecord.BorrowedDate)} = :BorrowedDate, 
                            {nameof(BorrowRecord.ReturnedDate)} = :ReturnedDate,
                            {nameof(BorrowRecord.WorkflowInstanceId)} = :WorkflowInstanceId,
                            {nameof(BorrowRecord.WorkflowStatusName)} = :WorkflowStatusName
                        WHERE {nameof(BorrowRecord.Id)} = :Id";
            var parameters = new DynamicParameters();
            parameters.Add("DossierId", string.IsNullOrEmpty(record.DossierId) ? null : record.DossierId);
            parameters.Add("RequesterId", string.IsNullOrEmpty(record.RequesterId) ? null : record.RequesterId);
            parameters.Add("Reason", string.IsNullOrEmpty(record.Reason) ? null : record.Reason);
            parameters.Add("State", record.State.ToString());
            parameters.Add("RequestDate", record.RequestDate);
            parameters.Add("ApprovedDate", record.ApprovedDate);
            parameters.Add("BorrowedDate", record.BorrowedDate);
            parameters.Add("ReturnedDate", record.ReturnedDate);
            parameters.Add("WorkflowInstanceId", record.WorkflowInstanceId?.ToString());
            parameters.Add("WorkflowStatusName", string.IsNullOrEmpty(record.WorkflowStatusName) ? null : record.WorkflowStatusName);
            parameters.Add("Id", record.Id.ToString());

            var affected = await _connection.ExecuteAsync(sql, parameters);
            return affected > 0;
        }
    }
}
