using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using EvnHanoi.Infrastructure.Audit;
using EvnHanoi.NotificationService.Models;
using Serilog;

namespace EvnHanoi.NotificationService.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private const int ExportPageSize = 1000;
        private readonly ElasticsearchClient _elasticsearchClient;

        public AuditLogRepository(ElasticsearchClient elasticsearchClient)
        {
            _elasticsearchClient = elasticsearchClient;
        }

        public async Task<(long Total, IReadOnlyList<AuditLogItemDto> Logs)> GetAuditLogsAsync(
            int page,
            int pageSize,
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null)
        {
            var response = await SearchAsync(page, pageSize, keyword, action, resourceType, serviceName, userName, fromDate, toDate, logGroup, unitIds);

            if (!response.IsValidResponse)
            {
                Log.Error(
                    "Query audit_logs thất bại: {Error}",
                    response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);
                throw new Exception("Failed to query Elasticsearch audit logs");
            }

            return (response.Total, response.Hits.Select(MapHit).ToList());
        }

        public async Task<IReadOnlyList<AuditLogItemDto>> GetRecentAuditLogsAsync(int count)
        {
            var pingResponse = await _elasticsearchClient.PingAsync();
            if (!pingResponse.IsValidResponse)
            {
                Log.Error(
                    "Không thể kết nối đến Elasticsearch: {Error}",
                    pingResponse.ElasticsearchServerError?.Error.Reason);
            }

            var indicesResponse = await _elasticsearchClient.Indices.ExistsAsync($"{AuditMessaging.IndexPrefix}-*");
            if (!indicesResponse.Exists)
                Log.Warning("Index pattern '{IndexPrefix}-*' chưa tồn tại trên Elasticsearch.", AuditMessaging.IndexPrefix);

            var response = await SearchAsync(1, count, null, null, null, null, null, null, null, null, null);

            if (!response.IsValidResponse)
                throw new Exception("Failed to query Elasticsearch");

            return response.Hits.Select(MapHit).ToList();
        }

        public async Task<IReadOnlyList<AuditLogItemDto>> ExportAuditLogsAsync(
            string? keyword = null,
            string? action = null,
            string? resourceType = null,
            string? serviceName = null,
            string? userName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int maxRows = 50000,
            string? logGroup = null,
            IReadOnlyList<string>? unitIds = null)
        {
            var all = new List<AuditLogItemDto>();
            var page = 1;

            while (all.Count < maxRows)
            {
                var take = Math.Min(ExportPageSize, maxRows - all.Count);
                var response = await SearchAsync(page, take, keyword, action, resourceType, serviceName, userName, fromDate, toDate, logGroup, unitIds);
                if (!response.IsValidResponse)
                    throw new Exception("Failed to export audit logs from Elasticsearch");

                var batch = response.Hits.Select(MapHit).ToList();
                if (batch.Count == 0)
                    break;

                all.AddRange(batch);
                if (batch.Count < take || all.Count >= response.Total)
                    break;

                page++;
            }

            return all;
        }

        public async Task<long> DeleteAuditLogsAsync(DateTime fromDate, DateTime toDate, string? username, string? userId)
        {
            var searchResponse = await _elasticsearchClient.CountAsync(c => c
                .Indices($"{AuditMessaging.IndexPrefix}-*")
                .Query(q => q.Bool(b =>
                {
                    b.Must(m => m.Range(r => r.DateRange(dr => dr
                        .Field("occurredAt")
                        .Gte(fromDate.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                        .Lte(toDate.ToString("yyyy-MM-ddTHH:mm:ssZ")))));
                    b.MustNot(mn => mn.Term(t => t.Field("isDeleted").Value(true)));
                })));

            long count = searchResponse.Count;
            Log.Warning(
                "[AUDIT PURGE] SuperAdmin {Username} (ID: {UserId}) đã yêu cầu Soft Delete {Count} nhật ký từ {FromDate} đến {ToDate}.",
                username, userId, count, fromDate, toDate);

            if (count > 0)
            {
                var updateResponse = await _elasticsearchClient.UpdateByQueryAsync<AuditLogDocument>(u => u
                    .Indices($"{AuditMessaging.IndexPrefix}-*")
                    .Query(q => q.Bool(b =>
                    {
                        b.Must(m => m.Range(r => r.DateRange(dr => dr
                            .Field("occurredAt")
                            .Gte(fromDate.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                            .Lte(toDate.ToString("yyyy-MM-ddTHH:mm:ssZ")))));
                        b.MustNot(mn => mn.Term(t => t.Field("isDeleted").Value(true)));
                    }))
                    .Script(sc => sc.Source("ctx._source.isDeleted = true;")));

                if (!updateResponse.IsValidResponse)
                {
                    Log.Error(
                        "[AUDIT PURGE ERROR] Soft Delete từ {FromDate} đến {ToDate} thất bại. Lỗi: {Error}",
                        fromDate, toDate, updateResponse.ElasticsearchServerError?.Error?.Reason);
                    throw new Exception("Xóa mềm nhật ký từ Elasticsearch thất bại.");
                }
            }

            return count;
        }

        public async Task<long> DeleteAuditLogsByIdsAsync(IReadOnlyList<string> ids, string? username, string? userId)
        {
            if (ids is null || ids.Count == 0)
                return 0;

            var distinctIds = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctIds.Count == 0)
                return 0;

            const int maxBatch = 500;
            if (distinctIds.Count > maxBatch)
                throw new ArgumentException($"Chỉ được xóa tối đa {maxBatch} nhật ký mỗi lần.");

            var mustQueries = new List<Query>
            {
                BuildIdsQuery(distinctIds)
            };
            var mustNotQueries = new List<Query>
            {
                new QueryDescriptor<AuditLogDocument>().Term(term => term
                    .Field("isDeleted")
                    .Value(true))
            };

            var countResponse = await _elasticsearchClient.CountAsync(c => c
                .Indices($"{AuditMessaging.IndexPrefix}-*")
                .Query(q => q.Bool(b =>
                {
                    b.Must(mustQueries);
                    b.MustNot(mustNotQueries);
                })));

            if (!countResponse.IsValidResponse)
            {
                Log.Error(
                    "Đếm nhật ký cần xóa theo ID thất bại: {Error}",
                    countResponse.ElasticsearchServerError?.Error?.Reason ?? countResponse.DebugInformation);
                throw new Exception("Không thể xác định số lượng nhật ký cần xóa.");
            }

            long count = countResponse.Count;
            Log.Warning(
                "[AUDIT PURGE] User {Username} (ID: {UserId}) đã yêu cầu Soft Delete {Count} nhật ký theo danh sách ID.",
                username, userId, count);

            if (count > 0)
            {
                var updateResponse = await _elasticsearchClient.UpdateByQueryAsync<AuditLogDocument>(u => u
                    .Indices($"{AuditMessaging.IndexPrefix}-*")
                    .Query(q => q.Bool(b =>
                    {
                        b.Must(mustQueries);
                        b.MustNot(mustNotQueries);
                    }))
                    .Script(sc => sc.Source("ctx._source.isDeleted = true;")));

                if (!updateResponse.IsValidResponse)
                {
                    Log.Error(
                        "[AUDIT PURGE ERROR] Soft Delete theo ID thất bại. Lỗi: {Error}",
                        updateResponse.ElasticsearchServerError?.Error?.Reason ?? updateResponse.DebugInformation);
                    throw new Exception("Xóa mềm nhật ký đã chọn từ Elasticsearch thất bại.");
                }
            }

            return count;
        }

        private Task<SearchResponse<AuditLogDocument>> SearchAsync(
            int page,
            int pageSize,
            string? keyword,
            string? action,
            string? resourceType,
            string? serviceName,
            string? userName,
            DateTime? fromDate,
            DateTime? toDate,
            string? logGroup,
            IReadOnlyList<string>? unitIds)
        {
            return _elasticsearchClient.SearchAsync<AuditLogDocument>(s => s
                .Indices($"{AuditMessaging.IndexPrefix}-*")
                .TrackTotalHits(true)
                .From((page - 1) * pageSize)
                .Size(pageSize)
                .Query(q => q.Bool(b =>
                {
                    var mustQueries = new List<Query>();
                    var mustNotQueries = new List<Query>
                    {
                        new QueryDescriptor<AuditLogDocument>().Term(t => t
                            .Field("isDeleted")
                            .Value(true))
                    };

                    foreach (var actorId in AuditUserActionGuard.NonUserActorIds)
                    {
                        var value = actorId;
                        mustNotQueries.Add(new QueryDescriptor<AuditLogDocument>().Wildcard(w => w
                            .Field("actorUserId")
                            .Value(value)
                            .CaseInsensitive(true)));
                    }

                    if (!string.IsNullOrWhiteSpace(keyword))
                        mustQueries.Add(BuildKeywordQuery(keyword));

                    if (!string.IsNullOrWhiteSpace(action))
                        mustQueries.Add(BuildExactFieldQuery("action", action));

                    if (!string.IsNullOrWhiteSpace(resourceType))
                        mustQueries.Add(BuildContainsFieldQuery("resourceType", resourceType));

                    if (!string.IsNullOrWhiteSpace(serviceName))
                        mustQueries.Add(BuildContainsFieldQuery("serviceName", serviceName));

                    if (!string.IsNullOrWhiteSpace(userName))
                        mustQueries.Add(BuildContainsFieldQuery("userName", userName));

                    if (!string.IsNullOrWhiteSpace(logGroup))
                        mustQueries.Add(BuildExactFieldQuery("logGroup", logGroup));

                    var unitIdList = unitIds?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
                    if (unitIdList is { Count: > 0 })
                    {
                        mustQueries.Add(new QueryDescriptor<AuditLogDocument>().Terms(t => t
                            .Field("actorUnitId")
                            .Terms(new TermsQueryField(unitIdList.Select(FieldValue.String).ToArray()))));
                    }

                    if (fromDate.HasValue || toDate.HasValue)
                    {
                        mustQueries.Add(new QueryDescriptor<AuditLogDocument>().Range(r => r.DateRange(dr =>
                        {
                            dr.Field("occurredAt");
                            if (fromDate.HasValue)
                                dr.Gte(FormatEsDate(fromDate.Value));
                            if (toDate.HasValue)
                                dr.Lte(FormatEsDate(toDate.Value));
                        })));
                    }

                    if (mustQueries.Count > 0)
                        b.Must(mustQueries);
                    if (mustNotQueries.Count > 0)
                        b.MustNot(mustNotQueries);
                }))
                .Sort(sort => sort.Field(f => f.OccurredAt, fs => fs.Order(SortOrder.Desc))));
        }

        private static Query BuildKeywordQuery(string keyword)
        {
            var escaped = EscapeWildcard(keyword);
            var pattern = $"*{escaped}*";
            return new QueryDescriptor<AuditLogDocument>().Bool(inner => inner
                .MinimumShouldMatch(1)
                .Should(
                    sh => sh.Wildcard(w => w.Field("action").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("userName").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("details").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("resourceName").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("resourceType").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("serviceName").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("requestPath").Value(pattern).CaseInsensitive(true)),
                    sh => sh.Wildcard(w => w.Field("resourceId").Value(pattern).CaseInsensitive(true))));
        }

        private static Query BuildExactFieldQuery(string field, string value)
        {
            var escaped = EscapeWildcard(value.Trim());
            return new QueryDescriptor<AuditLogDocument>().Wildcard(w => w
                .Field(field)
                .Value(escaped)
                .CaseInsensitive(true));
        }

        private static Query BuildContainsFieldQuery(string field, string value)
        {
            var escaped = EscapeWildcard(value.Trim());
            return new QueryDescriptor<AuditLogDocument>().Wildcard(w => w
                .Field(field)
                .Value($"*{escaped}*")
                .CaseInsensitive(true));
        }

        private static string EscapeWildcard(string input) =>
            input.Replace("\\", "\\\\").Replace("*", "\\*").Replace("?", "\\?");

        private static string FormatEsDate(DateTime value) =>
            DateTime.SpecifyKind(value, DateTimeKind.Utc).ToUniversalTime().ToString("O");

        private static Query BuildIdsQuery(IReadOnlyList<string> ids)
        {
            var idValues = new Ids(ids);
            var keywordValues = new TermsQueryField(ids.Select(FieldValue.String).ToArray());
            return new QueryDescriptor<AuditLogDocument>().Bool(b => b
                .MinimumShouldMatch(1)
                .Should(
                    sh => sh.Ids(i => i.Values(idValues)),
                    sh => sh.Terms(t => t
                        .Field("id.keyword")
                        .Terms(keywordValues)),
                    sh => sh.Terms(t => t
                        .Field("Id.keyword")
                        .Terms(keywordValues))));
        }

        private static AuditLogItemDto MapHit(Hit<AuditLogDocument> hit)
        {
            var doc = hit.Source ?? new AuditLogDocument();
            return MapToDto(doc, hit.Id);
        }

        private static AuditLogItemDto MapToDto(AuditLogDocument doc, string? documentId = null)
        {
            return new AuditLogItemDto
            {
                Id = !string.IsNullOrWhiteSpace(documentId) ? documentId : doc.Id,
                Action = doc.Action,
                UserName = doc.UserName,
                Timestamp = doc.OccurredAt == default ? DateTime.UtcNow : doc.OccurredAt,
                Details = doc.Details,
                ResourceType = doc.ResourceType,
                ResourceId = doc.ResourceId,
                ResourceName = doc.ResourceName,
                ServiceName = doc.ServiceName,
                StatusCode = doc.StatusCode,
                HttpMethod = doc.HttpMethod,
                RequestPath = doc.RequestPath,
                LogGroup = doc.LogGroup,
                ActorUnitId = doc.ActorUnitId,
                ActorUnitName = doc.ActorUnitName,
                ActorFullName = doc.ActorFullName
            };
        }
    }
}
