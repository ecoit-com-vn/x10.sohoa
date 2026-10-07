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
                    item.GridTypeId, item.ParentPmisCode, item.CmisCode);
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
                    // Tài liệu đi theo thiết bị đang sống (job danh sách tài liệu chỉ lấy phần mới nên không còn tự đẩy lại để sửa owner).
                    if (upsertResult.OldEquipmentId is Guid oldEqId && upsertResult.EquipmentId is Guid newEqId)
                    {
                        try { await _pmisDocumentRepository.MoveDocumentsBetweenEquipmentAsync(oldEqId, newEqId); }
                        catch (Exception ex) { Log.Warning(ex, "InternalPmisSyncController: lỗi chuyển tài liệu theo thiết bị khi chuyển TBA {PmisCode}.", item.PmisCode); }
                    }

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

                // Thiết bị MỚI tạo: nhận lại các tài liệu đã lưu tạm cho Trạm/Đường dây khi thiết bị chưa tồn tại (DEVICE_CODE khớp).
                // Job danh sách tài liệu chỉ lấy phần mới nên không còn tự sửa owner như khi mỗi chu kỳ đẩy lại mọi tài liệu. Lỗi ở đây chỉ log.
                if (upsertResult.WasCreated && upsertResult.EquipmentId is Guid newEquipmentId)
                {
                    try { await _pmisDocumentRepository.ReassignDocumentsToEquipmentAsync(item.PmisCode, newEquipmentId); }
                    catch (Exception ex) { Log.Warning(ex, "InternalPmisSyncController: lỗi chuyển tài liệu sang thiết bị mới {PmisCode}.", item.PmisCode); }
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
                if (existing != null && !string.IsNullOrWhiteSpace(item.DeviceCode))
                    await _pmisDocumentRepository.SetDeviceCodeAsync(existing.Id, item.DeviceCode);

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

                // Pha đồng bộ DANH SÁCH chỉ lưu metadata + URL file (FILE_STATUS=PENDING) — việc tải file vật
                // lý do job nền PmisDocumentFileDownloadJob (SyncService) đảm nhiệm qua pending-files/attach-file.
                // FileBase64 vẫn được chấp nhận (tương thích client cũ / upload trực tiếp) — có thì lưu luôn.
                string? objectKey = null;
                long? fileSize = null;
                if (!string.IsNullOrWhiteSpace(item.FileBase64))
                {
                    var bytes = Convert.FromBase64String(item.FileBase64);
                    using var stream = new MemoryStream(bytes);
                    var (key, _) = await _fileStorageService.UploadPmisDocumentAsync(
                        stream, item.FileName ?? item.PmisDocumentCode, "application/octet-stream", bytes.Length,
                        ownerType, ownerId.Value);
                    objectKey = key;
                    fileSize = bytes.Length;
                }

                if (existing != null)
                {
                    // Đã có dòng metadata (chưa có file) — chỉ cập nhật file/URL, không INSERT lại vì
                    // PmisDocumentCode đã UNIQUE. Cũng sửa owner nếu resolve lại ra chủ đúng hơn (giống nhánh
                    // đã có file ở trên): dòng chờ tải lâu có thể được gán tạm cho INFRASTRUCTURE trước khi
                    // EQUIPMENT thật tồn tại; attach-file lưu file theo owner đang có trên dòng này.
                    if (!string.Equals(existing.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase) ||
                        existing.OwnerId != ownerId.Value)
                    {
                        await _pmisDocumentRepository.UpdateOwnerAsync(existing.Id, ownerType, ownerId.Value);
                    }

                    if (objectKey != null)
                        await _pmisDocumentRepository.UpdateFileAsync(existing.Id, objectKey, fileSize!.Value, item.SyncHistoryId);
                    else
                        await _pmisDocumentRepository.EnsureFilePendingAsync(existing.Id);

                    results.Add(BuildUpsertResult(item.PmisDocumentCode));
                    continue;
                }

                item.OwnerType = ownerType; // ghi đúng OwnerType đã phân giải (có thể khác giá trị gửi lên nếu resolve theo DeviceCode thành công)
                await _pmisDocumentRepository.InsertAsync(item, ownerId.Value, objectKey, fileSize);
                var created = BuildUpsertResult(item.PmisDocumentCode);
                created.WasCreated = true;
                results.Add(created);
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

    /// <summary>Lưu metadata tài liệu thành công = Success (file có sẵn hoặc đang chờ job nền tải theo mã qua DOCUMENT_FILE_DOWNLOAD —
    /// trạng thái bình thường của pha danh sách, không phải lỗi).</summary>
    private static UpsertPmisDocumentResult BuildUpsertResult(string code) =>
        new() { PmisDocumentCode = code, Success = true };

    /// <summary>Lấy các tài liệu đang chờ tải file (đã tới hạn thử lại) cho job nền của SyncService.</summary>
    [HttpGet("documents/pending-files")]
    public async Task<IActionResult> GetPendingDocumentFiles(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromQuery] int take = 40,
        [FromQuery] string? excludePrefixes = null)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        take = Math.Clamp(take, 1, 200);
        return Ok(await _pmisDocumentRepository.GetPendingFilesAsync(take, ParsePrefixes(excludePrefixes)));
    }

    /// <summary>Tóm tắt hàng đợi tải file cho watchdog của SyncService (PmisDocumentFileDownloadWatchdogJob)
    /// — phát hiện khi job tải file ngừng tiến triển mà không ai biết.</summary>
    [HttpGet("documents/pending-summary")]
    public async Task<IActionResult> GetPendingDocumentSummary(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromQuery] string? excludePrefixes = null)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        return Ok(await _pmisDocumentRepository.GetPendingSummaryAsync(ParsePrefixes(excludePrefixes)));
    }

    /// <summary>Trạm/Đường dây còn tài liệu chưa có file — SyncService dùng để backfill đồng bộ lại danh sách
    /// tài liệu (lấy link file mới) đúng cho các owner này thay vì quét cả ~40.000 owner.</summary>
    [HttpGet("documents/pending-owner-infrastructures")]
    public async Task<IActionResult> GetPendingOwnerInfrastructures(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        var rows = await _pmisDocumentRepository.GetPendingOwnerInfrastructuresAsync();
        return Ok(rows.Select(r => new { pmisCode = r.PmisCode, infraTypeId = r.InfraTypeId }));
    }

    /// <summary>Số tài liệu đã có trong DB theo từng Trạm/Đường dây (gồm tài liệu thiết bị con) — job DOCUMENT của SyncService dùng làm mốc
    /// bootstrap: owner đã đủ tài liệu so với tổng PMIS thì chỉ ghi trạng thái, không kéo lại.</summary>
    [HttpGet("documents/counts-by-infrastructure")]
    public async Task<IActionResult> GetDocumentCountsByInfrastructure(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        return Ok(await _pmisDocumentRepository.GetDocumentCountsByInfrastructureAsync());
    }

    /// <summary>Trạng thái tải file hiện tại theo danh sách mã tài liệu (tối đa 200) — màn Lịch sử đồng bộ của
    /// SyncService dùng để hiện lỗi tải file thật (FILE_LAST_ERROR) thay vì chỉ "Thành công" của pha danh sách.</summary>
    [HttpPost("documents/file-status")]
    public async Task<IActionResult> GetDocumentFileStatus(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] List<string> codes)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        var distinct = (codes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Take(200).ToList();
        return Ok(await _pmisDocumentRepository.GetFileStatusByCodesAsync(distinct));
    }

    /// <summary>Nhận kết quả tải file của job nền: có FileBase64 thì lưu MinIO + đánh dấu DONE; không thì ghi
    /// lỗi + đặt lịch thử lại theo backoff.</summary>
    [HttpPost("documents/attach-file")]
    public async Task<IActionResult> AttachDocumentFile(
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] AttachPmisDocumentFileRequest request)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (request is null || string.IsNullOrWhiteSpace(request.PmisDocumentCode))
            return BadRequest(new { message = "Thiếu PmisDocumentCode." });

        var target = await _pmisDocumentRepository.GetFileTargetByCodeAsync(request.PmisDocumentCode);
        if (target == null) return NotFound(new { message = "Không tìm thấy tài liệu." });
        if (!string.IsNullOrEmpty(target.ObjectKey)) return Ok(new { attached = false, reason = "Tài liệu đã có file." });

        if (string.IsNullOrWhiteSpace(request.FileBase64))
        {
            await _pmisDocumentRepository.MarkFileFailedAsync(target.Id, request.ErrorMessage);
            return Ok(new { attached = false });
        }

        var bytes = Convert.FromBase64String(request.FileBase64);
        using var stream = new MemoryStream(bytes);
        var (key, _) = await _fileStorageService.UploadPmisDocumentAsync(
            stream, target.DocumentName ?? target.PmisDocumentCode, "application/octet-stream", bytes.Length,
            target.OwnerType, target.OwnerId);
        await _pmisDocumentRepository.UpdateFileAsync(target.Id, key, bytes.Length, null);
        return Ok(new { attached = true });
    }

    private static IReadOnlyList<string>? ParsePrefixes(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? null : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Nhận file DẠNG LUỒNG (octet-stream) từ job tải file: ghi thẳng lên MinIO (không đệm toàn bộ vào RAM, không base64),
    /// lưu ObjectKey/FileSize/CONTENT_SHA256 + FILE_STATUS='DONE'. Bắt buộc có Content-Length (MinIO cần biết kích thước trước).</summary>
    [HttpPost("documents/{code}/file")]
    public async Task<IActionResult> UploadDocumentFile(
        string code,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromHeader(Name = "X-File-Sha256")] string? sha256,
        CancellationToken cancellationToken)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;

        var length = Request.ContentLength;
        if (length is null or <= 0) return StatusCode(411, new { message = "Thiếu Content-Length." });

        var maxBytes = _configuration.GetValue<long?>("Pmis:DocumentFile:MaxBytes") ?? 100L * 1024 * 1024;
        if (length > maxBytes) return StatusCode(413, new { message = $"File lớn hơn giới hạn {maxBytes / 1024 / 1024} MB." });
        var sizeFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = maxBytes;

        var target = await _pmisDocumentRepository.GetFileTargetByCodeAsync(code);
        if (target == null) return NotFound(new { message = "Không tìm thấy tài liệu." });
        if (!string.IsNullOrEmpty(target.ObjectKey)) return Ok(new { attached = true, alreadyHadFile = true });

        var (key, _) = await _fileStorageService.UploadPmisDocumentAsync(
            Request.Body, target.DocumentName ?? target.PmisDocumentCode, "application/octet-stream", length.Value,
            target.OwnerType, target.OwnerId, cancellationToken);
        await _pmisDocumentRepository.UpdateFileAsync(target.Id, key, length.Value, null, NormalizeSha256(sha256));
        return Ok(new { attached = true });
    }

    /// <summary>Chống trùng nội dung: nếu đã có tài liệu KHÁC với cùng SHA-256 + kích thước, gắn lại object đó cho tài liệu này
    /// (DONE) — job khỏi gửi/ghi lại file. Trả attached=false nếu chưa có file trùng (job gửi file như bình thường).
    /// An toàn vì object PMIS không bao giờ bị xoá/ghi đè khi sao chép vào hồ sơ (DossierDocumentService chỉ đọc theo ObjectKey).</summary>
    [HttpPost("documents/{code}/file-by-hash")]
    public async Task<IActionResult> AttachDocumentFileByHash(
        string code,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] DocumentFileHashRequest request)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        var sha = NormalizeSha256(request?.Sha256);
        if (sha == null || request!.Size <= 0) return BadRequest(new { message = "Thiếu/sai sha256 hoặc size." });

        var target = await _pmisDocumentRepository.GetFileTargetByCodeAsync(code);
        if (target == null) return NotFound(new { message = "Không tìm thấy tài liệu." });
        if (!string.IsNullOrEmpty(target.ObjectKey)) return Ok(new { attached = true, alreadyHadFile = true });

        var existingKey = await _pmisDocumentRepository.FindObjectKeyByHashAsync(sha, request.Size);
        if (existingKey == null) return Ok(new { attached = false });

        await _pmisDocumentRepository.UpdateFileAsync(target.Id, existingKey, request.Size, null, sha);
        return Ok(new { attached = true, deduplicated = true });
    }

    /// <summary>Ghi nhận lần tải lỗi từ job: PERMANENT (lỗi riêng của tài liệu — tính lần thử, backoff luỹ thừa) hoặc
    /// TRANSIENT (PMIS quá tải/mạng — KHÔNG tính lần thử, hẹn thử lại sau retryMinutes).</summary>
    [HttpPost("documents/{code}/file-failure")]
    public async Task<IActionResult> ReportDocumentFileFailure(
        string code,
        [FromHeader(Name = "X-Internal-Token")] string? internalToken,
        [FromBody] DocumentFileFailureRequest request)
    {
        if (!ValidateInternalToken(internalToken, out var tokenError)) return tokenError!;
        if (request is null) return BadRequest(new { message = "Thiếu nội dung." });

        var target = await _pmisDocumentRepository.GetFileTargetByCodeAsync(code);
        if (target == null) return NotFound(new { message = "Không tìm thấy tài liệu." });
        if (!string.IsNullOrEmpty(target.ObjectKey)) return Ok(new { recorded = false, reason = "Tài liệu đã có file." });

        if (string.Equals(request.Kind, "TRANSIENT", StringComparison.OrdinalIgnoreCase))
            await _pmisDocumentRepository.MarkFileTransientFailureAsync(target.Id, request.Message, request.RetryMinutes);
        else
            await _pmisDocumentRepository.MarkFileFailedAsync(target.Id, request.Message);
        return Ok(new { recorded = true });
    }

    private static string? NormalizeSha256(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return v is { Length: 64 } && v.All(Uri.IsHexDigit) ? v : null;
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
