using System.Linq;
using System.Text.Json;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.EquipmentService.Core.Services;
using EvnHanoi.Infrastructure.Messaging;
using EvnHanoi.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace EvnHanoi.EquipmentService.Controllers;

/// <summary>
/// API NỘI BỘ — SyncService gọi để lưu dữ liệu Trạm/Đường dây/Thiết bị đã đồng bộ từ PMIS.
/// - Đặt ngoài tiền tố "/api/v1/..." nên KHÔNG có route ở ApiGateway ⇒ không expose ra Internet
///   (giống InternalDossierController).
/// - [BypassDynamicPermission]: không kiểm quyền người dùng cuối (gọi service-to-service).
/// - Phòng thủ chiều sâu: bắt buộc khớp shared-secret header "X-Internal-Token".
/// </summary>
[ApiController]
[Route("internal/v1")]
[BypassDynamicPermission]
public class InternalPmisSyncController : ControllerBase
{
    private readonly IInfrastructureRepository _infrastructureRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IEquipmentPmisSpecRepository _equipmentPmisSpecRepository;
    private readonly IEquipmentTypeRepository _equipmentTypeRepository;
    private readonly IEavFormTemplateRepository _eavFormTemplateRepository;
    private readonly IEavFormTemplateService _eavFormTemplateService;
    private readonly IPmisDocumentRepository _pmisDocumentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IConfiguration _configuration;
    private readonly IMessageProducer _messageProducer;

    public InternalPmisSyncController(
        IInfrastructureRepository infrastructureRepository,
        IEquipmentRepository equipmentRepository,
        IEquipmentPmisSpecRepository equipmentPmisSpecRepository,
        IEquipmentTypeRepository equipmentTypeRepository,
        IEavFormTemplateRepository eavFormTemplateRepository,
        IEavFormTemplateService eavFormTemplateService,
        IPmisDocumentRepository pmisDocumentRepository,
        IFileStorageService fileStorageService,
        IConfiguration configuration,
        IMessageProducer messageProducer)
    {
        _infrastructureRepository = infrastructureRepository;
        _equipmentRepository = equipmentRepository;
        _equipmentPmisSpecRepository = equipmentPmisSpecRepository;
        _equipmentTypeRepository = equipmentTypeRepository;
        _eavFormTemplateRepository = eavFormTemplateRepository;
        _eavFormTemplateService = eavFormTemplateService;
        _pmisDocumentRepository = pmisDocumentRepository;
        _fileStorageService = fileStorageService;
        _configuration = configuration;
        _messageProducer = messageProducer;
    }

    [HttpGet("infrastructure/synced-pmis-codes")]
    public async Task<IActionResult> GetSyncedPmisCodes([FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;

        var rows = await _infrastructureRepository.GetSyncedPmisCodesAsync();
        return Ok(rows.Select(r => new { pmisCode = r.PmisCode, infraTypeId = r.InfraTypeId }));
    }

    [HttpPost("infrastructure/upsert-from-pmis")]
    public async Task<IActionResult> UpsertInfrastructureFromPmis(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] List<UpsertInfrastructureFromPmisRequest> items)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (items is null || items.Count == 0) return BadRequest(new { message = "Danh sách rỗng." });

        var results = new List<UpsertInfrastructureFromPmisResult>();
        foreach (var item in items)
        {
            try
            {
                var (id, wasCreated, hasChanged) = await _infrastructureRepository.UpsertFromPmisAsync(
                    item.InfraTypeId, item.PmisCode, item.Code, item.Name, item.Address, item.UnitCode, item.OperationDate, item.GridTypeId);
                results.Add(new UpsertInfrastructureFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = true,
                    InfrastructureId = id,
                    WasCreated = wasCreated,
                    HasChanged = hasChanged
                });
            }
            catch (Exception ex)
            {
                results.Add(new UpsertInfrastructureFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return Ok(results);
    }

    [HttpPost("equipment/upsert-from-pmis")]
    public async Task<IActionResult> UpsertEquipmentFromPmis(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] List<UpsertEquipmentFromPmisRequest> items)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (items is null || items.Count == 0) return BadRequest(new { message = "Danh sách rỗng." });

        var results = new List<UpsertEquipmentFromPmisResult>();
        foreach (var item in items)
        {
            try
            {
                var upsertResult = await _equipmentRepository.UpsertFromPmisAsync(
                    item.PmisCode, item.Code, item.Name, item.SerialNumber,
                    item.EquipmentTypeCode, item.ParentPmisCode, item.UnitCode,
                    item.ManufactureYear, item.QrCodeBase64, item.GridTypeId, item.EquipmentTypeName);

                if (!upsertResult.Success)
                {
                    results.Add(new UpsertEquipmentFromPmisResult
                    {
                        PmisCode = item.PmisCode,
                        Success = false,
                        ErrorMessage = upsertResult.ErrorMessage
                    });
                    continue;
                }

                if (upsertResult.WasTransferred)
                {
                    // Tái nạp đầy đủ dữ liệu bản ghi cũ/mới (thay vì tự dựng object rút gọn) — worker
                    // EquipmentIndexWorker REPLACE nguyên document Elasticsearch theo đúng payload gửi lên,
                    // gửi thiếu trường sẽ làm mất dữ liệu đã index trước đó, không phải update từng phần.
                    try
                    {
                        var oldEquipment = await _equipmentRepository.GetByIdAsync(upsertResult.OldEquipmentId!.Value);
                        if (oldEquipment != null)
                            await _messageProducer.SendMessageAsync(oldEquipment, "equipment_sync_queue");

                        var newEquipment = await _equipmentRepository.GetByIdAsync(upsertResult.EquipmentId!.Value);
                        if (newEquipment != null)
                            await _messageProducer.SendMessageAsync(newEquipment, "equipment_sync_queue");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "InternalPmisSyncController: lỗi khi gửi message đồng bộ index cho thiết bị {PmisCode} sau khi chuyển TBA tự động.", item.PmisCode);
                    }

                    try
                    {
                        await _messageProducer.PublishToExchangeAsync(
                            new EquipmentTbaTransferredEvent
                            {
                                EquipmentId = upsertResult.EquipmentId!.Value,
                                EquipmentCode = item.Code,
                                OldUnitId = upsertResult.OldUnitId,
                                NewUnitId = upsertResult.NewUnitId,
                                ActorUserId = "PMIS_SYNC",
                                Timestamp = DateTime.UtcNow
                            },
                            NotificationTopicTopology.ExchangeName,
                            NotificationTopicTopology.EquipmentTbaTransferredRoutingKey);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "InternalPmisSyncController: lỗi khi phát sự kiện chuyển TBA tự động cho thiết bị {PmisCode}.", item.PmisCode);
                    }
                }

                // Thông số kỹ thuật lưu riêng — KHÔNG ghi đè EQUIPMENTS.FormValues (dữ liệu người dùng chỉnh sửa nội bộ).
                if (!string.IsNullOrWhiteSpace(item.ThongSoKyThuat))
                {
                    await _equipmentPmisSpecRepository.UpsertAsync(upsertResult.EquipmentId!.Value, item.ThongSoKyThuat, null);
                }

                // Tự động tạo biểu mẫu thông số kỹ thuật nếu loại thiết bị chưa có — sinh sẵn trường theo
                // đúng khoá thongSoKyThuat PMIS thật, không tạo biểu mẫu rỗng (xem BuildAutoFormFieldsFromPmisSpec).
                if (upsertResult.EquipmentTypeId is Guid equipmentTypeId && !string.IsNullOrWhiteSpace(item.ThongSoKyThuat))
                {
                    await EnsureAutoFormTemplateAsync(equipmentTypeId, item.ThongSoKyThuat);
                }

                // Thiết bị chưa có thông số nào (FORM_VALUES NULL — mới tạo, hoặc chưa ai nhập tay) thì lấy
                // luôn dữ liệu PMIS làm giá trị mặc định, khớp đúng field theo FormSchema hiện hành (đến
                // đây template chắc chắn đã tồn tại, kể cả vừa mới tự tạo ở bước trên). KHÔNG bao giờ ghi
                // đè nếu đã có dữ liệu — SetFormValuesIfEmptyAsync tự bảo đảm điều đó ở tầng SQL.
                if (!string.IsNullOrWhiteSpace(item.ThongSoKyThuat) && upsertResult.EquipmentId is Guid equipmentIdForDefaults)
                {
                    var equipmentDto = await _equipmentRepository.GetDtoByIdAsync(equipmentIdForDefaults);
                    if (equipmentDto != null && string.IsNullOrWhiteSpace(equipmentDto.FormValues) && !string.IsNullOrWhiteSpace(equipmentDto.FormSchema))
                    {
                        var defaultFormValues = BuildDefaultFormValuesFromPmisSpec(equipmentDto.FormSchema!, item.ThongSoKyThuat);
                        if (defaultFormValues != null)
                        {
                            await _equipmentRepository.SetFormValuesIfEmptyAsync(equipmentIdForDefaults, defaultFormValues);
                        }
                    }
                }

                results.Add(new UpsertEquipmentFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = true,
                    EquipmentId = upsertResult.EquipmentId,
                    WasCreated = upsertResult.WasCreated,
                    HasChanged = upsertResult.HasChanged
                });
            }
            catch (Exception ex)
            {
                // Cách ly lỗi theo từng item — 1 thiết bị lỗi (deadlock, race condition khi upsert
                // EQUIPMENT_PMIS_SPEC...) không được làm mất kết quả của các item đã xử lý xong trước đó
                // hay chặn các item còn lại, giống đúng khuôn của UpsertInfrastructureFromPmis ở trên.
                results.Add(new UpsertEquipmentFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return Ok(results);
    }

    [HttpPost("documents/upsert-from-pmis")]
    public async Task<IActionResult> UpsertDocumentsFromPmis(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] List<UpsertPmisDocumentRequest> items)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (items is null || items.Count == 0) return BadRequest(new { message = "Danh sách rỗng." });

        var results = new List<UpsertPmisDocumentResult>();
        foreach (var item in items)
        {
            try
            {
                var existing = await _pmisDocumentRepository.GetByCodeAsync(item.PmisDocumentCode);
                if (existing != null && !string.IsNullOrEmpty(existing.ObjectKey))
                {
                    results.Add(new UpsertPmisDocumentResult
                    {
                        PmisDocumentCode = item.PmisDocumentCode,
                        Success = true,
                        WasSkippedAsExisting = true
                    });
                    continue;
                }

                // Ưu tiên gán theo ĐÚNG thiết bị nếu tài liệu có kèm mã thiết bị (DeviceCode) — kể cả khi
                // gọi từ lượt đồng bộ cấp Trạm/Đường dây (item.OwnerType="INFRASTRUCTURE" ở đây chỉ là
                // giá trị mặc định/dự phòng). Thiết bị chưa tồn tại (chưa đồng bộ tới) thì rơi về đúng
                // OwnerType/OwnerPmisCode ban đầu — KHÔNG bỏ qua tài liệu, tránh mất dữ liệu.
                var ownerType = item.OwnerType;
                var ownerId = string.IsNullOrWhiteSpace(item.DeviceCode)
                    ? null
                    : await _pmisDocumentRepository.ResolveOwnerIdAsync("EQUIPMENT", item.DeviceCode);
                if (ownerId != null)
                {
                    ownerType = "EQUIPMENT";
                }
                else
                {
                    ownerId = await _pmisDocumentRepository.ResolveOwnerIdAsync(item.OwnerType, item.OwnerPmisCode);
                }

                if (ownerId == null)
                {
                    results.Add(new UpsertPmisDocumentResult
                    {
                        PmisDocumentCode = item.PmisDocumentCode,
                        Success = false,
                        ErrorMessage = "Không tìm thấy đối tượng sở hữu tài liệu (Trạm/Đường dây/Thiết bị)."
                    });
                    continue;
                }

                string? objectKey = null;
                long? fileSize = null;
                if (!string.IsNullOrWhiteSpace(item.FileBase64))
                {
                    var bytes = Convert.FromBase64String(item.FileBase64);
                    using var stream = new MemoryStream(bytes);
                    var (key, _) = await _fileStorageService.UploadPmisDocumentAsync(
                        stream, item.FileName ?? item.PmisDocumentCode, "application/octet-stream", bytes.Length,
                        item.OwnerType, ownerId.Value);
                    objectKey = key;
                    fileSize = bytes.Length;
                }

                if (existing != null)
                {
                    // Đã có dòng metadata từ lần trước (tải file lỗi) — chỉ update nếu lần này tải
                    // được, không INSERT lại vì PmisDocumentCode đã UNIQUE.
                    if (objectKey != null)
                        await _pmisDocumentRepository.UpdateFileAsync(existing.Id, objectKey, fileSize!.Value, item.SyncHistoryId);

                    results.Add(new UpsertPmisDocumentResult
                    {
                        PmisDocumentCode = item.PmisDocumentCode,
                        Success = objectKey != null,
                        ErrorMessage = objectKey == null
                            ? "Không tải được file tài liệu từ PMIS — đã lưu thông tin, chưa có file."
                            : null
                    });
                    continue;
                }

                item.OwnerType = ownerType; // ghi đúng OwnerType đã phân giải (có thể khác giá trị gửi lên nếu resolve theo DeviceCode thành công)
                await _pmisDocumentRepository.InsertAsync(item, ownerId.Value, objectKey, fileSize);
                results.Add(new UpsertPmisDocumentResult
                {
                    PmisDocumentCode = item.PmisDocumentCode,
                    Success = objectKey != null,
                    ErrorMessage = objectKey == null
                        ? "Không tải được file tài liệu từ PMIS — đã lưu thông tin, chưa có file."
                        : null
                });
            }
            catch (Exception ex)
            {
                results.Add(new UpsertPmisDocumentResult
                {
                    PmisDocumentCode = item.PmisDocumentCode,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return Ok(results);
    }

    /// <summary>
    /// Tự động tạo biểu mẫu thông số kỹ thuật (EAV form template, FormType="TEMPLATE") cho loại thiết bị
    /// nếu chưa có — sinh sẵn trường theo đúng khoá thongSoKyThuat PMIS thật của thiết bị đầu tiên kích
    /// hoạt việc tạo, để panel so sánh có dữ liệu ngay, không cần Admin vào Form Builder tạo tay trước.
    /// Lỗi ở bước này CHỈ log cảnh báo — thiết bị đã lưu thành công trước đó, không bị ảnh hưởng.
    /// </summary>
    private async Task EnsureAutoFormTemplateAsync(Guid equipmentTypeId, string thongSoKyThuatJson)
    {
        try
        {
            var existingTemplate = await _eavFormTemplateRepository.GetActiveByEquipmentTypeIdAsync(equipmentTypeId);
            if (existingTemplate != null) return;

            var equipmentType = await _equipmentTypeRepository.GetByIdAsync(equipmentTypeId);
            if (equipmentType == null) return;

            var fields = BuildAutoFormFieldsFromPmisSpec(thongSoKyThuatJson);
            if (fields == null) return; // JSON không hợp lệ hoặc không phải object — không tạo biểu mẫu rỗng vô nghĩa.

            await _eavFormTemplateService.CreateFormTemplateAsync(
                name: $"Biểu mẫu {equipmentType.Name} (tự động tạo từ PMIS)",
                code: $"AUTO_{equipmentType.Code}",
                category: equipmentType.Code,
                description: string.Empty,
                descriptionInfo: "Tự động tạo khi đồng bộ thiết bị đầu tiên của loại này từ PMIS — các trường lấy đúng theo khoá thongSoKyThuat PMIS trả về.",
                formSchema: fields,
                createdBy: "PMIS_SYNC",
                equipmentTypeId: equipmentTypeId,
                formType: "TEMPLATE",
                gridTypeId: equipmentType.GridTypeId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "InternalPmisSyncController: lỗi tự tạo biểu mẫu cho loại thiết bị {EquipmentTypeId}, bỏ qua — thiết bị vẫn lưu bình thường.", equipmentTypeId);
        }
    }

    /// <summary>
    /// Sinh mảng JSON đúng shape FormField phía Form Builder (id/name/label/type/placeholder/required/
    /// width/dataSourceType/selectAll/active/pmisFieldName) — 1 trường/khoá trong thongSoKyThuat, Label để
    /// nguyên khoá PMIS (không tự "làm đẹp" tên, Admin có thể vào Form Builder đổi sau).
    /// </summary>
    private static string? BuildAutoFormFieldsFromPmisSpec(string thongSoKyThuatJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(thongSoKyThuatJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var fields = new List<object>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                fields.Add(new
                {
                    id = "f_" + Guid.NewGuid().ToString("N")[..7],
                    name = string.Empty,
                    label = property.Name,
                    type = "text",
                    placeholder = string.Empty,
                    required = false,
                    width = 100,
                    dataSourceType = "manual",
                    selectAll = false,
                    active = true,
                    pmisFieldName = property.Name
                });
            }

            return fields.Count > 0 ? JsonSerializer.Serialize(fields) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Dựng EQUIPMENTS.FormValues mặc định từ thongSoKyThuat PMIS, khớp đúng từng field trong formSchema
    /// theo CÙNG logic khoá đang dùng để đọc lại giá trị (EavSchemaHelper.ResolveSchemaFieldName —
    /// name → key → id → fieldName) — field tự sinh bởi BuildAutoFormFieldsFromPmisSpec có name rỗng nên
    /// khoá thật là "id" ngẫu nhiên, không phải mã PMIS; nếu tự đoán sai khoá thì FE sẽ không hiển thị
    /// được giá trị vừa ghi. Tra giá trị PMIS theo pmisFieldName (ưu tiên) hoặc khoá đã resolve (fallback).
    /// </summary>
    private static string? BuildDefaultFormValuesFromPmisSpec(string formSchemaJson, string thongSoKyThuatJson)
    {
        try
        {
            using var pmisDoc = JsonDocument.Parse(thongSoKyThuatJson);
            if (pmisDoc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var formValues = new Dictionary<string, object?>();
            foreach (var field in EavSchemaHelper.EnumerateSchemaFields(formSchemaJson))
            {
                var localKey = EavSchemaHelper.ResolveSchemaFieldName(field);
                if (string.IsNullOrWhiteSpace(localKey)) continue;

                var pmisKey = EavSchemaHelper.ReadSchemaString(field, "pmisFieldName", "PmisFieldName") ?? localKey;
                if (!EavSchemaHelper.TryGetPropertyIgnoreCase(pmisDoc.RootElement, pmisKey, out var pmisValue)) continue;
                if (pmisValue.ValueKind == JsonValueKind.Null) continue;

                formValues[localKey] = pmisValue.ValueKind == JsonValueKind.String ? pmisValue.GetString() : pmisValue.ToString();
            }

            return formValues.Count > 0 ? JsonSerializer.Serialize(formValues) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool ValidateInternalToken(string? internalToken, out IActionResult? errorResult)
    {
        var expected = _configuration["Internal:Token"];
        if (string.IsNullOrEmpty(expected))
        {
            errorResult = StatusCode(503, new { message = "Internal:Token chưa được cấu hình trên EquipmentService." });
            return false;
        }

        if (!string.Equals(internalToken, expected, StringComparison.Ordinal))
        {
            errorResult = Unauthorized(new { message = "Token nội bộ không hợp lệ." });
            return false;
        }

        errorResult = null;
        return true;
    }
}
