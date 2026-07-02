using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;
using EvnHanoi.EquipmentService.Core.Interfaces;
using EvnHanoi.Infrastructure.Database;

namespace EvnHanoi.EquipmentService.Core.Services;

public class FolderAllocationService : IFolderAllocationService
{
    private readonly IFolderAllocationRepository _folderAllocationRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IDocumentRepository _documentRepository;

    public FolderAllocationService(
        IFolderAllocationRepository folderAllocationRepository,
        IEquipmentRepository equipmentRepository,
        IDocumentRepository documentRepository)
    {
        _folderAllocationRepository = folderAllocationRepository ?? throw new ArgumentNullException(nameof(folderAllocationRepository));
        _equipmentRepository = equipmentRepository ?? throw new ArgumentNullException(nameof(equipmentRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
    }

    private async Task<List<long>> GetUnitScopeIdsAsync(long userUnitId)
    {
        var allowedUnits = await _equipmentRepository.GetOrganizationUnitsHierarchicalAsync(userUnitId);
        return allowedUnits.Select(u => u.Id).ToList();
    }

    public async Task<(IEnumerable<FolderAllocationListItemDto> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? keyword,
        string? status,
        long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);
        return await _folderAllocationRepository.GetPagedAsync(page, pageSize, keyword, status, unitScopeIds);
    }

    public async Task<FolderAllocationListItemDto?> GetByIdAsync(Guid id, long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);
        return await _folderAllocationRepository.GetByIdAsync(id, unitScopeIds);
    }

    public async Task<Guid> CreateAsync(CreateFolderAllocationRequest request, string createdBy, long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);

        // 1. Kiểm tra folder tồn tại và thuộc unit scope
        var folder = await _documentRepository.GetFolderByIdAsync(request.FolderId);
        if (folder == null)
        {
            throw new ArgumentException($"Thư mục được chọn không tồn tại.");
        }

        if (!unitScopeIds.Contains(folder.UnitId))
        {
            throw new UnauthorizedAccessException($"Thư mục '{folder.Name}' không thuộc đơn vị quản lý của bạn.");
        }

        // 2. Kiểm tra user tồn tại trong unit scope và active
        var users = await _folderAllocationRepository.GetUsersInUnitScopeAsync(unitScopeIds);
        var targetUser = users.FirstOrDefault(u => u.Id.Equals(request.UserId, StringComparison.OrdinalIgnoreCase));
        if (targetUser == null)
        {
            throw new ArgumentException($"Người xử lý không tồn tại hoặc không thuộc đơn vị quản lý của bạn.");
        }

        // 3. Kiểm tra trùng lặp bản ghi Active
        var activeAllocations = await _folderAllocationRepository.GetActiveAllocationsByUserAsync(request.UserId);
        var existing = activeAllocations.FirstOrDefault(a => a.FolderId == request.FolderId);
        if (existing != null)
        {
            throw new InvalidOperationException($"Người xử lý '{targetUser.FullName}' đã được phân bổ thư mục '{folder.Name}' và đang hoạt động.");
        }

        // 4. Tạo entity
        var entity = new FolderUserAllocation
        {
            Id = Guid.Parse(UuidHelper.NewUuid()),
            FolderId = request.FolderId,
            UserId = request.UserId,
            UnitId = folder.UnitId, // Denormalize unit_id từ folder
            Status = "Active",
            CreatedBy = createdBy,
            CreatedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        return await _folderAllocationRepository.CreateAsync(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateFolderAllocationRequest request, string modifiedBy, long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);

        // 1. Lấy thông tin phân bổ hiện tại
        var entity = await _folderAllocationRepository.GetEntityByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông tin phân bổ.");
        }

        if (!unitScopeIds.Contains(entity.UnitId))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa phân bổ của đơn vị khác.");
        }

        // 2. Kiểm tra folder mới
        var folder = await _documentRepository.GetFolderByIdAsync(request.FolderId);
        if (folder == null)
        {
            throw new ArgumentException("Thư mục được chọn không tồn tại.");
        }

        if (!unitScopeIds.Contains(folder.UnitId))
        {
            throw new UnauthorizedAccessException($"Thư mục '{folder.Name}' không thuộc đơn vị quản lý của bạn.");
        }

        // 3. Kiểm tra user mới
        var users = await _folderAllocationRepository.GetUsersInUnitScopeAsync(unitScopeIds);
        var targetUser = users.FirstOrDefault(u => u.Id.Equals(request.UserId, StringComparison.OrdinalIgnoreCase));
        if (targetUser == null)
        {
            throw new ArgumentException("Người xử lý không tồn tại hoặc không thuộc đơn vị quản lý của bạn.");
        }

        // 4. Kiểm tra trùng lặp bản ghi Active khác
        var activeAllocations = await _folderAllocationRepository.GetActiveAllocationsByUserAsync(request.UserId);
        var existing = activeAllocations.FirstOrDefault(a => a.FolderId == request.FolderId && a.Id != id);
        if (existing != null)
        {
            throw new InvalidOperationException($"Người xử lý '{targetUser.FullName}' đã được phân bổ thư mục '{folder.Name}' và đang hoạt động.");
        }

        // 5. Cập nhật thông tin (Chỉnh sửa hoặc Phân bổ lại đều map ở đây và reset Status = Active)
        entity.FolderId = request.FolderId;
        entity.UserId = request.UserId;
        entity.UnitId = folder.UnitId;
        entity.Status = "Active";
        entity.ModifiedBy = modifiedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        return await _folderAllocationRepository.UpdateAsync(entity);
    }

    public async Task<bool> RevokeAsync(Guid id, string modifiedBy, long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);

        var entity = await _folderAllocationRepository.GetEntityByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông tin phân bổ.");
        }

        if (!unitScopeIds.Contains(entity.UnitId))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền thu hồi phân bổ của đơn vị khác.");
        }

        entity.Status = "Revoked";
        entity.ModifiedBy = modifiedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        return await _folderAllocationRepository.UpdateAsync(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, string modifiedBy, long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);

        var entity = await _folderAllocationRepository.GetEntityByIdAsync(id);
        if (entity == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông tin phân bổ.");
        }

        if (!unitScopeIds.Contains(entity.UnitId))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xóa phân bổ của đơn vị khác.");
        }

        entity.IsDeleted = true;
        entity.ModifiedBy = modifiedBy;
        entity.ModifiedDate = DateTime.UtcNow;

        return await _folderAllocationRepository.UpdateAsync(entity);
    }

    public async Task<IEnumerable<UserLookupItemDto>> GetUsersLookupAsync(long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);
        return await _folderAllocationRepository.GetUsersInUnitScopeAsync(unitScopeIds);
    }

    public async Task<IEnumerable<FolderLookupItemDto>> GetFoldersLookupAsync(long userUnitId)
    {
        var unitScopeIds = await GetUnitScopeIdsAsync(userUnitId);
        return await _folderAllocationRepository.GetFoldersInUnitScopeAsync(unitScopeIds);
    }

    public async Task<IEnumerable<FolderLookupItemDto>> GetMyFoldersAsync(string userId)
    {
        // 1. Lấy tất cả các folders được phân bổ trực tiếp và đang Active
        var myAllocations = await _folderAllocationRepository.GetMyAllocatedFoldersAsync(userId);
        var allocatedIds = myAllocations.Select(a => a.Id).ToHashSet();

        if (allocatedIds.Count == 0)
        {
            return Enumerable.Empty<FolderLookupItemDto>();
        }

        // Lấy tất cả các folder của các đơn vị mà user được phân bổ
        var unitIds = myAllocations.Select(a => a.UnitId).Distinct();
        
        // Ta sẽ query toàn bộ các folder chưa bị xóa thuộc các units này
        var allFoldersInUnits = await _folderAllocationRepository.GetFoldersInUnitScopeAsync(unitIds);

        // 2. Thực hiện lọc đệ quy kế thừa thư mục con trong C#
        var parentMap = allFoldersInUnits.ToDictionary(f => f.Id, f => f.ParentId);
        var result = new List<FolderLookupItemDto>();

        bool IsDescendantOfAllocated(Guid folderId, Dictionary<Guid, Guid?> map, HashSet<Guid> allocated)
        {
            Guid? currentId = folderId;
            while (currentId.HasValue)
            {
                if (allocated.Contains(currentId.Value))
                    return true;

                if (map.TryGetValue(currentId.Value, out var parentId))
                {
                    currentId = parentId;
                }
                else
                {
                    break;
                }
            }
            return false;
        }

        foreach (var folder in allFoldersInUnits)
        {
            if (IsDescendantOfAllocated(folder.Id, parentMap, allocatedIds))
            {
                result.Add(folder);
            }
        }

        return result;
    }
}
