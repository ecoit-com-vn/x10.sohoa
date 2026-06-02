using System.Threading.Tasks;

namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IMessageProducer
{
    Task SendMessageAsync<T>(T message, string queueName);
}
