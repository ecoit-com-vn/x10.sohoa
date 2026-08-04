using System;
using System.Threading.Tasks;

namespace EvnHanoi.DigitizationService.Services
{
    public interface IMessagePublisher
    {
        Task PublishMessageAsync<T>(T message, string exchange, string routingKey);

        /// <summary>
        /// Publish không ném exception — thử lại tối đa <paramref name="maxAttempts"/> lần (backoff
        /// theo <paramref name="initialDelay"/>, nhân đôi mỗi lần), trả về false nếu vẫn thất bại sau
        /// khi hết lượt thử thay vì làm hỏng luồng gọi (dùng cho các publish không được phép làm treo
        /// hoặc làm chết consumer khi RabbitMQ chập chờn thoáng qua).
        /// </summary>
        Task<bool> TryPublishMessageAsync<T>(T message, string exchange, string routingKey,
            int maxAttempts = 1, TimeSpan? initialDelay = null);
    }
}
