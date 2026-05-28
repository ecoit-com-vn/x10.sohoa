using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EvnHanoi.EquipmentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IElasticsearchService _elasticsearchService;
    private readonly IMessageProducer _messageProducer;

    public EquipmentController(IEquipmentRepository equipmentRepository, IElasticsearchService elasticsearchService, IMessageProducer messageProducer)
    {
        _equipmentRepository = equipmentRepository;
        _elasticsearchService = elasticsearchService;
        _messageProducer = messageProducer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var equipments = await _equipmentRepository.GetAllAsync();
        return Ok(equipments);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("Keyword is required.");

        var results = await _elasticsearchService.SearchEquipmentsAsync(keyword);
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var equipment = await _equipmentRepository.GetByIdAsync(id);
        if (equipment == null)
            return NotFound();

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
            DynamicAttributes = attributes.ToDictionary(a => a.AttributeDefinitionId, a => a.Value)
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EquipmentCreateDto dto)
    {
        var equipmentId = Guid.NewGuid();
        var equipment = new Equipment
        {
            Id = equipmentId,
            EquipmentTypeId = dto.EquipmentTypeId,
            Name = dto.Name,
            Code = dto.Code,
            SerialNumber = dto.SerialNumber,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = dto.CreatedBy
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
                DynamicAttributes = dto.DynamicAttributes
            };
            _messageProducer.SendMessage(syncMessage, "equipment_sync_queue");

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

        existing.EquipmentTypeId = dto.EquipmentTypeId;
        existing.Name = dto.Name;
        existing.Code = dto.Code;
        existing.SerialNumber = dto.SerialNumber;

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
        // First, check if equipment exists
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        // Delete attributes first to avoid foreign key constraints (if any)
        // Since we didn't add a specific DeleteAttributesAsync method, and deleting equipment might fail if no cascade delete.
        // Wait, the requirement says we should handle transaction on Insert. What about delete? I'll assume we delete attributes then equipment.
        await _equipmentRepository.UpdateAttributesAsync(id, new List<AttributeValue>()); // This deletes all attributes
        
        var result = await _equipmentRepository.DeleteAsync(id);
        if (result)
            return NoContent();

        return BadRequest("Failed to delete equipment.");
    }
}
