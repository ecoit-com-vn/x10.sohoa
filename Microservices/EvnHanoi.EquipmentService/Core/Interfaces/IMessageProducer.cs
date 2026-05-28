namespace EvnHanoi.EquipmentService.Core.Interfaces;

public interface IMessageProducer
{
    void SendMessage<T>(T message, string queueName);
}
