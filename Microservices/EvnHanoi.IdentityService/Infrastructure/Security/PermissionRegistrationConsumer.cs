using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EvnHanoi.IdentityService.Infrastructure.Security;

public class PermissionRegistrationConsumer : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionRegistrationConsumer> _logger;
    private const string QueueName = "identity_permission_registration_queue";

    public PermissionRegistrationConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<PermissionRegistrationConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IChannel channel;
        try
        {
            channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            _logger.LogInformation("📥 PermissionRegistrationConsumer: Đang lắng nghe trên queue '{QueueName}'...", QueueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Lỗi khởi tạo kênh nhận tin RabbitMQ.");
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageString = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<PermissionRegistrationMessage>(messageString);
                if (message != null)
                {
                    _logger.LogInformation("📥 Nhận được yêu cầu đồng bộ {Count} quyền từ dịch vụ '{ServiceName}'...", 
                        message.Permissions.Count, message.ServiceName);

                    await ProcessPermissionsAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi xử lý bản tin đăng ký phân quyền.");
            }
            finally
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Giữ background service chạy cho đến khi bị dừng
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        await channel.CloseAsync();
    }

    private async Task ProcessPermissionsAsync(PermissionRegistrationMessage message)
    {
        using var scope = _scopeFactory.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<IDbConnection>();

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();

        try
        {
            // Lấy ID của vai trò ADMIN
            var adminRoleId = await connection.QuerySingleOrDefaultAsync<long?>(
                "SELECT Id FROM ROLE WHERE Code = 'ADMIN'", transaction: transaction);

            if (!adminRoleId.HasValue)
            {
                _logger.LogWarning("⚠️ Không tìm thấy vai trò ADMIN trong cơ sở dữ liệu. Bỏ qua liên kết quyền cho ADMIN.");
            }

            int permissionInserted = 0;
            int detailInserted = 0;
            int roleMapped = 0;

            foreach (var permDto in message.Permissions)
            {
                // 1. Đồng bộ PERMISSION
                var permId = GenerateDeterministicGuid("PERM_" + permDto.Code);
                var existsPerm = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM PERMISSION WHERE Code = :Code", 
                    new { Code = permDto.Code }, 
                    transaction: transaction);

                if (existsPerm == 0)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO PERMISSION (Id, Code, Name, Description, IsActive, CreatedBy)
                        VALUES (:Id, :Code, :Name, :Description, 1, 'SYSTEM')",
                        new { Id = permId, Code = permDto.Code, Name = permDto.Name, Description = permDto.Description },
                        transaction: transaction);
                    permissionInserted++;
                }
                else
                {
                    // Lấy ID thực tế từ database
                    permId = await connection.QuerySingleAsync<string>(
                        "SELECT Id FROM PERMISSION WHERE Code = :Code", 
                        new { Code = permDto.Code }, 
                        transaction: transaction);
                }

                // 2. Đồng bộ PERMISSION_DETAIL
                foreach (var detailDto in permDto.Details)
                {
                    var detailId = GenerateDeterministicGuid("DETAIL_" + detailDto.ControllerName + "_" + detailDto.ActionName + "_" + permDto.Code);
                    var existsDetail = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM PERMISSION_DETAIL WHERE ControllerName = :ControllerName AND ActionName = :ActionName AND PermissionId = :PermissionId",
                        new { ControllerName = detailDto.ControllerName, ActionName = detailDto.ActionName, PermissionId = permId },
                        transaction: transaction);

                    if (existsDetail == 0)
                    {
                        await connection.ExecuteAsync(@"
                            INSERT INTO PERMISSION_DETAIL (Id, PermissionId, ControllerName, ActionName)
                            VALUES (:Id, :PermissionId, :ControllerName, :ActionName)",
                            new { Id = detailId, PermissionId = permId, ControllerName = detailDto.ControllerName, ActionName = detailDto.ActionName },
                            transaction: transaction);
                        detailInserted++;
                    }
                }

                // 3. Tự động gán quyền mới vào vai trò ADMIN
                if (adminRoleId.HasValue)
                {
                    var existsRolePerm = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM ROLE_PERMISSION WHERE RoleId = :RoleId AND PermissionId = :PermissionId",
                        new { RoleId = adminRoleId.Value, PermissionId = permId },
                        transaction: transaction);

                    if (existsRolePerm == 0)
                    {
                        var rolePermId = Guid.NewGuid().ToString(); // sử dụng ngẫu nhiên hoặc UUIDv7
                        await connection.ExecuteAsync(@"
                            INSERT INTO ROLE_PERMISSION (Id, RoleId, PermissionId)
                            VALUES (:Id, :RoleId, :PermissionId)",
                            new { Id = rolePermId, RoleId = adminRoleId.Value, PermissionId = permId },
                            transaction: transaction);
                        roleMapped++;
                    }
                }
            }

            transaction.Commit();

            _logger.LogInformation("✅ Đồng bộ thành công dịch vụ '{ServiceName}': " +
                                "Đã chèn {PermCount} quyền PERMISSION mới, " +
                                "{DetailCount} chi tiết PERMISSION_DETAIL, " +
                                "gán {RoleCount} quyền mới cho ADMIN.",
                message.ServiceName, permissionInserted, detailInserted, roleMapped);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "❌ Lỗi rollback khi đồng bộ phân quyền từ '{ServiceName}'.", message.ServiceName);
        }
    }

    private string GenerateDeterministicGuid(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            byte[] guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes).ToString();
        }
    }
}

public class PermissionRegistrationMessage
{
    public string ServiceName { get; set; } = string.Empty;
    public List<PermissionDto> Permissions { get; set; } = new();
}

public class PermissionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<PermissionDetailDto> Details { get; set; } = new();
}

public class PermissionDetailDto
{
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
}
