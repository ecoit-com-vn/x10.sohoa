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

    /// <summary>Đếm thiết bị bị đánh dấu "Đã chuyển TBA" bởi PMIS_SYNC trong <paramref name="sinceHours"/>
    /// giờ gần đây — dùng bởi PmisReconciliationJob (SyncService) làm compensating check nhẹ cho khả năng
    /// mất EquipmentTbaTransferredEvent (xem EquipmentRepository.CountRecentlyTransferredAsync).</summary>
    [HttpGet("equipment/recently-transferred-count")]
    public async Task<IActionResult> GetRecentlyTransferredCount(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken, [FromQuery] int sinceHours = 24)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;

        var count = await _equipmentRepository.CountRecentlyTransferredAsync(DateTime.UtcNow.AddHours(-sinceHours));
        return Ok(new { count });
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
                var (id, wasCreated, hasChanged, parentUnresolved) = await _infrastructureRepository.UpsertFromPmisAsync(
                    item.InfraTypeId, item.PmisCode, item.Code, item.Name, item.Address, item.UnitCode, item.OperationDate,
                    item.GridTypeId, item.ParentPmisCode);
                results.Add(new UpsertInfrastructureFromPmisResult
                {
                    PmisCode = item.PmisCode,
                    Success = true,
                    InfrastructureId = id,
                    WasCreated = wasCreated,
                    HasChanged = hasChanged,
                    ParentUnresolved = parentUnresolved
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

        // Prefetch 4 lookup lặp lại (cha, loại thiết bị, đơn vị, bản ghi đã tồn tại) cho CẢ TRANG trong 3-4
        // round-trip DB cố định thay vì tới 4×N — audit hiệu năng PMIS 2026-09-24, xem
        // EquipmentRepository.PrefetchUpsertLookupsAsync. Lỗi ở bước này (hiếm — DB tạm gián đoạn) không
        // chặn cả request: rơi về prefetch=null, mỗi item tự SELECT riêng như trước (chậm hơn nhưng đúng).
        EquipmentUpsertPrefetch? prefetch = null;
        try
        {
            prefetch = await _equipmentRepository.PrefetchUpsertLookupsAsync(
                items.Select(i => (i.PmisCode, i.ParentPmisCode, i.UnitCode)).ToList());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "InternalPmisSyncController: lỗi khi prefetch lookup cho lô {Count} thiết bị — rơi về tra từng bản ghi riêng lẻ (chậm hơn).", items.Count);
        }

        var results = new List<UpsertEquipmentFromPmisResult>();
        foreach (var item in items)
        {
            try
            {
                var upsertResult = await _equipmentRepository.UpsertFromPmisAsync(
                    item.PmisCode, item.Code, item.Name, item.SerialNumber,
                    item.EquipmentTypeCode, item.ParentPmisCode, item.UnitCode,
                    item.ManufactureYear, item.QrCodeBase64, item.GridTypeId, item.EquipmentTypeName,
                    prefetch);

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
                    await _equipmentPmisSpecRepository.UpsertAsync(upsertResult.EquipmentId!.Value, item.ThongSoKyThuat, null, item.TenThongSoKyThuat);
                }

                // Tự động tạo biểu mẫu thông số kỹ thuật nếu loại thiết bị chưa có — sinh sẵn trường theo
                // đúng khoá thongSoKyThuat PMIS thật, không tạo biểu mẫu rỗng (xem BuildAutoFormFieldsFromPmisSpec).
                if (upsertResult.EquipmentTypeId is Guid equipmentTypeId && !string.IsNullOrWhiteSpace(item.ThongSoKyThuat))
                {
                    await EnsureAutoFormTemplateAsync(equipmentTypeId, item.ThongSoKyThuat, item.TenThongSoKyThuat);

                    // Nhãn PMIS (tenThongSoKyThuat) là hằng số theo LOẠI thiết bị, không đổi theo từng
                    // thiết bị — lưu 1 lần/loại vào EquipmentTypes.PmisFieldLabels (chỉ ghi khi còn rỗng,
                    // xem SetPmisFieldLabelsIfEmptyAsync) để EquipmentController.GetPmisSpecKeys đọc
                    // thẳng 1 dòng thay vì phải quét/gộp nhãn từ tối đa 50 dòng EQUIPMENT_PMIS_SPEC mỗi
                    // lần gọi (review 2026-09-23) — EQUIPMENT_PMIS_SPEC.FieldLabels vẫn giữ nguyên riêng
                    // theo từng thiết bị, phục vụ panel "So sánh với PMIS".
                    if (!string.IsNullOrWhiteSpace(item.TenThongSoKyThuat))
                    {
                        await _equipmentTypeRepository.SetPmisFieldLabelsIfEmptyAsync(equipmentTypeId, item.TenThongSoKyThuat);
                    }
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

                // Ưu tiên gán theo ĐÚNG thiết bị nếu tài liệu có kèm mã thiết bị (DeviceCode) — kể cả khi
                // gọi từ lượt đồng bộ cấp Trạm/Đường dây (item.OwnerType="INFRASTRUCTURE" ở đây chỉ là
                // giá trị mặc định/dự phòng). Thiết bị chưa tồn tại (chưa đồng bộ tới) thì rơi về đúng
                // OwnerType/OwnerPmisCode ban đầu — KHÔNG bỏ qua tài liệu, tránh mất dữ liệu.
                // LUÔN resolve lại (kể cả khi item đã tồn tại + đã có file) — thiết bị thật có thể vừa
                // được tạo ở 1 lượt Equipment sync SAU lượt đã gán tài liệu này cho INFRASTRUCTURE, nên
                // không thể chỉ tin owner đã lưu trước đó (xem so sánh bên dưới).
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

                if (existing != null && !string.IsNullOrEmpty(existing.ObjectKey))
                {
                    // Owner đã resolve lại khác với owner đang lưu (đặc biệt: đang là INFRASTRUCTURE
                    // nhưng giờ đã khớp được EQUIPMENT thật) — sửa lại 2 cột này, KHÔNG cần tải lại file.
                    if (ownerId != null &&
                        (!string.Equals(existing.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase) ||
                         existing.OwnerId != ownerId.Value))
                    {
                        await _pmisDocumentRepository.UpdateOwnerAsync(existing.Id, ownerType, ownerId.Value);
                    }

                    results.Add(new UpsertPmisDocumentResult
                    {
                        PmisDocumentCode = item.PmisDocumentCode,
                        Success = true,
                        WasSkippedAsExisting = true
                    });
                    continue;
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
                        ErrorMessage = objectKey == null ? BuildFileDownloadFailedMessage(item) : null
                    });
                    continue;
                }

                item.OwnerType = ownerType; // ghi đúng OwnerType đã phân giải (có thể khác giá trị gửi lên nếu resolve theo DeviceCode thành công)
                await _pmisDocumentRepository.InsertAsync(item, ownerId.Value, objectKey, fileSize);
                results.Add(new UpsertPmisDocumentResult
                {
                    PmisDocumentCode = item.PmisDocumentCode,
                    Success = objectKey != null,
                    ErrorMessage = objectKey == null ? BuildFileDownloadFailedMessage(item) : null
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
    private async Task EnsureAutoFormTemplateAsync(Guid equipmentTypeId, string thongSoKyThuatJson, string? tenThongSoKyThuatJson)
    {
        try
        {
            var existingTemplate = await _eavFormTemplateRepository.GetActiveByEquipmentTypeIdAsync(equipmentTypeId);
            if (existingTemplate != null) return;

            var equipmentType = await _equipmentTypeRepository.GetByIdAsync(equipmentTypeId);
            if (equipmentType == null) return;

            var fields = BuildAutoFormFieldsFromPmisSpec(thongSoKyThuatJson, tenThongSoKyThuatJson);
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
    /// width/dataSourceType/selectAll/active/pmisFieldName) — 1 trường/khoá trong thongSoKyThuat. Label
    /// ưu tiên nhãn tiếng Việt thật PMIS cung cấp (<paramref name="tenThongSoKyThuatJson"/>, field
    /// "tenThongSoKyThuat", bổ sung 2026-09-23); nếu thiếu (null, JSON lỗi, hoặc khoá này chưa có nhãn
    /// tương ứng) thì để nguyên khoá PMIS như trước đây — Admin vẫn có thể vào Form Builder đổi tay.
    /// </summary>
    private static string? BuildAutoFormFieldsFromPmisSpec(string thongSoKyThuatJson, string? tenThongSoKyThuatJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(thongSoKyThuatJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            // Dùng chung EavSchemaHelper.MergeJsonStringLabelsInto với EquipmentController.GetPmisSpecKeys
            // thay vì tự viết lại logic parse nhãn (2 nơi từng lệch nhau ở việc có nhận value non-string
            // hay không — nhãn PMIS luôn là chuỗi nên helper chỉ nhận string, không mất gì ở đây).
            var labels = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            EavSchemaHelper.MergeJsonStringLabelsInto(labels, tenThongSoKyThuatJson);

            var fields = new List<object>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                fields.Add(new
                {
                    id = "f_" + Guid.NewGuid().ToString("N")[..7],
                    name = string.Empty,
                    label = labels.TryGetValue(property.Name, out var pmisLabel) ? pmisLabel : property.Name,
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

    /// <summary>Ghép nguyên nhân thật (nếu SyncService có gửi kèm — xem UpsertPmisDocumentRequest.FileDownloadError)
    /// vào thông báo chung, để admin thấy được lý do cụ thể ngay trong màn "Lịch sử đồng bộ" thay vì phải
    /// vào log pod SyncService mới biết được vì sao tải file thất bại.</summary>
    private static string BuildFileDownloadFailedMessage(UpsertPmisDocumentRequest item) =>
        string.IsNullOrWhiteSpace(item.FileDownloadError)
            ? "Không tải được file tài liệu từ PMIS — đã lưu thông tin, chưa có file."
            : $"Không tải được file tài liệu từ PMIS — đã lưu thông tin, chưa có file. Nguyên nhân: {item.FileDownloadError}";

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
