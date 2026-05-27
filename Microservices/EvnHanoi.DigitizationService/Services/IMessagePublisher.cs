using System.Threading.Tasks;

namespace EvnHanoi.DigitizationService.Services
{
    public interface IMessagePublisher
    {
        Task PublishMessageAsync<T>(T message, string exchange, string routingKey);
    }
}
