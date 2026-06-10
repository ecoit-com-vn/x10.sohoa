using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Serilog;

namespace EvnHanoi.NotificationService.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ElasticsearchClient _elasticsearchClient;

        public AuditLogRepository(ElasticsearchClient elasticsearchClient)
        {
            _elasticsearchClient = elasticsearchClient;
        }

        public async Task<(long Total, IEnumerable<dynamic> Logs)> GetAuditLogsAsync(int page, int pageSize)
        {
            var response = await _elasticsearchClient.SearchAsync<dynamic>(s => s
                .Indices("audit_logs-*")
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Query(q => q
                    .Bool(b => b
                        .MustNot(mn => mn
                            .Term(t => t
                                .Field("isDeleted")
                                .Value(true)
                            )
                        )
                    )
                )
                .Sort(sort => sort.Field("@timestamp", f => f.Order(SortOrder.Desc)))
            );

            if (!response.IsValidResponse)
            {
                throw new Exception("Failed to query Elasticsearch");
            }

            return (response.Total, response.Documents);
        }

        public async Task<IEnumerable<dynamic>> GetRecentAuditLogsAsync(int count)
        {
            // 1. Kiểm tra Ping kết nối đến Elasticsearch
            var pingResponse = await _elasticsearchClient.PingAsync();
            if (!pingResponse.IsValidResponse)
            {
                // Lỗi không thể kết nối tới Elastic server (IP/Port sai hoặc server chết)
                Log.Error("Không thể kết nối đến Elasticsearch: {Error}", pingResponse.ElasticsearchServerError?.Error.Reason);
            }
            // 2. Kiểm tra xem có Index nào khớp với pattern hay không
            var indicesResponse = await _elasticsearchClient.Indices.ExistsAsync("audit_logs-*");
            if (!indicesResponse.Exists)
            {
                // Index chưa được tạo (chưa có bản ghi nào được ghi xuống)
                Log.Warning("Index pattern 'audit_logs-*' chưa tồn tại trên Elasticsearch.");
            }
            var response = await _elasticsearchClient.SearchAsync<dynamic>(s => s
                .Indices("audit_logs-*")
                .From(0)
                .Size(count)
                .Query(q => q
                    .Bool(b => b
                        .MustNot(mn => mn
                            .Term(t => t
                                .Field("isDeleted")
                                .Value(true)
                            )
                        )
                    )
                )
                .Sort(sort => sort.Field("@timestamp", f => f.Order(SortOrder.Desc)))
            );

            if (!response.IsValidResponse)
            {
                throw new Exception("Failed to query Elasticsearch");
            }

            return response.Documents;
        }

        public async Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId)
        {
            var searchResponse = await _elasticsearchClient.CountAsync(c => c
                .Indices("audit_logs-*")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Range(r => r
                                .DateRange(dr => dr
                                    .Field("@timestamp")
                                    .Gte(fromDate.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                                    .Lte(toDate.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                                )
                            )
                        )
                        .MustNot(mn => mn
                            .Term(t => t
                                .Field("isDeleted")
                                .Value(true)
                            )
                        )
                    )
                )
            );

            long count = searchResponse.Count;
            Log.Warning("[AUDIT PURGE] SuperAdmin {Username} (ID: {UserId}) đã yêu cầu Soft Delete {Count} nhật ký thao tác từ {FromDate} đến {ToDate}.", 
                username, userId, count, fromDate, toDate);

            if (count > 0)
            {
                var updateResponse = await _elasticsearchClient.UpdateByQueryAsync<dynamic>(u => u
                    .Indices("audit_logs-*")
                    .Query(q => q
                        .Bool(b => b
                            .Must(m => m
                                .Range(r => r
                                    .DateRange(dr => dr
                                        .Field("@timestamp")
                                        .Gte(fromDate.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                                        .Lte(toDate.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                                    )
                                )
                            )
                            .MustNot(mn => mn
                                .Term(t => t
                                    .Field("isDeleted")
                                    .Value(true)
                                )
                            )
                        )
                    )
                    .Script(sc => sc
                        .Source("ctx._source.isDeleted = true;")
                    )
                );

                if (!updateResponse.IsValidResponse)
                {
                    Log.Error("[AUDIT PURGE ERROR] Soft Delete từ {FromDate} đến {ToDate} thất bại. Lỗi: {Error}", 
                        fromDate, toDate, updateResponse.ElasticsearchServerError?.Error?.Reason);
                    throw new Exception("Xóa mềm nhật ký từ Elasticsearch thất bại.");
                }
            }

            return count;
        }
    }
}
