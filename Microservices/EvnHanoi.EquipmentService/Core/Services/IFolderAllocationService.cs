using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Services;

public interface IFolderAllocationService
{
    Task<(IEnumerable<FolderAllocationListItemDto> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? keyword,
        string? status,
        long userUnitId);

    Task<FolderAllocationListItemDto?> GetByIdAsync(Guid id, long userUnitId);

    Task<Guid> CreateAsync(CreateFolderAllocationRequest request, string createdBy, long userUnitId);

    Task<bool> UpdateAsync(Guid id, UpdateFolderAllocationRequest request, string modifiedBy, long userUnitId);

    Task<bool> RevokeAsync(Guid id, string modifiedBy, long userUnitId);
    Task<bool> ReactivateAsync(Guid id, string modifiedBy, long userUnitId);

    Task<bool> DeleteAsync(Guid id, string modifiedBy, long userUnitId);

    Task<IEnumerable<UserLookupItemDto>> GetUsersLookupAsync(long userUnitId);

    Task<IEnumerable<FolderLookupItemDto>> GetFoldersLookupAsync(long userUnitId);

    Task<IEnumerable<FolderLookupItemDto>> GetMyFoldersAsync(string userId);
}
