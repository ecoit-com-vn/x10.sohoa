// Microservices/EvnHanoi.ReportService/Controllers/ReportGroupController.cs
using EvnHanoi.ReportService.Core.DTOs;
using EvnHanoi.ReportService.Core.Entities;
using EvnHanoi.ReportService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace EvnHanoi.ReportService.Controllers
{
    [Authorize]
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
                Code = g.Code,
                Name = g.Name,
                SortOrder = g.SortOrder,
                Description = g.Description,
                IsActive = g.IsActive == 1,
                ReportCount = g.ReportCount,
                UnitCount = g.UnitCount
            });
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var g = await _reportRepository.GetReportGroupByIdAsync(id);
            if (g == null) return NotFound("Không tìm thấy nhóm báo cáo hệ thống");
            
            var dto = new ReportGroupDto
            {
                Id = g.Id,
                Code = g.Code,
                Name = g.Name,
                SortOrder = g.SortOrder,
                Description = g.Description,
                IsActive = g.IsActive == 1,
                ReportCount = g.Reports.Count,
                UnitCount = g.UnitIds.Count,
                ReportIds = g.Reports.Select(r => r.Id).ToList(),
                UnitIds = g.UnitIds,
                Reports = g.Reports.Select(r => new ReportDto
                {
                    Id = r.Id,
                    Code = r.Code,
                    Name = r.Name
                }).ToList()
            };
            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReportGroupCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest("Mã nhóm báo cáo không được để trống");
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Tên nhóm báo cáo không được để trống");

            var username = User.FindFirst("preferred_username")?.Value ?? "SYSTEM";

            var group = new ReportGroup
            {
                Code = dto.Code.Trim(),
                Name = dto.Name.Trim(),
                SortOrder = dto.SortOrder,
                Description = dto.Description?.Trim(),
                CreatedBy = username,
                IsActive = dto.IsActive ? 1 : 0
            };

            var id = await _reportRepository.CreateReportGroupAsync(group, dto.ReportIds, dto.UnitIds);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] ReportGroupUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest("Mã nhóm báo cáo không được để trống");
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Tên nhóm báo cáo không được để trống");

            var group = await _reportRepository.GetReportGroupByIdAsync(id);
            if (group == null) return NotFound("Không tìm thấy nhóm báo cáo hệ thống");

            var username = User.FindFirst("preferred_username")?.Value ?? "SYSTEM";

            group.Code = dto.Code.Trim();
            group.Name = dto.Name.Trim();
            group.SortOrder = dto.SortOrder;
            group.Description = dto.Description?.Trim();
            group.UpdatedBy = username;
            group.IsActive = dto.IsActive ? 1 : 0;
            
            var success = await _reportRepository.UpdateReportGroupAsync(group, dto.ReportIds, dto.UnitIds);
            return success ? Ok(true) : BadRequest("Cập nhật nhóm báo cáo thất bại");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var success = await _reportRepository.DeleteReportGroupAsync(id);
            return success ? Ok(true) : NotFound("Không tìm thấy nhóm báo cáo hoặc xóa thất bại");
        }

        /// <summary>
        /// API lookup danh sách các báo cáo tĩnh cho màn hình cấu hình.
        /// </summary>
        [HttpGet("reports")]
        public async Task<IActionResult> GetSystemReports()
        {
            var reports = await _reportRepository.GetSystemReportsAsync();
            var dtos = reports.Select(r => new ReportDto
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name
            });
            return Ok(dtos);
        }
    }
}
