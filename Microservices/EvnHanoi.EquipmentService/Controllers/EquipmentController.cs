using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public partial class EquipmentController : ControllerBase
{
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IEquipmentTypeRepository _equipmentTypeRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IDocumentDigitizationService _documentDigitizationService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDossierRepository _dossierRepository;
    private readonly IFileDownloadTokenService _downloadTokenService;
    private readonly IFileStorageService _fileStorageService;

    public EquipmentController(
        IEquipmentRepository equipmentRepository, 
        IEquipmentTypeRepository equipmentTypeRepository,
        IMessageProducer messageProducer,
        IDocumentDigitizationService documentDigitizationService,
        IDocumentRepository documentRepository,
        IDossierRepository dossierRepository,
        IFileDownloadTokenService downloadTokenService,
        IFileStorageService fileStorageService)
    {
        _equipmentRepository = equipmentRepository;
        _equipmentTypeRepository = equipmentTypeRepository;
        _messageProducer = messageProducer;
        _documentDigitizationService = documentDigitizationService;
        _documentRepository = documentRepository;
        _dossierRepository = dossierRepository;
        _downloadTokenService = downloadTokenService;
        _fileStorageService = fileStorageService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? code = null,
        [FromQuery] string? name = null,
        [FromQuery] long? unitId = null,
        [FromQuery] Guid? infrastructureId = null,
        [FromQuery] int? gridTypeId = null,
        [FromQuery] Guid? equipmentTypeId = null,
        [FromQuery] bool? isActive = null)
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null)
        {
            if (unitId.HasValue && !allowedUnitIds.Contains(unitId.Value))
            {
                return Ok(new { items = new List<EquipmentDto>(), totalCount = 0, page, pageSize });
            }
        }

        var (items, totalCount) = await _equipmentRepository.GetPagedAsync(
            page, 
            pageSize, 
            keyword,
            code, 
            name, 
            unitId, 
            infrastructureId, 
            gridTypeId, 
            equipmentTypeId,
            isActive,
            allowedUnitIds);

        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("check-code")]
    public async Task<IActionResult> CheckCode(
        [FromQuery] string code,
        [FromQuery] Guid? infrastructureId,
        [FromQuery] Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Mã thiết bị không được để trống." });

        var existing = await _equipmentRepository.GetByCodeAsync(code, infrastructureId);
        var exists = existing != null && (!excludeId.HasValue || existing.Id != excludeId.Value);
        return Ok(new { exists });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(id);
        if (dto == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
        {
            return Forbid();
        }

        return Ok(dto);
    }
    [HttpPost("{id}/copy-byid")]
    public async Task<IActionResult> CreateFromById(Guid id, Guid InfrastructureId, string? note)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(id);
        if (dto == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
        {
            return Forbid();
        }

        if (InfrastructureId == Guid.Empty)
            return BadRequest(new { message = "Trạm không hợp lệ." });

        // Trạm/đường dây đích khi chuyển thiết bị có thể thuộc đơn vị khác với đơn vị của thiết bị nguồn
        // (chuyển sang đơn vị khác là mục đích chính của chức năng này), nên không giới hạn theo
        // allowedUnitIds ở đây — chỉ cần kiểm tra trạm đó tồn tại và đang hoạt động.
        var infrastructures = await _equipmentRepository.GetInfrastructuresLookupAsync(null);
        var targetInfrastructure = infrastructures.FirstOrDefault(infrastructure => infrastructure.Id == InfrastructureId);
        if (targetInfrastructure == null)
            return BadRequest(new { message = "Trạm được chọn không tồn tại hoặc bạn không có quyền sử dụng." });

        var sourceEquipment = await _equipmentRepository.GetByIdAsync(id);
        if (sourceEquipment == null)
            return NotFound();

        if (sourceEquipment.InfrastructureId == InfrastructureId)
        {
            return BadRequest(new
            {
                message = "TBA đang trùng với TBA của thiết bị hiện tại. Vui lòng chọn TBA khác."
            });
        }

        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var replacementEquipment = new Equipment
        {
            Id = Guid.NewGuid(),
            EquipmentTypeId = dto.EquipmentTypeId,
            Name = dto.Name,
            Code = dto.Code,
            SerialNumber = sourceEquipment.SerialNumber,
            InfrastructureId = InfrastructureId,
            ManufactureYear = dto.ManufactureYear,
            EquipmentStatusId = dto.EquipmentStatusId,
            IsActive = true,
            // Đơn vị quản lý của thiết bị mới phải theo đúng Trạm/Đường dây đích vừa chọn (mục đích
            // chính của "Chuyển thiết bị" là chuyển sang đơn vị khác) — không kế thừa đơn vị cũ của
            // thiết bị nguồn (dto.UnitId).
            UnitId = targetInfrastructure.UnitId ?? dto.UnitId,
            FormValues = dto.FormValues,
            CreatedBy = userName,
            CreatedAt = DateTime.UtcNow,
            StatusTransition = null,
        };

        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var creatorId))
            replacementEquipment.CreatorId = creatorId;

        sourceEquipment.IsActive = false;
        sourceEquipment.ModifiedBy = userName;
        sourceEquipment.ModifiedDate = DateTime.UtcNow;
        sourceEquipment.StatusTransition = 0; // 0: Đã chuyển TBA, 1: Đã chuyển hồ sơ
        sourceEquipment.Note = note;

        var result = await _equipmentRepository.CloneForInfrastructureTransferAsync(
            sourceEquipment,
            replacementEquipment);

        if (!result)
            return BadRequest(new { message = "Không thể chuyển thiết bị sang hạ tầng mới." });

        try
        {
            await _messageProducer.SendMessageAsync(new
            {
                Id = replacementEquipment.Id,
                EquipmentTypeId = replacementEquipment.EquipmentTypeId,
                Name = replacementEquipment.Name,
                Code = replacementEquipment.Code,
                SerialNumber = replacementEquipment.SerialNumber,
                CreatedAt = replacementEquipment.CreatedAt,
                CreatedBy = replacementEquipment.CreatedBy,
                UnitId = replacementEquipment.UnitId,
                IsActive = replacementEquipment.IsActive,
            }, "equipment_sync_queue");

            await _messageProducer.SendMessageAsync(new
            {
                Id = sourceEquipment.Id,
                EquipmentTypeId = sourceEquipment.EquipmentTypeId,
                Name = sourceEquipment.Name,
                Code = sourceEquipment.Code,
                SerialNumber = sourceEquipment.SerialNumber,
                CreatedAt = sourceEquipment.CreatedAt,
                CreatedBy = sourceEquipment.CreatedBy,
                UnitId = sourceEquipment.UnitId,
                IsActive = sourceEquipment.IsActive
            }, "equipment_sync_queue");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to publish sync messages for equipment transfer from {SourceEquipmentId} to {ReplacementEquipmentId}.", id, replacementEquipment.Id);
        }

        try
        {
            // Đơn vị mới lấy từ TBA đích (Infrastructure.UnitId) — KHÔNG dùng replacementEquipment.UnitId vì
            // trường đó được copy nguyên từ đơn vị cũ, chưa phản ánh TBA mới.
            var newUnitId = infrastructures.FirstOrDefault(infra => infra.Id == InfrastructureId)?.UnitId;

            await _messageProducer.PublishToExchangeAsync(
                new EquipmentTbaTransferredEvent
                {
                    EquipmentId = replacementEquipment.Id,
                    EquipmentCode = replacementEquipment.Code,
                    OldUnitId = sourceEquipment.UnitId,
                    NewUnitId = newUnitId,
                    ActorUserId = userId,
                    Timestamp = DateTime.UtcNow
                },
                NotificationTopicTopology.ExchangeName,
                NotificationTopicTopology.EquipmentTbaTransferredRoutingKey);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to publish notification event for equipment transfer from {SourceEquipmentId} to {ReplacementEquipmentId}.", id, replacementEquipment.Id);
        }

        var createdDto = await _equipmentRepository.GetDtoByIdAsync(replacementEquipment.Id);
        return CreatedAtAction(nameof(GetById), new { id = replacementEquipment.Id }, createdDto);
    }
    [HttpPost("{id}/copy-detailbyid")]
    public async Task<IActionResult> CreateDetailFromById(Guid id)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(id);
        if (dto == null)
            return NotFound();

        var sourceEquipment = await _equipmentRepository.GetByIdAsync(id);
        if (sourceEquipment == null)
            return NotFound();

        if (sourceEquipment.StatusTransition == 1)
            return BadRequest(new { message = "Thiết bị nguồn đã được chuyển hồ sơ." });

        var replacementEquipment = await _equipmentRepository.GetDetailTransferTargetAsync(sourceEquipment);
        if (replacementEquipment == null)
        {
            return BadRequest(new
            {
                message = "Không tìm thấy thiết bị mới có cùng mã, thuộc hạ tầng khác và chưa nhận hồ sơ."
            });
        }

        sourceEquipment.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        sourceEquipment.ModifiedDate = DateTime.UtcNow;
        sourceEquipment.StatusTransition = 1;

        var transferredDossierIds = await _equipmentRepository.CloneDossiersAndDocumentsForDetailTransferAsync(
            sourceEquipment,
            replacementEquipment);

        try
        {
            // replacementEquipment.UnitId cũng không đáng tin (xem ghi chú ở copy-byid) — lấy đơn vị nhận
            // thật sự từ Infrastructure.UnitId của TBA đích.
            var actorUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var infrastructures = await _equipmentRepository.GetInfrastructuresLookupAsync(null);
            var newUnitId = infrastructures.FirstOrDefault(infra => infra.Id == replacementEquipment.InfrastructureId)?.UnitId;

            await _messageProducer.PublishToExchangeAsync(
                new EquipmentDossierTransferredEvent
                {
                    EquipmentId = replacementEquipment.Id,
                    EquipmentCode = replacementEquipment.Code,
                    OldUnitId = sourceEquipment.UnitId,
                    NewUnitId = newUnitId,
                    ActorUserId = actorUserId,
                    Timestamp = DateTime.UtcNow
                },
                NotificationTopicTopology.ExchangeName,
                NotificationTopicTopology.EquipmentDossierTransferredRoutingKey);

            foreach (var dossierId in transferredDossierIds)
            {
                try
                {
                    var indexEvent = new DossierChangedEvent(
                        DossierIndexIdNormalizer.Normalize(dossierId.ToString()),
                        DossierChangedActions.Created,
                        Guid.NewGuid().ToString(),
                        DateTime.UtcNow);

                    await _messageProducer.SendMessageAsync(indexEvent, DossierMessaging.IndexQueue);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to publish dossier index event for transferred dossier {DossierId}.", dossierId);
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to publish notification event for dossier transfer from {SourceEquipmentId} to {ReplacementEquipmentId}.", id, replacementEquipment.Id);
        }

        var targetDto = await _equipmentRepository.GetDtoByIdAsync(replacementEquipment.Id);
        return Ok(new
        {
            message = "Đã chuyển hồ sơ thiết bị sang bản ghi mới.",
            equipment = targetDto
        });
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EquipmentCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Mã và Tên thiết bị là bắt buộc." });

        if (dto.EquipmentTypeId == Guid.Empty)
            return BadRequest(new { message = "Loại thiết bị không hợp lệ." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null)
        {
            if (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value))
            {
                return BadRequest(new { message = "Bạn không có quyền quản lý dữ liệu của đơn vị được chọn." });
            }
        }

        var trimmedCode = dto.Code.Trim();
        var existingByCode = await _equipmentRepository.GetByCodeAsync(trimmedCode, dto.InfrastructureId);
        if (existingByCode != null)
        {
            var duplicateMessage = $"Mã thiết bị '{trimmedCode}' đã tồn tại trong hệ thống.";
            return BadRequest(new { message = duplicateMessage, code = duplicateMessage });
        }

        var equipmentId = Guid.NewGuid();
        var equipment = new Equipment
        {
            Id = equipmentId,
            EquipmentTypeId = dto.EquipmentTypeId,
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim(),
            InfrastructureId = dto.InfrastructureId,
            ManufactureYear = dto.ManufactureYear,
            EquipmentStatusId = dto.EquipmentStatusId,
            IsActive = dto.IsActive,
            UnitId = dto.UnitId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system"
        };

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var creatorGuid))
        {
            equipment.CreatorId = creatorGuid;
        }

        var result = await _equipmentRepository.CreateAsync(equipment);
        if (result)
        {
            try
            {
                var syncMessage = new
                {
                    Id = equipment.Id,
                    EquipmentTypeId = equipment.EquipmentTypeId,
                    Name = equipment.Name,
                    Code = equipment.Code,
                    SerialNumber = equipment.SerialNumber,
                    CreatedAt = equipment.CreatedAt,
                    CreatedBy = equipment.CreatedBy,
                    UnitId = equipment.UnitId
                };
                await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to publish sync message for equipment creation.");
            }

            var createdDto = await _equipmentRepository.GetDtoByIdAsync(equipmentId);
            return CreatedAtAction(nameof(GetById), new { id = equipmentId }, createdDto);
        }

        return BadRequest(new { message = "Không thể tạo thiết bị mới." });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EquipmentUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Mã và Tên thiết bị là bắt buộc." });

        if (dto.EquipmentTypeId == Guid.Empty)
            return BadRequest(new { message = "Loại thiết bị không hợp lệ." });

        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null)
        {
            if (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value))
            {
                return Forbid();
            }
            if (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value))
            {
                return BadRequest(new { message = "Bạn không có quyền quản lý dữ liệu của đơn vị mới được chọn." });
            }
        }

        var trimmedCode = dto.Code.Trim();
        var existingByCode = await _equipmentRepository.GetByCodeAsync(trimmedCode, dto.InfrastructureId);
        if (existingByCode != null && existingByCode.Id != id)
        {
            var duplicateMessage = $"Mã thiết bị '{trimmedCode}' đã được sử dụng bởi bản ghi khác.";
            return BadRequest(new { message = duplicateMessage, code = duplicateMessage });
        }

        existing.EquipmentTypeId = dto.EquipmentTypeId;
        existing.Name = dto.Name.Trim();
        existing.Code = dto.Code.Trim();
        existing.InfrastructureId = dto.InfrastructureId;
        existing.ManufactureYear = dto.ManufactureYear;
        existing.EquipmentStatusId = dto.EquipmentStatusId;
        existing.UnitId = dto.UnitId;
        existing.IsActive = dto.IsActive;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
        {
            try
            {
                var syncMessage = new
                {
                    Id = existing.Id,
                    EquipmentTypeId = existing.EquipmentTypeId,
                    Name = existing.Name,
                    Code = existing.Code,
                    SerialNumber = existing.SerialNumber,
                    CreatedAt = existing.CreatedAt,
                    CreatedBy = existing.CreatedBy,
                    UnitId = existing.UnitId
                };
                await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to publish sync message for equipment update.");
            }

            return NoContent();
        }

        return BadRequest(new { message = "Không thể cập nhật thiết bị." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        var result = await _equipmentRepository.DeleteAsync(id);
        if (result)
            return NoContent();

        return BadRequest(new { message = "Không thể xóa thiết bị." });
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        existing.IsActive = false;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
            return Ok(new { message = "Đã khóa thiết bị thành công." });

        return BadRequest(new { message = "Không thể khóa thiết bị." });
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        existing.IsActive = true;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
            return Ok(new { message = "Đã mở khóa thiết bị thành công." });

        return BadRequest(new { message = "Không thể mở khóa thiết bị." });
    }

    [HttpGet("lookup")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetLookup()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        long? startUnitId = null;
        if (!isAdmin)
        {
            var userUnitId = GetUserUnitIdFromClaims();
            if (userUnitId.HasValue)
            {
                startUnitId = userUnitId.Value;
            }
            else
            {
                var fallbackUnitIds = GetAuthorizedUnitIds();
                if (fallbackUnitIds != null && fallbackUnitIds.Any())
                {
                    startUnitId = fallbackUnitIds.First();
                }
            }
        }

        var allowedUnitIdsForInfra = await GetAllowedUnitIdsAsync();
        var organizationUnits = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(startUnitId);
        var infrastructures = await _equipmentRepository.GetInfrastructuresLookupAsync(allowedUnitIdsForInfra);
        var gridTypes = await _equipmentTypeRepository.GetGridTypesAsync();
        var equipmentTypes = await _equipmentRepository.GetEquipmentTypesLookupAsync();

        return Ok(new
        {
            organizationUnits,
            infrastructures,
            gridTypes,
            equipmentTypes
        });
    }

    [HttpGet("get-organization-units")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetOrganizationUnits()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        long? startUnitId = null;
        if (!isAdmin)
        {
            var userUnitId = GetUserUnitIdFromClaims();
            if (userUnitId.HasValue)
            {
                startUnitId = userUnitId.Value;
            }
            else
            {
                var fallbackUnitIds = GetAuthorizedUnitIds();
                if (fallbackUnitIds != null && fallbackUnitIds.Any())
                {
                    startUnitId = fallbackUnitIds.First();
                }
            }
        }

        var data = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(startUnitId);
        return Ok(data);
    }

    [HttpGet("get-infrastructures")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetInfrastructures()
    {
        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        var data = await _equipmentRepository.GetInfrastructuresLookupAsync(allowedUnitIds);
        return Ok(data);
    }

    /// <summary>
    /// Trạm/Đường dây của TẤT CẢ đơn vị đang hoạt động — dùng riêng cho dialog Chuyển thiết bị,
    /// không giới hạn theo đơn vị của người dùng vì đích chuyển có thể thuộc đơn vị khác.
    /// </summary>
    [HttpGet("get-infrastructures-all")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetAllInfrastructures()
    {
        var data = await _equipmentRepository.GetInfrastructuresLookupAsync(null);
        return Ok(data);
    }

    [HttpGet("get-grid-types")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetGridTypes()
    {
        var data = await _equipmentTypeRepository.GetGridTypesAsync();
        return Ok(data);
    }

    [HttpGet("get-equipment-types")]
    [BypassDynamicPermission]
    public async Task<IActionResult> GetEquipmentTypes()
    {
        var data = await _equipmentRepository.GetEquipmentTypesLookupAsync();
        return Ok(data);
    }

    private async Task<List<long>?> GetAllowedUnitIdsAsync()
    {
        var isAdmin = User.IsInRole("ADMIN") || User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
        if (isAdmin)
        {
            return null;
        }

        var userUnitId = GetUserUnitIdFromClaims();
        if (userUnitId.HasValue)
        {
            var allowedUnits = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId.Value);
            return allowedUnits.Select(u => u.Id).ToList();
        }

        var fallbackUnitIds = GetAuthorizedUnitIds();
        if (fallbackUnitIds != null && fallbackUnitIds.Any())
        {
            var list = new List<long>();
            foreach (var fId in fallbackUnitIds)
            {
                var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(fId);
                list.AddRange(units.Select(u => u.Id));
            }
            return list.Distinct().ToList();
        }

        return new List<long> { -1 };
    }

    private long? GetUserUnitIdFromClaims()
    {
        var claimNames = new[]
        {
            "unit_id",
            "UnitId",
            "unitId",
            "Unit_Id",
            "organization_unit_id",
            "organizationUnitId",
            "OrganizationUnitId"
        };

        foreach (var claimName in claimNames)
        {
            var value = User.FindFirst(claimName)?.Value;
            if (long.TryParse(value, out var unitId) && unitId > 0)
                return unitId;
        }

        return null;
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

    [HttpPut("{id}/form-values")]
    public async Task<IActionResult> UpdateFormValues(Guid id, [FromBody] UpdateFormValuesRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Dữ liệu không hợp lệ." });

        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        existing.FormValues = request.FormValues;
        existing.ModifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        existing.ModifiedDate = DateTime.UtcNow;

        var result = await _equipmentRepository.UpdateAsync(existing);
        if (result)
        {
            try
            {
                var syncMessage = new
                {
                    Id = existing.Id,
                    EquipmentTypeId = existing.EquipmentTypeId,
                    Name = existing.Name,
                    Code = existing.Code,
                    SerialNumber = existing.SerialNumber,
                    CreatedAt = existing.CreatedAt,
                    CreatedBy = existing.CreatedBy,
                    UnitId = existing.UnitId
                };
                await _messageProducer.SendMessageAsync(syncMessage, "equipment_sync_queue");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to publish sync message for equipment form values update.");
            }

            return NoContent();
        }

        return BadRequest(new { message = "Không thể cập nhật thông số thiết bị." });
    }

    [HttpPut("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var existing = await _equipmentRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!existing.UnitId.HasValue || !allowedUnitIds.Contains(existing.UnitId.Value)))
        {
            return Forbid();
        }

        var modifiedBy = User.FindFirst(ClaimTypes.Name)?.Value ?? User.Identity?.Name ?? "system";
        var result = await _equipmentRepository.ConfirmAsync(id, modifiedBy);
        if (!result)
            return BadRequest(new { message = "Không thể xác nhận thiết bị." });

        return NoContent();
    }

    /// <summary>
    /// Biểu mẫu EAV thông số theo loại thiết bị — tuân thủ quyền EQUIPMENT_VIEW, không gọi eav-form-templates.
    /// </summary>
    [HttpGet("{id:guid}/form-template")]
    public async Task<IActionResult> GetFormTemplate(Guid id)
    {
        var dto = await _equipmentRepository.GetDtoByIdAsync(id);
        if (dto == null)
            return NotFound(new { message = "Không tìm thấy thiết bị." });

        var allowedUnitIds = await GetAllowedUnitIdsAsync();
        if (allowedUnitIds != null && (!dto.UnitId.HasValue || !allowedUnitIds.Contains(dto.UnitId.Value)))
            return Forbid();

        if (string.IsNullOrWhiteSpace(dto.FormSchema))
            return NotFound(new { message = "Loại thiết bị chưa có biểu mẫu thông số kỹ thuật." });

        return Ok(new
        {
            id = dto.FormTemplateId,
            name = dto.FormTemplateName,
            formSchema = dto.FormSchema
        });
    }

}

public class UpdateFormValuesRequest
{
    public string FormValues { get; set; } = string.Empty;
}
