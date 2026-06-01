// Microservices/EvnHanoi.ReportService/Controllers/ReportGroupController.cs
using EvnHanoi.ReportService.Core.DTOs;
using EvnHanoi.ReportService.Core.Entities;
using EvnHanoi.ReportService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Controllers
{
    [ApiController]
    [Route("api/v1/report-groups")]
    public class ReportGroupController : ControllerBase
    {
        private readonly IReportRepository _reportRepository;

        public ReportGroupController(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var groups = await _reportRepository.GetReportGroupsAsync();
            var dtos = groups.Select(g => new ReportGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                SortOrder = g.SortOrder,
                Description = g.Description,
                DynamicReports = g.DynamicReports.Select(r => new DynamicReportDto
                {
                    Id = r.Id,
                    GroupId = r.GroupId,
                    Name = r.Name,
                    SqlQuery = r.SqlQuery,
                    ParametersJson = r.ParametersJson,
                    AllowedRoles = r.AllowedRoles,
                    IsActive = r.IsActive == 1
                }).ToList()
            });
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var g = await _reportRepository.GetReportGroupByIdAsync(id);
            if (g == null) return NotFound("Không tìm thấy nhóm báo cáo");
            
            var dto = new ReportGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                SortOrder = g.SortOrder,
                Description = g.Description,
                DynamicReports = g.DynamicReports.Select(r => new DynamicReportDto
                {
                    Id = r.Id,
                    GroupId = r.GroupId,
                    Name = r.Name,
                    SqlQuery = r.SqlQuery,
                    ParametersJson = r.ParametersJson,
                    AllowedRoles = r.AllowedRoles,
                    IsActive = r.IsActive == 1
                }).ToList()
            };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReportGroupCreateDto dto)
        {
            var group = new ReportGroup
            {
                Name = dto.Name,
                SortOrder = dto.SortOrder,
                Description = dto.Description,
                CreatedBy = "Admin"
            };
            var id = await _reportRepository.CreateReportGroupAsync(group);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] ReportGroupUpdateDto dto)
        {
            var group = await _reportRepository.GetReportGroupByIdAsync(id);
            if (group == null) return NotFound("Không tìm thấy nhóm báo cáo");
            
            group.Name = dto.Name;
            group.SortOrder = dto.SortOrder;
            group.Description = dto.Description;
            group.UpdatedBy = "Admin";
            
            var success = await _reportRepository.UpdateReportGroupAsync(group);
            return success ? Ok(true) : BadRequest("Cập nhật thất bại");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _reportRepository.DeleteReportGroupAsync(id);
            return success ? Ok(true) : NotFound("Không tìm thấy nhóm báo cáo hoặc xóa thất bại");
        }
    }
}
