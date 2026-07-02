using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.EquipmentService.Core.DTOs;
using EvnHanoi.EquipmentService.Core.Entities;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IFolderAllocationRepository
{
    Task<(IEnumerable<FolderAllocationListItemDto> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? keyword,
        string? status,
        IEnumerable<long> unitScopeIds);

    Task<FolderAllocationListItemDto?> GetByIdAsync(Guid id, IEnumerable<long> unitScopeIds);
    Task<FolderUserAllocation?> GetEntityByIdAsync(Guid id);
    Task<Guid> CreateAsync(FolderUserAllocation entity);
    Task<bool> UpdateAsync(FolderUserAllocation entity);
    Task<IEnumerable<FolderUserAllocation>> GetActiveAllocationsByUserAsync(string userId);
    Task<IEnumerable<UserLookupItemDto>> GetUsersInUnitScopeAsync(IEnumerable<long> unitScopeIds);
    Task<IEnumerable<FolderLookupItemDto>> GetFoldersInUnitScopeAsync(IEnumerable<long> unitScopeIds);
    Task<IEnumerable<FolderLookupItemDto>> GetMyAllocatedFoldersAsync(string userId);
    Task<bool> IsUserAdminAsync(string userId);
}
