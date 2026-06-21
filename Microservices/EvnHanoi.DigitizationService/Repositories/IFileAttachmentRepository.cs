using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public interface IFileAttachmentRepository
    {
        Task<Guid> CreateAsync(FileAttachment fileAttachment);
        Task UpdateStatusAsync(Guid id, string status);
    }
}
