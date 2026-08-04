using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;
using EvnHanoi.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Data;
using Dapper;
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
    private readonly IDbConnection _dbConnection;
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
        System.Data.IDbConnection dbConnection,
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
        _dbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
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
    /// Cây kệ → tầng → hộp theo đúng đơn vị hiện tại (không gồm đơn vị con).
    /// Không có unitId (admin) → toàn bộ vị trí. Sắp xếp theo Priority rồi Code.
    /// </summary>
    public async Task<IReadOnlyList<PhysicalStorageTreeShelfDto>> GetPhysicalStorageTreeAsync(long? currentUnitId)
    {
        IReadOnlyList<long>? unitIds = currentUnitId is > 0
            ? new List<long> { currentUnitId.Value }
            : null;
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


    public async Task<IEnumerable<Guid>> GetListDocumentIdsAsync(
        string? keyword,
        Guid? infrastructureId,
        Guid? dossierTypeId,
        long? unitId,
        int page,
        int pageSize)
    {
        return await _dossierRepository.GetListDocumentIdsAsync(
        keyword,
        infrastructureId,
        dossierTypeId,
        unitId,
        page,
        pageSize);
    }
    public async Task<IEnumerable<GridTypeEntity>> GetGridTypesLookupAsync()
    {
        return await _dossierRepository.GetGridTypesLookupAsync();
    }
    public async Task<IEnumerable<DossierType>> GetDossierTypesLookupAsync()
    {
        return await _dossierRepository.GetDossierTypesLookupAsync();
    }

    public async Task<DossierCodePreviewDto> GenerateDossierCodeAsync(Guid infrastructureId, Guid dossierTypeId)
    {
        var infrastructure = await _infrastructureRepository.GetByIdAsync(infrastructureId)
            ?? throw new ArgumentException("Trạm / đường dây không tồn tại.");

        if (!infrastructure.UnitId.HasValue)
            throw new ArgumentException("Trạm / đường dây chưa được gán đơn vị quản lý.");

        var dossierType = (await _dossierRepository.GetDossierTypesLookupAsync())
            .FirstOrDefault(item => item.Id == dossierTypeId)
            ?? throw new ArgumentException("Loại hồ sơ không tồn tại hoặc không hoạt động.");

        const string unitSql = @"
            SELECT Code
            FROM ORGANIZATION_UNIT
            WHERE Id = :UnitId
              AND NVL(IsDeleted, 0) = 0";
        var unitCode = await _dbConnection.QuerySingleOrDefaultAsync<string>(unitSql, new
        {
            UnitId = infrastructure.UnitId.Value
        });

        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("Không tìm thấy mã đơn vị quản lý của trạm / đường dây.");
        if (string.IsNullOrWhiteSpace(infrastructure.Code) || string.IsNullOrWhiteSpace(dossierType.Code))
            throw new ArgumentException("Thiếu mã trạm / đường dây hoặc mã loại hồ sơ.");

        var sequence = await GetNextDossierCodeSequenceAsync(unitCode.Trim(), infrastructure, dossierType);
        return new DossierCodePreviewDto
        {
            Code = string.Join('.', unitCode.Trim(), infrastructure.Code.Trim(), dossierType.Code.Trim(), sequence)
        };
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

    public async Task<(IEnumerable<DossierListItemDto> Items, int TotalCount)>
       GetPagedAsync(DossierFilterDto filter)
    {
        // Chuẩn hóa từ khóa một lần trước khi phân nhánh repository.
        filter.Keyword = string.IsNullOrWhiteSpace(filter.Keyword)
            ? null
            : filter.Keyword.Trim();

        if (filter.UnitId.HasValue)
        {
            var units = await _equipmentRepository
                .GetOrganizationUnitsHierarchicalAsync(filter.UnitId);

            filter.UnitScopeIds = units
                .Select(unit => unit.Id)
                .Distinct()
                .ToList();
        }

        if (string.Equals(filter.Tab, "draft", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(filter.MenuScope, "creator", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(filter.UserId))
        {
            return await _dossierRepository
                .GetDraftPagedFromDbAsync(filter, filter.UserId);
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

    public Task<Guid> CreateAsync(
        DossierCreateDto dto,
        string userId,
        string userName,
        string userFullName,
        int kindId = 2)
    {
        return CreateInternalAsync(dto, userId, userName, userFullName, kindId, DossierStatusConstants.New, null);
    }
    public Task<Guid> CreateForPublishingAsync(DossierCreateDto dto, string userId, string userName, string userFullName)
    {
        return CreateInternalAsync(
            dto,
            userId,
            userName,
            userFullName,
            kindId: 2,
            statusId: DossierStatusConstants.Approved,
            publishStatusId: DossierPublishStatusConstants.Pending);
    }

    private async Task<Guid> CreateInternalAsync(
        DossierCreateDto dto,
        string userId,
        string userName,
        string userFullName,
        int kindId,
        int statusId,
        int? publishStatusId)
    {
        var infraIds = dto.InfrastructureIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (infraIds.Count == 0 && dto.InfrastructureId.HasValue && dto.InfrastructureId != Guid.Empty)
        {
            infraIds.Add(dto.InfrastructureId.Value);
        }

        var equipmentIds = await ValidateAndNormalizeGroupAsync(dto.DossierGroupId, infraIds, dto.EquipmentIds);

        var dossier = new Dossier
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            DossierGroupId = dto.DossierGroupId,
            GridTypeId = dto.GridTypeId,
            InfrastructureId = infraIds.FirstOrDefault(),
            InfrastructureIds = infraIds,
            DossierSetId = dto.DossierSetId,
            DossierTypeId = dto.DossierTypeId,
            FormDataJson = dto.FormDataJson,
            StatusId = statusId,
            PublishStatusId = publishStatusId,
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

    public Task<bool> UpdateAsync(Guid id, DossierUpdateDto dto, string userId)
    {
        return UpdateInternalAsync(id, dto, userId, allowPendingPublishEdit: false);
    }

    public Task<bool> UpdateForPublishingAsync(Guid id, DossierUpdateDto dto, string userId)
    {
        return UpdateInternalAsync(id, dto, userId, allowPendingPublishEdit: true);
    }

    private async Task<bool> UpdateInternalAsync(
        Guid id,
        DossierUpdateDto dto,
        string userId,
        bool allowPendingPublishEdit)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        var canEditPendingPublish = allowPendingPublishEdit
                                    && existing.PublishStatusId == DossierPublishStatusConstants.Pending;
        if (allowPendingPublishEdit && !canEditPendingPublish)
            throw new InvalidOperationException("Chỉ có thể chỉnh sửa hồ sơ đang ở trạng thái Chờ xuất bản.");

        if (!string.IsNullOrEmpty(dto.FormDataJson) && !canEditPendingPublish)
            await EnsureCanEditFormDataAsync(existing);

        var infraIds = dto.InfrastructureIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (infraIds.Count == 0 && dto.InfrastructureId.HasValue && dto.InfrastructureId != Guid.Empty)
        {
            infraIds.Add(dto.InfrastructureId.Value);
        }

        var equipmentIds = await ValidateAndNormalizeGroupAsync(dto.DossierGroupId, infraIds, dto.EquipmentIds);

        existing.DossierGroupId = dto.DossierGroupId;
        existing.GridTypeId = dto.GridTypeId;
        existing.InfrastructureId = infraIds.FirstOrDefault();
        existing.InfrastructureIds = infraIds;
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
        List<Guid>? infrastructureIds,
        List<Guid>? equipmentIds)
    {
        var group = await _dossierRepository.GetDossierGroupByIdAsync(dossierGroupId);
        if (group == null)
            throw new ArgumentException("Nhóm hồ sơ không hợp lệ.");

        var infraList = infrastructureIds?.Where(x => x != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (infraList.Count > 0)
        {
            var infras = new List<InfrastructureEntity>();
            foreach (var infraId in infraList)
            {
                var infra = await _infrastructureRepository.GetByIdAsync(infraId);
                if (infra == null)
                    throw new ArgumentException($"Trạm / đường dây với ID '{infraId}' không tồn tại.");
                if (infra.InfraTypeId != group.InfraTypeId)
                {
                    throw new ArgumentException(
                        group.InfraTypeId == 1
                            ? "Nhóm hồ sơ này yêu cầu chọn trạm biến áp."
                            : "Nhóm hồ sơ này yêu cầu chọn đường dây.");
                }
                infras.Add(infra);
            }

            // Quy tắc nghiệp vụ: Tất cả các trạm/đường dây trong cùng 1 hồ sơ phải thuộc CÙNG MỘT ĐƠN VỊ quản lý (UnitId)
            var unitIds = infras.Where(i => i.UnitId.HasValue).Select(i => i.UnitId!.Value).Distinct().ToList();
            if (unitIds.Count > 1)
            {
                throw new ArgumentException("Tất cả các trạm, đường dây trong cùng một hồ sơ phải thuộc cùng một đơn vị quản lý.");
            }
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

    public async Task<bool> DeleteForPublishingAsync(Guid id, string userId)
    {
        var existing = await _dossierRepository.GetByIdAsync(id);
        if (existing == null) throw new KeyNotFoundException($"Không tìm thấy hồ sơ với ID = {id}");

        var canDelete = existing.PublishStatusId == DossierPublishStatusConstants.Pending
                        || existing.PublishStatusId == DossierPublishStatusConstants.Unpublished;
        if (!canDelete)
            throw new InvalidOperationException("Chỉ có thể xóa hồ sơ ở trạng thái Chờ xuất bản hoặc Hủy xuất bản.");

        var deleted = await _dossierRepository.SoftDeleteAsync(id, userId);
        if (deleted)
            await PublishDossierChangedAsync(id, DossierChangedActions.Deleted);

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
        // Pending-publication dossiers remain editable until they are released.
        if (dossier.PublishStatusId == DossierPublishStatusConstants.Pending)
            return;

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
    public byte[] GenerateImportTemplate()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        
        // Sheet 1: Template
        var wsTemplate = workbook.Worksheets.Add("Template");
        string[] headers = { "STT", "Nhóm hồ sơ", "Loại lưới điện", "Trạm/đường dây", "Hộp lưu trữ", "Loại hồ sơ", "Thiết bị", "Ghi chú" };
        for (int i = 0; i < headers.Length; i++)
        {
            wsTemplate.Cell(1, i + 1).Value = headers[i];
        }
        var headerRange = wsTemplate.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        wsTemplate.Columns().AdjustToContents();

        // Sheet 2: Quy tắc import
        var wsRules = workbook.Worksheets.Add("Quy tắc import");
        wsRules.Cell(1, 1).Value = "Cột";
        wsRules.Cell(1, 2).Value = "Bắt buộc";
        wsRules.Cell(1, 3).Value = "Quy tắc nhập dữ liệu";
        
        var rulesHeader = wsRules.Range(1, 1, 1, 3);
        rulesHeader.Style.Font.Bold = true;
        rulesHeader.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

        string[][] ruleRows = new string[][]
        {
            new string[] { "STT", "Không", "Số thứ tự của dòng import, không bắt buộc (nếu có nhập thì hiển thị kết quả kiểm tra lỗi theo STT này)." },
            new string[] { "Nhóm hồ sơ", "Có", "Tên nhóm hồ sơ, không phân biệt hoa thường và khoảng trắng. Ví dụ: 'Nhóm hồ sơ trạm biến áp', 'Nhóm hồ sơ đường dây', 'Nhóm hồ sơ thiết bị trạm', 'Nhóm hồ sơ thiết bị đường dây'" },
            new string[] { "Loại lưới điện", "Không", "Tên loại lưới điện, không phân biệt hoa thường và khoảng trắng. Ví dụ: '110kV', '220kV'" },
            new string[] { "Trạm/đường dây", "Có", "Mã trạm biến áp hoặc mã đường dây, phân biệt hoa thường. Ví dụ: 'TBA110_HOADONG'" },
            new string[] { "Hộp lưu trữ", "Không", "Mã hộp lưu trữ vật lý, phân biệt hoa thường. Ví dụ: 'BOX-001'" },
            new string[] { "Loại hồ sơ", "Có", "Mã loại hồ sơ, phân biệt hoa thường. Ví dụ: 'LH-001'" },
            new string[] { "Thiết bị", "Bắt buộc đối với nhóm hồ sơ 3 và 4", "Mã các thiết bị ngăn cách nhau bằng dấu chấm phẩy (;), phân biệt hoa thường. Các thiết bị phải thuộc Trạm/đường dây đã chọn. Ví dụ: 'TB-001;TB-002'" },
            new string[] { "Ghi chú", "Không", "Ghi chú thêm cho hồ sơ, thông tin này được lưu vào trường dữ liệu động với key là 'NOTE'. Ví dụ: 'Hồ sơ lắp đặt bổ sung'" },
            new string[] { "Mã hồ sơ", "Tự sinh", "Hệ thống tự sinh khi import: <Mã đơn vị>.<Mã trạm/đường dây>.<Mã loại hồ sơ>.<Số thứ tự>. Số thứ tự tự tăng theo tổ hợp ba mã trên và có 3 chữ số (ví dụ: 001)." },
            new string[] { "Tiêu đề hồ sơ", "Tự sinh", "Hệ thống tự sinh khi import: <Tên loại hồ sơ> <Tên trạm/đường dây>." }
        };

        for (int r = 0; r < ruleRows.Length; r++)
        {
            wsRules.Cell(r + 2, 1).Value = ruleRows[r][0];
            wsRules.Cell(r + 2, 2).Value = ruleRows[r][1];
            wsRules.Cell(r + 2, 3).Value = ruleRows[r][2];
        }

        var allRulesRange = wsRules.Range(1, 1, ruleRows.Length + 1, 3);
        allRulesRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        allRulesRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        
        wsRules.Columns().AdjustToContents();

        using var stream = new System.IO.MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string NormalizeForMatching(string? val)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        return val.Replace(" ", "").ToLowerInvariant();
    }

    public async Task<DossierImportResultDto> ImportDossiersAsync(
        System.IO.Stream excelStream,
        string userId,
        string userName,
        string userFullName,
        int kindId)
    {
        var result = new DossierImportResultDto();
        
        using var workbook = new ClosedXML.Excel.XLWorkbook(excelStream);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1).ToList();
        if (rows.Count == 0)
        {
            return result;
        }

        var infraCodes = new HashSet<string>(StringComparer.Ordinal);
        var boxCodes = new HashSet<string>(StringComparer.Ordinal);
        var equipCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var infraCodeText = row.Cell(4).GetString()?.Trim();
            var boxCodeText = row.Cell(5).GetString()?.Trim();
            var equipCodesText = row.Cell(7).GetString()?.Trim();

            if (!string.IsNullOrEmpty(infraCodeText))
                infraCodes.Add(infraCodeText);
            if (!string.IsNullOrEmpty(boxCodeText))
                boxCodes.Add(boxCodeText);
            if (!string.IsNullOrEmpty(equipCodesText))
            {
                var parts = equipCodesText.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        equipCodes.Add(trimmed);
                }
            }
        }

        // Bulk load Infrastructures
        var infras = new Dictionary<string, InfrastructureEntity>(StringComparer.Ordinal);
        if (infraCodes.Count > 0)
        {
            var sql = "SELECT Id, Code, Name, INFRA_TYPE_ID as InfraTypeId, GRIDTYPEID as GridTypeId, UNIT_ID as UnitId, IsDeleted FROM INFRASTRUCTURE WHERE CODE IN :Codes AND NVL(IsDeleted, 0) = 0";
            var list = await _dbConnection.QueryAsync<InfrastructureEntity>(sql, new { Codes = infraCodes.ToArray() });
            foreach (var x in list)
            {
                if (!infras.ContainsKey(x.Code))
                    infras.Add(x.Code, x);
            }
        }

        var unitCodes = new Dictionary<long, string>();
        var unitIds = infras.Values
            .Where(infra => infra.UnitId.HasValue)
            .Select(infra => infra.UnitId!.Value)
            .Distinct()
            .ToArray();
        if (unitIds.Length > 0)
        {
            var sql = @"SELECT Id, Code
                        FROM ORGANIZATION_UNIT
                        WHERE Id IN :Ids
                          AND NVL(IsDeleted, 0) = 0";
            var units = await _dbConnection.QueryAsync<OrganizationDto>(sql, new { Ids = unitIds });
            foreach (var unit in units)
            {
                if (!string.IsNullOrWhiteSpace(unit.Code))
                    unitCodes[unit.Id] = unit.Code.Trim();
            }
        }

        // Bulk load Boxes, Floors, Shelves
        var boxes = new Dictionary<string, PhysicalBox>(StringComparer.Ordinal);
        var floorIds = new HashSet<long>();
        if (boxCodes.Count > 0)
        {
            var sql = "SELECT * FROM PHYSICAL_BOX WHERE CODE IN :Codes AND NVL(IS_DELETED, 0) = 0";
            var list = await _dbConnection.QueryAsync<PhysicalBox>(sql, new { Codes = boxCodes.ToArray() });
            foreach (var x in list)
            {
                if (!boxes.ContainsKey(x.Code))
                {
                    boxes.Add(x.Code, x);
                    floorIds.Add(x.FloorId);
                }
            }
        }

        var floors = new Dictionary<long, PhysicalFloor>();
        var shelfIds = new HashSet<long>();
        if (floorIds.Count > 0)
        {
            var sql = "SELECT * FROM PHYSICAL_FLOOR WHERE ID IN :Ids AND NVL(IS_DELETED, 0) = 0";
            var list = await _dbConnection.QueryAsync<PhysicalFloor>(sql, new { Ids = floorIds.ToArray() });
            floors = list.ToDictionary(f => f.Id);
            foreach (var f in list)
            {
                shelfIds.Add(f.ShelfId);
            }
        }

        var shelves = new Dictionary<long, PhysicalShelf>();
        if (shelfIds.Count > 0)
        {
            var sql = "SELECT * FROM PHYSICAL_SHELF WHERE ID IN :Ids AND NVL(IS_DELETED, 0) = 0";
            var list = await _dbConnection.QueryAsync<PhysicalShelf>(sql, new { Ids = shelfIds.ToArray() });
            shelves = list.ToDictionary(s => s.Id);
        }

        // Bulk load Equipments
        var equipments = new Dictionary<string, Equipment>(StringComparer.Ordinal);
        if (equipCodes.Count > 0)
        {
            var sql = "SELECT Id, Code, Name, INFRASTRUCTURE_ID as InfrastructureId, IsDeleted FROM EQUIPMENTS WHERE CODE IN :Codes AND NVL(IsDeleted, 0) = 0";
            var list = await _dbConnection.QueryAsync<Equipment>(sql, new { Codes = equipCodes.ToArray() });
            foreach (var x in list)
            {
                if (!equipments.ContainsKey(x.Code))
                    equipments.Add(x.Code, x);
            }
        }

        // Load lookups
        var groups = (await GetDossierGroupsLookupAsync()).ToList();
        var gridTypes = (await GetGridTypesLookupAsync()).ToList();
        var dossierTypes = (await _dossierRepository.GetDossierTypesLookupAsync()).ToList();

        foreach (var row in rows)
        {
            var rowIndex = row.RowNumber();
            var sttText = row.Cell(1).GetString()?.Trim();
            var groupText = row.Cell(2).GetString()?.Trim();
            var gridTypeText = row.Cell(3).GetString()?.Trim();
            var infraCodeText = row.Cell(4).GetString()?.Trim();
            var boxCodeText = row.Cell(5).GetString()?.Trim();
            var dossierTypeCodeText = row.Cell(6).GetString()?.Trim();
            var equipCodesText = row.Cell(7).GetString()?.Trim();
            var noteText = row.Cell(8).GetString()?.Trim();

            var rowResult = new ImportRowResultDto
            {
                RowIndex = rowIndex,
                STT = sttText,
                DossierGroupName = groupText,
                GridTypeName = gridTypeText,
                InfrastructureCode = infraCodeText,
                StorageBoxCode = boxCodeText,
                DossierTypeCode = dossierTypeCodeText,
                EquipmentCodes = equipCodesText,
                Note = noteText
            };

            var errors = new List<string>();

            // 1. Nhóm hồ sơ
            if (string.IsNullOrEmpty(groupText))
            {
                errors.Add("Nhóm hồ sơ không được để trống.");
            }
            var group = string.IsNullOrEmpty(groupText)
                ? null
                : groups.FirstOrDefault(g => NormalizeForMatching(g.Name) == NormalizeForMatching(groupText));
            if (!string.IsNullOrEmpty(groupText) && group == null)
            {
                errors.Add($"Nhóm hồ sơ '{groupText}' không tồn tại.");
            }

            // 2. Loại lưới điện
            GridTypeEntity? gridType = null;
            if (!string.IsNullOrEmpty(gridTypeText))
            {
                gridType = gridTypes.FirstOrDefault(g => NormalizeForMatching(g.Name) == NormalizeForMatching(gridTypeText));
                if (gridType == null)
                {
                    errors.Add($"Loại lưới điện '{gridTypeText}' không tồn tại.");
                }
            }

            // 3. Trạm/đường dây
            InfrastructureEntity? infra = null;
            if (string.IsNullOrEmpty(infraCodeText))
            {
                errors.Add("Trạm/đường dây không được để trống.");
            }
            else
            {
                infras.TryGetValue(infraCodeText, out infra);
                if (infra == null || infra.Code != infraCodeText)
                {
                    errors.Add($"Trạm/đường dây với mã '{infraCodeText}' không tồn tại.");
                    infra = null;
                }
                else if (group != null)
                {
                    var expectedInfraTypeId = DossierGroupConstants.ResolveInfraTypeId(group.Id);
                    if (infra.InfraTypeId != expectedInfraTypeId)
                    {
                        errors.Add(expectedInfraTypeId == 1
                            ? $"Nhóm hồ sơ '{groupText}' yêu cầu chọn trạm biến áp, nhưng '{infraCodeText}' là đường dây."
                            : $"Nhóm hồ sơ '{groupText}' yêu cầu chọn đường dây, nhưng '{infraCodeText}' là trạm biến áp.");
                    }
                }
            }

            // 4. Hộp lưu trữ
            long? shelfId = null;
            long? floorId = null;
            long? boxId = null;
            if (!string.IsNullOrEmpty(boxCodeText))
            {
                boxes.TryGetValue(boxCodeText, out var box);
                if (box == null || box.Code != boxCodeText)
                {
                    errors.Add($"Hộp lưu trữ với mã '{boxCodeText}' không tồn tại.");
                }
                else
                {
                    boxId = box.Id;
                    floorId = box.FloorId;
                    if (floors.TryGetValue(box.FloorId, out var floor))
                    {
                        shelfId = floor.ShelfId;
                    }
                }
            }

            // 5. Loại hồ sơ
            DossierType? dossierType = null;
            if (string.IsNullOrEmpty(dossierTypeCodeText))
            {
                errors.Add("Loại hồ sơ không được để trống.");
            }
            else
            {
                dossierType = dossierTypes.FirstOrDefault(d => d.Code == dossierTypeCodeText);
                if (dossierType == null)
                {
                    errors.Add($"Loại hồ sơ với mã '{dossierTypeCodeText}' không tồn tại.");
                }
            }

            string? unitCode = null;
            if (infra?.UnitId.HasValue == true)
                unitCodes.TryGetValue(infra.UnitId.Value, out unitCode);
            if (infra != null && string.IsNullOrWhiteSpace(unitCode))
                errors.Add($"Không tìm thấy mã đơn vị quản lý của trạm/đường dây '{infraCodeText}'.");

            // 6. Thiết bị
            var listEquipIds = new List<Guid>();
            if (group != null)
            {
                var isEquipDossier = DossierGroupConstants.IsEquipmentDossierId(group.Id);
                if (isEquipDossier && string.IsNullOrEmpty(equipCodesText))
                {
                    errors.Add("Hồ sơ thiết bị bắt buộc chọn ít nhất một thiết bị.");
                }
                else if (!string.IsNullOrEmpty(equipCodesText))
                {
                    var codeList = equipCodesText.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToList();
                    
                    if (isEquipDossier && codeList.Count == 0)
                    {
                        errors.Add("Hồ sơ thiết bị bắt buộc chọn ít nhất một thiết bị.");
                    }

                    foreach (var code in codeList)
                    {
                        equipments.TryGetValue(code, out var equip);
                        if (equip == null || equip.Code != code)
                        {
                            errors.Add($"Thiết bị với mã '{code}' không tồn tại.");
                        }
                        else
                        {
                            listEquipIds.Add(equip.Id);
                            if (infra != null && equip.InfrastructureId != infra.Id)
                            {
                                errors.Add($"Thiết bị '{code}' không thuộc trạm/đường dây '{infraCodeText}'.");
                            }
                        }
                    }
                }
            }

            if (errors.Count > 0)
            {
                rowResult.ErrorMessage = string.Join(" ", errors);
                result.FailedDossiers.Add(rowResult);
            }
            else
            {
                try
                {
                    var sequence = await GetNextDossierCodeSequenceAsync(
                        unitCode!,
                        infra!,
                        dossierType!);
                    var dossierCode = string.Join('.', unitCode, infra!.Code, dossierType!.Code, sequence);
                    var dossierTitle = $"{dossierType.Name} {infra.Name}";

                    // Chuẩn bị FormDataJson chứa các trường tự sinh và ghi chú.
                    var formDataObj = new Dictionary<string, object>();
                    formDataObj.Add("CODE", dossierCode);
                    formDataObj.Add("NAME", dossierTitle);
                    if (!string.IsNullOrEmpty(noteText))
                    {
                        formDataObj.Add("NOTE", noteText);
                    }
                    var formDataJson = System.Text.Json.JsonSerializer.Serialize(formDataObj);

                    // Tạo hồ sơ
                    var dossier = new Dossier
                    {
                        Id = Guid.Parse(UuidHelper.NewUuid()),
                        DossierGroupId = group!.Id,
                        GridTypeId = gridType?.Id ?? infra?.GridTypeId,
                        InfrastructureId = infra?.Id,
                        DossierSetId = null,
                        DossierTypeId = dossierType!.Id,
                        FormDataJson = formDataJson,
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
                    ApplyPhysicalStorage(dossier, shelfId, floorId, boxId);

                    var newId = await _dossierRepository.CreateAsync(dossier, listEquipIds);

                    // Phiên bản khởi đầu v1
                    var firstVersion = new DossierVersion
                    {
                        DossierId = newId,
                        FormDataJson = formDataJson,
                        ChangeNote = "Khởi tạo hồ sơ bằng Import Excel",
                        CreatedBy = userName,
                        CreatedDate = DateTime.UtcNow
                    };
                    await _dossierRepository.CreateVersionAsync(firstVersion);

                    // Sự kiện thay đổi
                    await PublishDossierChangedAsync(newId, DossierChangedActions.Created);

                    rowResult.CreatedDossierId = newId;
                    result.SuccessDossiers.Add(rowResult);
                }
                catch (Exception ex)
                {
                    rowResult.ErrorMessage = $"Lỗi hệ thống khi lưu hồ sơ: {ex.Message}";
                    result.FailedDossiers.Add(rowResult);
                }
            }
        }

        return result;
    }

    private async Task<string> GetNextDossierCodeSequenceAsync(
        string unitCode,
        InfrastructureEntity infrastructure,
        DossierType dossierType)
    {
        var codePrefix = string.Join('.', unitCode, infrastructure.Code, dossierType.Code);
        const string sql = @"
            SELECT NVL(MAX(
                CASE
                    WHEN SUBSTR(
                        JSON_VALUE(d.FormDataJson, '$.CODE' RETURNING VARCHAR2(4000) NULL ON ERROR),
                        1,
                        LENGTH(:CodePrefix) + 1) = :CodePrefix || '.'
                    AND REGEXP_LIKE(
                        JSON_VALUE(d.FormDataJson, '$.CODE' RETURNING VARCHAR2(4000) NULL ON ERROR),
                        '\.[0-9]+$')
                    THEN TO_NUMBER(REGEXP_SUBSTR(
                        JSON_VALUE(d.FormDataJson, '$.CODE' RETURNING VARCHAR2(4000) NULL ON ERROR),
                        '[0-9]+$'))
                END), 0)
            FROM DOSSIERS d
            WHERE d.IsDeleted = 0
              AND d.InfrastructureId = :InfrastructureId
              AND d.DossierTypeId = :DossierTypeId";

        var latestSequence = await _dbConnection.ExecuteScalarAsync<int>(sql, new
        {
            CodePrefix = codePrefix,
            InfrastructureId = infrastructure.Id.ToString(),
            DossierTypeId = dossierType.Id.ToString()
        });

        if (latestSequence == int.MaxValue)
            throw new InvalidOperationException($"Đã hết số thứ tự cho mã hồ sơ '{codePrefix}'.");

        return (latestSequence + 1).ToString("D3");
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

