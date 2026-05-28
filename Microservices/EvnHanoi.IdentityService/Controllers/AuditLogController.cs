using Microsoft.AspNetCore.Mvc;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

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
        public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var response = await _elasticsearchClient.SearchAsync<dynamic>(s => s
                .Indices("audit_logs-*")
                .From((page - 1) * pageSize)
                .Size(pageSize)
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
    }
}
