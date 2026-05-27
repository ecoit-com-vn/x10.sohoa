using System.Threading.Tasks;
using EvnHanoi.DigitizationService.Models;

namespace EvnHanoi.DigitizationService.Repositories
{
    public interface IFileAttachmentRepository
    {
        Task<int> CreateAsync(FileAttachment fileAttachment);
        Task UpdateStatusAsync(int id, string status);
    }
}
