// Microservices/EvnHanoi.ReportService/Controllers/DynamicReportController.cs
using EvnHanoi.ReportService.Core.DTOs;
using EvnHanoi.ReportService.Core.Entities;
using EvnHanoi.ReportService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace EvnHanoi.ReportService.Controllers
{
    [ApiController]
    [Route("api/v1/dynamic-reports")]
    public class DynamicReportController : ControllerBase
    {
        private readonly IReportRepository _reportRepository;

        public DynamicReportController(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository;
        }

        [HttpGet("group/{groupId}")]
        public async Task<IActionResult> GetByGroupId(long groupId)
        {
            var reports = await _reportRepository.GetDynamicReportsByGroupIdAsync(groupId);
            var dtos = reports.Select(r => new DynamicReportDto
            {
                Id = r.Id,
                GroupId = r.GroupId,
                Name = r.Name,
                SqlQuery = r.SqlQuery,
                ParametersJson = r.ParametersJson,
                AllowedRoles = r.AllowedRoles,
                IsActive = r.IsActive == 1
            });
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var r = await _reportRepository.GetDynamicReportByIdAsync(id);
            if (r == null) return NotFound("Không tìm thấy báo cáo động");
            
            var dto = new DynamicReportDto
            {
                Id = r.Id,
                GroupId = r.GroupId,
                Name = r.Name,
                SqlQuery = r.SqlQuery,
                ParametersJson = r.ParametersJson,
                AllowedRoles = r.AllowedRoles,
                IsActive = r.IsActive == 1
            };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DynamicReportCreateDto dto)
        {
            var report = new DynamicReport
            {
                GroupId = dto.GroupId,
                Name = dto.Name,
                SqlQuery = dto.SqlQuery,
                ParametersJson = dto.ParametersJson,
                AllowedRoles = dto.AllowedRoles,
                IsActive = dto.IsActive ? 1 : 0,
                CreatedBy = "Admin"
            };
            var id = await _reportRepository.CreateDynamicReportAsync(report);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] DynamicReportUpdateDto dto)
        {
            var report = await _reportRepository.GetDynamicReportByIdAsync(id);
            if (report == null) return NotFound("Không tìm thấy báo cáo động");
            
            report.GroupId = dto.GroupId;
            report.Name = dto.Name;
            report.SqlQuery = dto.SqlQuery;
            report.ParametersJson = dto.ParametersJson;
            report.AllowedRoles = dto.AllowedRoles;
            report.IsActive = dto.IsActive ? 1 : 0;
            report.UpdatedBy = "Admin";
            
            var success = await _reportRepository.UpdateDynamicReportAsync(report);
            return success ? Ok(true) : BadRequest("Cập nhật thất bại");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _reportRepository.DeleteDynamicReportAsync(id);
            return success ? Ok(true) : NotFound("Không tìm thấy báo cáo động hoặc xóa thất bại");
        }
    }
}
