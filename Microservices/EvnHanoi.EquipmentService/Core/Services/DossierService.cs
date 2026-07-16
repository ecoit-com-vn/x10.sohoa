using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using InfrastructureEntity = EvnHanoi.EquipmentService.Core.Entities.Infrastructure;
using GridTypeEntity = EvnHanoi.EquipmentService.Core.Entities.GridType;


namespace EvnHanoi.EquipmentService.Core.Services;

/// <summary>
/// Service hồ sơ thiết bị — encapsulate toàn bộ logic nghiệp vụ và workflow.
/// Workflow operations gọi qua HTTP tới WorkflowService (microservice riêng).
/// </summary>
public class DossierService : IDossierService
{
    private readonly IDossierRepository _dossierRepository;
    private readonly IDossierSearchRepository _dossierSearchRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IPhysicalStorageRepository _physicalStorageRepository;
    private readonly IInfrastructureRepository _infrastructureRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IDocumentTextIndexNotifier _documentTextIndexNotifier;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DossierService> _logger;

    public DossierService(
        IDossierRepository dossierRepository,
        IDossierSearchRepository dossierSearchRepository,
        IDocumentRepository documentRepository,
        IEquipmentRepository equipmentRepository,
        IPhysicalStorageRepository physicalStorageRepository,
        IInfrastructureRepository infrastructureRepository,
        IMessageProducer messageProducer,
        IDocumentTextIndexNotifier documentTextIndexNotifier,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DossierService> logger)
    {
        _dossierRepository = dossierRepository ?? throw new ArgumentNullException(nameof(dossierRepository));
        _dossierSearchRepository = dossierSearchRepository ?? throw new ArgumentNullException(nameof(dossierSearchRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _equipmentRepository = equipmentRepository ?? throw new ArgumentNullException(nameof(equipmentRepository));
        _physicalStorageRepository = physicalStorageRepository ?? throw new ArgumentNullException(nameof(physicalStorageRepository));
        _infrastructureRepository = infrastructureRepository ?? throw new ArgumentNullException(nameof(infrastructureRepository));
        _messageProducer = messageProducer ?? throw new ArgumentNullException(nameof(messageProducer));
        _documentTextIndexNotifier = documentTextIndexNotifier ?? throw new ArgumentNullException(nameof(documentTextIndexNotifier));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ====loookup ====

    public async Task<IEnumerable<InfrastructureEntity>> GetInfrastructuresLookupAsync(
        bool isAdmin,
        long? userUnitId,
        IReadOnlyList<long>? fallbackUnitIds)
    {
        List<long>? allowedUnitIds = null;
        if (!isAdmin)
        {
            if (userUnitId.HasValue)
            {
                var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId);
                allowedUnitIds = units.Select(u => u.Id).ToList();
            }
            else if (fallbackUnitIds != null && fallbackUnitIds.Count > 0)
            {
                var list = new List<long>();
                foreach (var fId in fallbackUnitIds)
                {
                    var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(fId);
                    list.AddRange(units.Select(u => u.Id));
                }
                allowedUnitIds = list.Distinct().ToList();
            }
            else
            {
                allowedUnitIds = new List<long> { -1 };
            }
        }

        return await _dossierRepository.GetInfrastructuresLookupAsync(allowedUnitIds);
    }

    /// <summary>
    /// Cây kệ → tầng → hộp chỉ theo đúng đơn vị hiện tại (không gồm đơn vị con).
    /// Không có unitId → danh sách rỗng. Sắp xếp theo Priority rồi Code.
    /// </summary>
    public async Task<IReadOnlyList<PhysicalStorageTreeShelfDto>> GetPhysicalStorageTreeAsync(long? currentUnitId)
    {
        if (currentUnitId is null or <= 0)
            return Array.Empty<PhysicalStorageTreeShelfDto>();

        var unitIds = new List<long> { currentUnitId.Value };
        var shelves = (await _physicalStorageRepository.GetShelvesAsync(unitIds)).ToList();
        var floors = (await _physicalStorageRepository.GetFloorsByUnitIdsAsync(unitIds)).ToList();
        var boxes = (await _physicalStorageRepository.GetBoxesByUnitIdsAsync(unitIds)).ToList();

        var boxesByFloor = boxes
            .GroupBy(b => b.FloorId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(b => b.Priority)
                .ThenBy(b => b.Code)
                .Select(b => new PhysicalStorageTreeBoxDto
                {
                    Id = b.Id,
                    FloorId = b.FloorId,
                    Code = b.Code,
                    Name = b.Name,
                    Priority = b.Priority
                }).ToList());

        var floorsByShelf = floors
            .GroupBy(f => f.ShelfId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(f => f.Priority)
                .ThenBy(f => f.Code)
                .Select(f => new PhysicalStorageTreeFloorDto
                {
                    Id = f.Id,
                    ShelfId = f.ShelfId,
                    Code = f.Code,
                    Name = f.Name,
                    Priority = f.Priority,
                    Boxes = boxesByFloor.TryGetValue(f.Id, out var fb) ? fb : new List<PhysicalStorageTreeBoxDto>()
                }).ToList());

        return shelves
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Code)
            .Select(s => new PhysicalStorageTreeShelfDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Priority = s.Priority,
                Floors = floorsByShelf.TryGetValue(s.Id, out var sf) ? sf : new List<PhysicalStorageTreeFloorDto>()
            })
            .ToList();
    }

    public async Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync()
    {
        return await _dossierRepository.GetGridTypesLookupAsync();
    }
    public async Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync()
    {
        return await _dossierRepository.GetDossierTypesLookupAsync();
    }

    public async Task<IEnumerable<DossierGroupDto>> GetDossierGroupsLookupAsync()
    {
        var items = await _dossierRepository.GetDossierGroupsLookupAsync();
        return items.Select(g => new DossierGroupDto
        {
            Id = g.Id,
            Code = g.Code,
            Name = g.Name,
            InfraTypeId = g.InfraTypeId,
            IsEquipmentDossier = g.IsEquipmentDossier
        });
    }

    public async Task<(IEnumerable<EquipmentLookupItemDto> Items, int TotalCount)> GetEquipmentLookupAsync(
        EquipmentLookupFilterDto filter,
        bool isAdmin,
        long? userUnitId,
        IReadOnlyList<long>? fallbackUnitIds)
    {
        if (filter.Page < 1) filter.Page = 1;
        if (filter.PageSize < 1) filter.PageSize = 10;
        if (filter.PageSize > 100) filter.PageSize = 100;

        List<long>? allowedUnitIds = null;
        if (!isAdmin)
        {
            if (userUnitId.HasValue)
            {
                var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId);
                allowedUnitIds = units.Select(u => u.Id).ToList();
            }
            else if (fallbackUnitIds != null && fallbackUnitIds.Count > 0)
            {
                var list = new List<long>();
                foreach (var fId in fallbackUnitIds)
                {
                    var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(fId);
                    list.AddRange(units.Select(u => u.Id));
                }
                allowedUnitIds = list.Distinct().ToList();
            }
            else
            {
                allowedUnitIds = new List<long> { -1 };
            }

            if (filter.UnitId.HasValue && !allowedUnitIds.Contains(filter.UnitId.Value))
            {
                return (Enumerable.Empty<EquipmentLookupItemDto>(), 0);
            }
        }

        return await _equipmentRepository.GetLookupPagedAsync(filter, allowedUnitIds);
    }

    // ===== CRUD =====

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetPagedAsync(DossierFilterDto filter)
    {
        if (filter.UnitId.HasValue)
        {
            var units = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(filter.UnitId);
            filter.UnitScopeIds = units.Select(u => u.Id).Distinct().ToList();
        }

        return await _dossierSearchRepository.GetPagedAsync(filter);
    }

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)> GetCatalogDossiersAsync(
        string? keyword,
        Guid? infrastructureId,
        Guid? dossierTypeId,
        long? unitId,
        int page,
        int pageSize)
    {
        return await _dossierRepository.GetCatalogDossiersAsync(
            keyword, infrastructureId, dossierTypeId, unitId, page, pageSize);
    }

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupInfrastructuresAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId) =>
        _dossierRepository.GetEquipmentLookupInfrastructuresAsync(filter, ResolveUnitScope(isAdmin, userUnitId));

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentTypesAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId) =>
        _dossierRepository.GetEquipmentLookupEquipmentTypesAsync(filter, ResolveUnitScope(isAdmin, userUnitId));

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupEquipmentsAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId) =>
        _dossierRepository.GetEquipmentLookupEquipmentsAsync(filter, ResolveUnitScope(isAdmin, userUnitId));

    public Task<IEnumerable<DossierByEquipmentLookupItemDto>> GetEquipmentLookupDossierTypesAsync(
        DossierByEquipmentFilterDto filter,
        bool isAdmin,
        long? userUnitId) =>
        _dossierRepository.GetEquipmentLookupDossierTypesAsync(filter, ResolveUnitScope(isAdmin, userUnitId));

    public Task<IEnumerable<BhsCatalogColumnDto>> GetBhsCatalogColumnsAsync() =>
        _dossierRepository.GetBhsCatalogColumnsAsync();

    public async Task<DossierDetailDto?> GetPublishedDetailByIdAsync(Guid id, bool isAdmin, long? userUnitId)
    {
        var unitScope = ResolveUnitScope(isAdmin, userUnitId);
        if (!await _dossierRepository.IsPublishedDossierAccessibleAsync(id, unitScope))
            return null;

        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public Task<bool> IsPublishedDossierAccessibleAsync(Guid id, bool isAdmin, long? userUnitId) =>
        _dossierRepository.IsPublishedDossierAccessibleAsync(id, ResolveUnitScope(isAdmin, userUnitId));

    private static long? ResolveUnitScope(bool isAdmin, long? userUnitId) =>
        isAdmin ? null : userUnitId;

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount, IEnumerable<BhsCatalogColumnDto> Columns)> GetDossiersByEquipmentAsync(
        Guid equipmentId,
        int page,
        int pageSize)
    {
        return await _dossierRepository.GetDossiersByEquipmentAsync(equipmentId, page, pageSize);
    }

    public async Task<DossierDetailDto?> GetDetailByIdAsync(Guid id)
    {
        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public async Task<Guid> CreateAsync(DossierCreateDto dto, string userId, string userName, string userFullName, int kindId = 2)
    {
        var equipmentIds = await ValidateAndNormalizeGroupAsync(dto.DossierGroupId, dto.InfrastructureId, dto.EquipmentIds);

        var dossier = new Dossier
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            DossierGroupId = dto.DossierGroupId,
            GridTypeId = dto.GridTypeId,
            InfrastructureId = dto.InfrastructureId,
            DossierSetId = dto.DossierSetId,
            DossierTypeId = dto.DossierTypeId,
            FormDataJson = dto.FormDataJson,
            StatusId = DossierStatusConstants.New,
            KindId = kindId,
            RowVersion = 1,
            CreatorId = string.IsNullOrEmpty(userId) ? null : Guid.TryParse(userId, out var uid) ? uid : null,
            CreatorUsername = userName,
            CreatorName = userFullName,
            CreatedBy = userName,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };
        ApplyPhysicalStorage(dossier, dto.ShelfId, dto.FloorId, dto.BoxId);

        var newId = await _dossierRepository.CreateAsync(dossier, equipmentIds);

        // Tạo phiên bản khởi đầu (v1) ngay khi hồ sơ được tạo mới
        if (!string.IsNullOrEmpty(dto.FormDataJson))
        {
            var firstVersion = new DossierVersion
            {
                DossierId = newId,
                FormDataJson = dto.FormDataJson,
                ChangeNote = "Khởi tạo hồ sơ",
                CreatedBy = userName,
                CreatedDate = DateTime.UtcNow
            };
            await _dossierRepository.CreateVersionAsync(firstVersion);
        }

        await PublishDossierChangedAsync(newId, DossierChangedActions.Created);
        return newId;
    }

    public async Task<bool> UpdateAsync(Guid id, DossierUpdateDto dto, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (!string.IsNullOrEmpty(dto.FormDataJson))
            await EnsureCanEditFormDataAsync(existing);

        var equipmentIds = await ValidateAndNormalizeGroupAsync(dto.DossierGroupId, dto.InfrastructureId, dto.EquipmentIds);

        existing.DossierGroupId = dto.DossierGroupId;
        existing.GridTypeId = dto.GridTypeId;
        existing.InfrastructureId = dto.InfrastructureId;
        existing.DossierSetId = dto.DossierSetId;
        existing.DossierTypeId = dto.DossierTypeId;
        existing.FormDataJson = dto.FormDataJson ?? existing.FormDataJson;
        existing.ModifiedBy = userId;
        existing.ModifiedDate = DateTime.UtcNow;
        existing.RowVersion = dto.RowVersion;
        ApplyPhysicalStorage(existing, dto.ShelfId, dto.FloorId, dto.BoxId);

        var updated = await _dossierRepository.UpdateAsync(existing, equipmentIds);
        if (updated)
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        return updated;
    }

    /// <summary>
    /// Validate nhóm hồ sơ / loại hạ tầng / thiết bị. Trả về danh sách EquipmentIds đã chuẩn hóa (rỗng nếu không phải HS thiết bị).
    /// </summary>
    private async Task<List<Guid>> ValidateAndNormalizeGroupAsync(
        int dossierGroupId,
        Guid? infrastructureId,
        List<Guid>? equipmentIds)
    {
        var group = await _dossierRepository.GetDossierGroupByIdAsync(dossierGroupId);
        if (group == null)
            throw new ArgumentException("Nhóm hồ sơ không hợp lệ.");

        if (infrastructureId.HasValue)
        {
            var infra = await _infrastructureRepository.GetByIdAsync(infrastructureId.Value);
            if (infra == null)
                throw new ArgumentException("Trạm / đường dây không tồn tại.");
            if (infra.InfraTypeId != group.InfraTypeId)
                throw new ArgumentException(
                    group.InfraTypeId == 1
                        ? "Nhóm hồ sơ này yêu cầu chọn trạm biến áp."
                        : "Nhóm hồ sơ này yêu cầu chọn đường dây.");
        }

        var ids = equipmentIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (group.IsEquipmentDossier)
        {
            if (ids.Count == 0)
                throw new ArgumentException("Hồ sơ thiết bị bắt buộc chọn ít nhất một thiết bị.");
            return ids;
        }

        return new List<Guid>();
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        var isDraft = existing.StatusId == DossierStatusConstants.New || existing.StatusId == DossierStatusConstants.CompletedInput;
        var notInWorkflow = !existing.WorkflowInstanceId.HasValue;
        if (!isDraft && !notInWorkflow)
            throw new InvalidOperationException("Chỉ có thể xóa hồ sơ ở trạng thái Tạo mới, Hoàn thành nhập liệu hoặc chưa đưa vào quy trình phê duyệt.");

        var deleted = await _dossierRepository.SoftDeleteAsync(id, userId);
        if (deleted)
        {
            var queued = await TryPublishDossierChangedAsync(id, DossierChangedActions.Deleted);
            if (!queued)
            {
                _logger.LogWarning(
                    "Dossier {DossierId}: không đưa được vào hàng đợi RabbitMQ, thử xóa ES đồng bộ.",
                    id);
                if (!await TrySyncDeleteFromEsAsync(id))
                {
                    _logger.LogError(
                        "Dossier {DossierId} đã xóa mềm Oracle nhưng không đồng bộ được Elasticsearch.",
                        id);
                }
            }
        }
        return deleted;
    }
    public async Task<bool> CompleteInputAsync(Guid id, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (existing.StatusId != DossierStatusConstants.New)
            throw new InvalidOperationException("Chỉ hồ sơ ở trạng thái 'Tạo mới' mới được phép xác nhận hoàn thành nhập liệu.");

        var success = await _dossierRepository.UpdateStatusAsync(id, DossierStatusConstants.CompletedInput, userId);
        if (success)
        {
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        }
        return success;
    }

    // ===== FORM DATA + VERSIONING =====

    public async Task<DossierDetailDto?> SaveFormDataAsync(Guid id, DossierSaveFormDataDto dto, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        await EnsureCanEditFormDataAsync(existing);

        // Cập nhật FormDataJson với Optimistic Locking
        await _dossierRepository.UpdateFormDataAsync(id, dto.FormDataJson, dto.RowVersion, userId);

        // Tạo snapshot version
        var version = new DossierVersion
        {
            DossierId = id,
            FormDataJson = dto.FormDataJson,
            ChangeNote = dto.ChangeNote,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        await _dossierRepository.CreateVersionAsync(version);

        await PublishDossierChangedAsync(id, DossierChangedActions.FormDataSaved);
        return await _dossierRepository.GetDetailByIdAsync(id);
    }

    public async Task<IEnumerable<DossierVersionDto>> GetVersionsAsync(Guid id)
    {
        return await _dossierRepository.GetVersionsAsync(id);
    }

    public async Task EnsureCanEditFormDataAsync(Guid dossierId)
    {
        var existing = await _dossierRepository.GetByIdAsync(dossierId);
        if (existing == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");
        await EnsureCanEditFormDataAsync(existing);
    }

    public async Task RecordDocumentListChangeAsync(Guid dossierId, string changeNote, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(dossierId);
        if (existing == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {dossierId}");

        var filter = new DossierDocumentFilterDto { Page = 1, PageSize = 1000 };
        var (items, _) = await _documentRepository.GetDocumentsByDossierAsync(dossierId, filter);
        var snapshot = System.Text.Json.JsonSerializer.Serialize(
            items.Select(d => new DossierDocumentSnapshotItemDto
            {
                Id = d.Id,
                Name = d.Name,
                FileSize = d.FileSize,
                MimeType = d.MimeType,
                LatestVersionId = d.LatestVersionId
            }));

        var version = new DossierVersion
        {
            DossierId = dossierId,
            FormDataJson = existing.FormDataJson,
            DocumentsSnapshotJson = snapshot,
            ChangeNote = changeNote,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        await _dossierRepository.CreateVersionAsync(version);
        await PublishDossierChangedAsync(dossierId, DossierChangedActions.Updated);
    }

    // ===== EQUIPMENT MANAGEMENT =====

    public async Task<IEnumerable<DossierEquipmentDto>> GetEquipmentsAsync(Guid id)
    {
        return await _dossierRepository.GetEquipmentsAsync(id);
    }

    public async Task<bool> AddEquipmentAsync(Guid id, Guid equipmentId)
    {
        var added = await _dossierRepository.AddEquipmentAsync(id, equipmentId);
        if (added)
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        return added;
    }

    public async Task<bool> RemoveEquipmentAsync(Guid id, Guid equipmentId)
    {
        var removed = await _dossierRepository.RemoveEquipmentAsync(id, equipmentId);
        if (removed)
            await PublishDossierChangedAsync(id, DossierChangedActions.Updated);
        return removed;
    }

    // ===== WORKFLOW SYNC (nhận đồng bộ từ WorkflowService qua API nội bộ) =====

    /// <summary>
    /// Cập nhật trạng thái workflow của hồ sơ theo dữ liệu do WorkflowService đẩy về.
    /// WorkflowService là bên SỞ HỮU logic suy ra DossierStatus; ES chỉ ghi nhận.
    /// </summary>
    public async Task UpdateWorkflowStateInternalAsync(Guid id, UpdateInternalWorkflowStateDto dto)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        var statusId = dto.DossierStatusId == 0 ? existing.StatusId : dto.DossierStatusId;
        var statusName = dto.WorkflowStatusName ?? existing.WorkflowStatusName ?? string.Empty;

        int? publishStatusId = null;
        if (statusId == DossierStatusConstants.Approved && (!existing.PublishStatusId.HasValue || existing.PublishStatusId == 0))
        {
            publishStatusId = 1; // Pending
        }

        await _dossierRepository.UpdateWorkflowAsync(id, dto.WorkflowInstanceId, statusName, statusId, publishStatusId, "system");

        // WF đã hoàn thành (Approved) → xóa WORKFLOW_TASKS_ACTIVE; chỉ lưu khi còn bước đang chạy
        var persistActiveTask = statusId != DossierStatusConstants.Approved
            && !string.IsNullOrWhiteSpace(dto.CurrentStepId);

        if (persistActiveTask)
        {
            var assignees = dto.CurrentAssignees != null && dto.CurrentAssignees.Any()
                ? string.Join(",", dto.CurrentAssignees)
                : string.Empty;

            var actionsJson = dto.AvailableActions != null && dto.AvailableActions.Any()
                ? System.Text.Json.JsonSerializer.Serialize(dto.AvailableActions)
                : "[]";

            await _dossierRepository.SaveActiveWorkflowTaskAsync(
                id,
                dto.CurrentStepId!,
                statusName,
                assignees,
                actionsJson,
                "system");
        }
        else
        {
            await _dossierRepository.SaveActiveWorkflowTaskAsync(
                id,
                string.Empty,
                statusName,
                string.Empty,
                "[]",
                "system");
        }

        var synced = await TrySyncReindexAsync(id);
        var queued = synced
            ? false
            : await TryPublishDossierChangedAsync(id, DossierChangedActions.WorkflowChanged);

        if (!synced && !queued)
        {
            throw new InvalidOperationException(
                "Đã cập nhật trạng thái hồ sơ trong Oracle nhưng không thể đồng bộ Elasticsearch. Vui lòng thử lại.");
        }

        if (!synced)
        {
            _logger.LogWarning(
                "Dossier {DossierId}: sync reindex thất bại, đã đưa vào hàng đợi RabbitMQ dossier_index_queue.",
                id);
        }
    }

    public async Task AutoApproveWithoutWorkflowAsync(Guid id)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (existing.StatusId != DossierStatusConstants.CompletedInput)
            throw new InvalidOperationException("Hồ sơ phải ở trạng thái 'Hoàn thành' mới được tự động phê duyệt.");

        await _dossierRepository.UpdateWorkflowAsync(
            id,
            Guid.Empty,
            "Tự động duyệt",
            DossierStatusConstants.Approved,
            DossierPublishStatusConstants.Pending,
            "system");

        await _dossierRepository.SaveActiveWorkflowTaskAsync(id, string.Empty, string.Empty, string.Empty, "[]", "system");

        var synced = await TrySyncReindexAsync(id);
        if (!synced)
            await TryPublishDossierChangedAsync(id, DossierChangedActions.WorkflowChanged);
    }

    private async Task<bool> TryPublishDossierChangedAsync(Guid dossierId, string action)
    {
        try
        {
            await PublishDossierChangedAsync(dossierId, action);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể publish sự kiện index hồ sơ {DossierId} vào RabbitMQ.", dossierId);
            return false;
        }
    }

    private async Task<bool> TrySyncDeleteFromEsAsync(Guid dossierId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"internal/v1/dossiers/{dossierId}");
            var token = _configuration["Internal:Token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError(
                    "Internal:Token chưa cấu hình — không thể gọi xóa ES đồng bộ cho hồ sơ {DossierId}.",
                    dossierId);
                return false;
            }

            request.Headers.Add("X-Internal-Token", token);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Sync delete dossier {DossierId} from ES failed ({StatusCode}): {Body}",
                    dossierId,
                    (int)response.StatusCode,
                    body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync delete dossier {DossierId} from ES failed.", dossierId);
            return false;
        }
    }

    private async Task<bool> TrySyncReindexAsync(Guid dossierId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationService");
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"internal/v1/dossiers/{dossierId}/reindex");
            var token = _configuration["Internal:Token"];
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError(
                    "Internal:Token chưa cấu hình — không thể gọi reindex đồng bộ cho hồ sơ {DossierId}.",
                    dossierId);
                return false;
            }

            request.Headers.Add("X-Internal-Token", token);

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Sync reindex dossier {DossierId} failed ({StatusCode}): {Body}",
                    dossierId,
                    (int)response.StatusCode,
                    body);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync reindex dossier {DossierId} failed.", dossierId);
            return false;
        }
    }

    /// <summary>
    /// Kiểm tra quyền sửa dữ liệu hồ sơ:
    /// - Chưa vào workflow: chỉ Draft hoặc Returned.
    /// - Đã vào workflow: bước hiện tại phải có AllowEdit = true và instance đang Running.
    /// </summary>
    private async Task EnsureCanEditFormDataAsync(Dossier dossier)
    {
        // Trả lại về bước người tạo — cho phép sửa dù instance WF vẫn đang chạy.
        if (dossier.StatusId == DossierStatusConstants.Returned)
            return;

        if (!dossier.WorkflowInstanceId.HasValue)
        {
            if (dossier.StatusId != DossierStatusConstants.New && dossier.StatusId != DossierStatusConstants.CompletedInput)
                throw new InvalidOperationException("Không thể chỉnh sửa dữ liệu hồ sơ ở trạng thái hiện tại.");
            return;
        }

        var statusDto = await _dossierRepository.GetWorkflowStatusByEntityAsync(dossier.Id.ToString());
        if (statusDto == null)
            throw new InvalidOperationException("Không thể xác minh quyền chỉnh sửa theo quy trình phê duyệt.");

        if (!string.Equals(statusDto.Status, "Running", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Quy trình phê duyệt đã kết thúc, không thể chỉnh sửa dữ liệu hồ sơ.");

        if (!statusDto.CurrentStepAllowEdit)
            throw new InvalidOperationException("Bước hiện tại của quy trình không cho phép chỉnh sửa dữ liệu hồ sơ.");
    }

    private async Task PublishDossierChangedAsync(Guid dossierId, string action)
    {
        var evt = new DossierChangedEvent(
            DossierIndexIdNormalizer.Normalize(dossierId.ToString()),
            action,
            UuidHelper.NewUuid(),
            DateTime.UtcNow);

        await _messageProducer.SendMessageAsync(evt, DossierMessaging.IndexQueue);
    }

    public async Task<bool> UpdatePublishStatusAsync(Guid id, int publishStatusId, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        if (existing.StatusId != DossierStatusConstants.Approved)
        {
            throw new InvalidOperationException("Hồ sơ chưa hoàn thành quy trình phê duyệt, không thể thay đổi trạng thái xuất bản.");
        }

        var updated = await _dossierRepository.UpdatePublishStatusAsync(id, publishStatusId, userId);
        if (updated)
        {
            var synced = await TrySyncReindexAsync(id);
            if (!synced)
            {
                await TryPublishDossierChangedAsync(id, DossierChangedActions.Updated);
            }

            try
            {
                if (publishStatusId == DossierPublishStatusConstants.Published)
                {
                    await _documentTextIndexNotifier.PublishReindexDossierDocumentsAsync(id);
                }
                else if (publishStatusId == DossierPublishStatusConstants.Unpublished)
                {
                    await _documentTextIndexNotifier.PublishDeleteDossierDocumentsAsync(id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Không đồng bộ document_index hồ sơ {DossierId} sau thay đổi trạng thái xuất bản (publishStatus={PublishStatusId}).",
                    id,
                    publishStatusId);
            }
        }
        return updated;
    }

    public async Task<EavFormTemplate?> GetFormTemplateForDossierAsync(Guid dossierId, Guid? formId)
    {
        if (formId.HasValue && formId.Value != Guid.Empty)
        {
            return await _dossierRepository.GetEavFormTemplateAsync(formId.Value);
        }

        return await _dossierRepository.GetEavFormTemplateByDossierIdAsync(dossierId);
    }

    /// <summary>
    /// Chỉ lưu kệ/tầng/hộp khi đã chọn đến hộp; ngược lại clear cả 3.
    /// </summary>
    private static void ApplyPhysicalStorage(Dossier dossier, long? shelfId, long? floorId, long? boxId)
    {
        if (boxId is null or <= 0)
        {
            dossier.ShelfId = null;
            dossier.FloorId = null;
            dossier.BoxId = null;
            return;
        }

        dossier.ShelfId = shelfId is > 0 ? shelfId : null;
        dossier.FloorId = floorId is > 0 ? floorId : null;
        dossier.BoxId = boxId;
    }
}

