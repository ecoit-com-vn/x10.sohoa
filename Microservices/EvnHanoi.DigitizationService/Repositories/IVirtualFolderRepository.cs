using System.Collections.Generic;
using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public interface IVirtualFolderRepository
    {
        Task<IEnumerable<VirtualFolder>> GetAllAsync(long? unitId = null, string? equipmentId = null);
        Task<VirtualFolder?> GetByIdAsync(long id);
        Task<long> CreateAsync(VirtualFolder folder);
        Task<bool> UpdateAsync(VirtualFolder folder);
        Task<bool> DeleteAsync(long id);
        Task AddDocumentToFolderAsync(long folderId, Guid documentId);
        Task RemoveDocumentFromFolderAsync(long folderId, Guid documentId);
        Task<IEnumerable<FileAttachment>> GetDocumentsInFolderAsync(long folderId);
        Task<IEnumerable<VirtualFolder>> GetChildFoldersAsync(long parentId);
    }
}
