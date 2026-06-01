using Microsoft.AspNetCore.Mvc;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.IdentityService.Controllers
{
    [ApiController]
    [Route("api/v1/audit-logs")]
    public class AuditLogController : ControllerBase
    {
        private readonly ElasticsearchClient _elasticsearchClient;

        public AuditLogController(ElasticsearchClient elasticsearchClient)
        {
            _elasticsearchClient = elasticsearchClient;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
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
                return StatusCode(500, "Failed to query Elasticsearch");
            }

            return Ok(new
            {
                Total = response.Total,
                Logs = response.Documents
            });
        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteAuditLogs([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            // Chỉ SuperAdmin (Role ADMIN) mới được phép soft delete audit log
            var isSuperAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "ADMIN");
            if (!isSuperAdmin)
            {
                return Forbid("Bạn không có quyền thực hiện chức năng này. Chỉ SuperAdmin mới được quyền dọn dẹp nhật ký.");
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            try
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
                            fromDate, toDate, updateResponse.ElasticsearchServerError?.Error.Reason);
                        return StatusCode(500, "Xóa mềm nhật ký từ Elasticsearch thất bại.");
                    }
                }

                return Ok(new { message = $"Đã thực hiện dọn dẹp ẩn danh {count} bản ghi nhật ký hệ thống thành công.", Count = count });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AUDIT PURGE ERROR] Lỗi hệ thống khi soft delete nhật ký.");
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }
    }
}
