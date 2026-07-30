namespace EvnHanoi.WorkflowService.Core.Interfaces;

public interface IMessageProducer
{
    Task SendMessageAsync<T>(T message, string queueName);
    Task PublishToExchangeAsync<T>(T message, string exchangeName, string routingKey);
}
