using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IElasticsearchService _elasticsearchService;
    private readonly IMessageProducer _messageProducer;
    private readonly IEquipmentTypeRepository _equipmentTypeRepository;

    public EquipmentController(
        IEquipmentRepository equipmentRepository, 
        IElasticsearchService elasticsearchService, 
        IMessageProducer messageProducer,
        IEquipmentTypeRepository equipmentTypeRepository)
    {
        _equipmentRepository = equipmentRepository;
        _elasticsearchService = elasticsearchService;
        _messageProducer = messageProducer;
        _equipmentTypeRepository = equipmentTypeRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var unitIds = GetAuthorizedUnitIds();
        if (unitIds != null && !unitIds.Any())
        {
            return Ok(new List<Equipment>());
        }

        var equipments = await _equipmentRepository.GetAllAsync(unitIds);
        return Ok(equipments);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("Keyword is required.");

        var unitIds = GetAuthorizedUnitIds();
        if (unitIds != null && !unitIds.Any())
        {
            return Ok(new List<Equipment>());
        }

        var results = await _elasticsearchService.SearchEquipmentsAsync(keyword, unitIds);
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var equipment = await _equipmentRepository.GetByIdAsync(id);
        if (equipment == null)
            return NotFound();

        var unitIds = GetAuthorizedUnitIds();
        if (unitIds != null && (!equipment.UnitId.HasValue || !unitIds.Contains(equipment.UnitId.Value)))
        {
            return Forbid();
        }

        var attributes = await _equipmentRepository.GetAttributesAsync(id);
        
        var dto = new EquipmentDto
        {
            Id = equipment.Id,
            EquipmentTypeId = equipment.EquipmentTypeId,
            Name = equipment.Name,
            Code = equipment.Code,
            SerialNumber = equipment.SerialNumber,
            CreatedAt = equipment.CreatedAt,
            CreatedBy = equipment.CreatedBy,
            UnitId = equipment.UnitId,
            DynamicAttributes = attributes.ToDictionary(a => a.AttributeDefinitionId, a => a.Value)
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EquipmentCreateDto dto)
    {
        var unitIds = GetAuthorizedUnitIds();
        if (unitIds != null)
        {
            if (!dto.UnitId.HasValue || !unitIds.Contains(dto.UnitId.Value))
            {
                return BadRequest("Bạn không có quyền quản lý dữ liệu của đơn vị được chọn.");
            }
        }

        var equipmentId = Guid.NewGuid();
        var equipment = new Equipment
        {
            Id = equipmentId,
            EquipmentTypeId = dto.EquipmentTypeId,
            Name = dto.Name,
            Code = dto.Code,
            SerialNumber = dto.SerialNumber,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = dto.CreatedBy,
            UnitId = dto.UnitId
        };

        var attributes = dto.DynamicAttributes.Select(kvp => new AttributeValue
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            AttributeDefinitionId = kvp.Key,
            Value = kvp.Value
        }).ToList();

        var result = await _equipmentRepository.CreateWithAttributesAsync(equipment, attributes);
        if (result)
        {
            // Publish message to RabbitMQ for SyncService
            var syncMessage = new
            {
                Id = equipment.Id,
                EquipmentTypeId = equipment.EquipmentTypeId,
                Name = equipment.Name,
                Code = equipment.Code,
                SerialNumber = equipment.SerialNumber,
                CreatedAt = equipment.CreatedAt,
                CreatedBy = equipment.CreatedBy,
                UnitId = equipment.UnitId,
                DynamicAttributes = dto.DynamicAttributes
            };
            await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");

            return CreatedAtAction(nameof(GetById), new { id = equipmentId }, equipment);
        }

        return BadRequest("Failed to create equipment.");
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EquipmentUpdateDto dto)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var unitIds = GetAuthorizedUnitIds();
        if (unitIds != null)
        {
            if (!existing.UnitId.HasValue || !unitIds.Contains(existing.UnitId.Value))
            {
                return Forbid();
            }
            if (!dto.UnitId.HasValue || !unitIds.Contains(dto.UnitId.Value))
            {
                return BadRequest("Bạn không có quyền quản lý dữ liệu của đơn vị mới được chọn.");
            }
        }

        existing.EquipmentTypeId = dto.EquipmentTypeId;
        existing.Name = dto.Name;
        existing.Code = dto.Code;
        existing.SerialNumber = dto.SerialNumber;
        existing.UnitId = dto.UnitId;

        var updateBase = await _equipmentRepository.UpdateAsync(existing);
        
        var attributes = dto.DynamicAttributes.Select(kvp => new AttributeValue
        {
            Id = Guid.NewGuid(),
            EquipmentId = id,
            AttributeDefinitionId = kvp.Key,
            Value = kvp.Value
        }).ToList();

        var updateAttributes = await _equipmentRepository.UpdateAttributesAsync(id, attributes);

        if (updateBase && updateAttributes)
            return NoContent();

        return BadRequest("Failed to update equipment.");
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var unitIds = GetAuthorizedUnitIds();
        if (unitIds != null && (!existing.UnitId.HasValue || !unitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        await _equipmentRepository.UpdateAttributesAsync(id, new List<AttributeValue>());
        
        var result = await _equipmentRepository.DeleteAsync(id);
        if (result)
            return NoContent();

        return BadRequest("Failed to delete equipment.");
    }

    [HttpGet("import-template/{equipmentTypeId}")]
    public async Task<IActionResult> GetImportTemplate(Guid equipmentTypeId)
    {
        var type = await _equipmentTypeRepository.GetByIdAsync(equipmentTypeId);
        if (type == null) return NotFound("Equipment type not found.");

        var attributes = await _equipmentTypeRepository.GetAttributeDefinitionsAsync(equipmentTypeId);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Import Template");

        // Set Headers
        worksheet.Cell(1, 1).Value = "Mã thiết bị (Code) *";
        worksheet.Cell(1, 2).Value = "Tên thiết bị (Name) *";
        worksheet.Cell(1, 3).Value = "Số Serial (SerialNumber)";

        var colIndex = 4;
        foreach (var attr in attributes)
        {
            var header = $"{attr.Name} ({attr.Code})";
            if (attr.IsRequired)
            {
                header += " *";
            }
            worksheet.Cell(1, colIndex).Value = header;
            colIndex++;
        }

        // Style header row
        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Import_Template_{type.Code}.xlsx");
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportEquipment([FromQuery] Guid equipmentTypeId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var type = await _equipmentTypeRepository.GetByIdAsync(equipmentTypeId);
        if (type == null) return BadRequest("Invalid Equipment Type.");

        var attributeDefs = (await _equipmentTypeRepository.GetAttributeDefinitionsAsync(equipmentTypeId)).ToList();

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return BadRequest("Workbook is empty.");

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 3;

        // Parse headers from row 1
        var attributeColMap = new Dictionary<int, AttributeDefinition>();
        for (int col = 4; col <= lastCol; col++)
        {
            var headerVal = worksheet.Cell(1, col).GetString();
            if (string.IsNullOrEmpty(headerVal)) continue;

            // Extract code inside brackets, e.g. "Tên thuộc tính (Mã thuộc tính)"
            var match = Regex.Match(headerVal, @"\(([^)]+)\)");
            if (match.Success)
            {
                var code = match.Groups[1].Value.Trim();
                var def = attributeDefs.FirstOrDefault(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (def != null)
                {
                    attributeColMap.Add(col, def);
                }
            }
        }

        var successCount = 0;
        var errors = new List<string>();

        for (int row = 2; row <= lastRow; row++)
        {
            var code = worksheet.Cell(row, 1).GetString()?.Trim();
            var name = worksheet.Cell(row, 2).GetString()?.Trim();
            var serialNumber = worksheet.Cell(row, 3).GetString()?.Trim();

            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name))
            {
                continue; // Bỏ qua dòng trống
            }

            if (string.IsNullOrEmpty(code))
            {
                errors.Add($"Dòng {row}: Mã thiết bị không được để trống.");
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                errors.Add($"Dòng {row}: Tên thiết bị không được để trống.");
                continue;
            }

            var equipmentId = Guid.NewGuid();
            var equipment = new Equipment
            {
                Id = equipmentId,
                EquipmentTypeId = equipmentTypeId,
                Name = name,
                Code = code,
                SerialNumber = serialNumber,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System",
                UnitId = null
            };

            var unitIds = GetAuthorizedUnitIds();
            if (unitIds != null && unitIds.Any())
            {
                equipment.UnitId = unitIds.First();
            }

            var dynamicAttributes = new Dictionary<Guid, string>();
            var attributeValues = new List<AttributeValue>();
            var rowHasError = false;

            // Read EAV values
            foreach (var col in attributeColMap.Keys)
            {
                var def = attributeColMap[col];
                var cellVal = worksheet.Cell(row, col).GetString()?.Trim();

                if (def.IsRequired && string.IsNullOrEmpty(cellVal))
                {
                    errors.Add($"Dòng {row}: Thuộc tính '{def.Name}' là bắt buộc.");
                    rowHasError = true;
                    break;
                }

                if (!string.IsNullOrEmpty(cellVal))
                {
                    attributeValues.Add(new AttributeValue
                    {
                        Id = Guid.NewGuid(),
                        EquipmentId = equipmentId,
                        AttributeDefinitionId = def.Id,
                        Value = cellVal
                    });

                    // Add to dictionary for elastic sync
                    dynamicAttributes.Add(def.Id, cellVal);
                }
            }

            if (rowHasError) continue;

            try
            {
                var result = await _equipmentRepository.CreateWithAttributesAsync(equipment, attributeValues);
                if (result)
                {
                    successCount++;

                    // Sync to Elasticsearch
                    var syncMessage = new
                    {
                        Id = equipment.Id,
                        EquipmentTypeId = equipment.EquipmentTypeId,
                        Name = equipment.Name,
                        Code = equipment.Code,
                        SerialNumber = equipment.SerialNumber,
                        CreatedAt = equipment.CreatedAt,
                        CreatedBy = equipment.CreatedBy,
                        UnitId = equipment.UnitId,
                        DynamicAttributes = dynamicAttributes
                    };
                    await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
                }
                else
                {
                    errors.Add($"Dòng {row}: Không thể tạo thiết bị vào cơ sở dữ liệu.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Dòng {row}: Lỗi khi lưu vào DB ({ex.Message}).");
            }
        }

        return Ok(new
        {
            Message = $"Nhập dữ liệu hoàn tất. Thành công: {successCount} dòng.",
            SuccessCount = successCount,
            Errors = errors
        });
    }

    private List<long>? GetAuthorizedUnitIds()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
        {
            return null;
        }

        var unitRolesClaim = User.FindFirst("unit_roles")?.Value;
        if (string.IsNullOrEmpty(unitRolesClaim))
        {
            return new List<long>();
        }

        try
        {
            var unitRoles = JsonSerializer.Deserialize<List<UnitRoleDto>>(unitRolesClaim, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return unitRoles?.Select(ur => ur.UnitId).Distinct().ToList() ?? new List<long>();
        }
        catch
        {
            return new List<long>();
        }
    }

    public class UnitRoleDto
    {
        public long UnitId { get; set; }
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
    }
}

