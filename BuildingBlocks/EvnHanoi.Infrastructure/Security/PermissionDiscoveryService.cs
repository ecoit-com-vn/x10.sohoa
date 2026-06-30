using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EvnHanoi.Infrastructure.Security;

public class PermissionDiscoveryService : BackgroundService
{
    private readonly IApiDescriptionGroupCollectionProvider _apiExplorer;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionDiscoveryService> _logger;
    private readonly string _serviceName;

    public PermissionDiscoveryService(
        IApiDescriptionGroupCollectionProvider apiExplorer,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        IServiceProvider serviceProvider,
        ILogger<PermissionDiscoveryService> logger,
        string serviceName)
    {
        _apiExplorer = apiExplorer;
        _configuration = configuration;
        _lifetime = lifetime;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serviceName = serviceName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource();
        using var registration = _lifetime.ApplicationStarted.Register(() => tcs.SetResult());

        try
        {
            // Chờ ứng dụng ASP.NET Core khởi động hoàn tất để các route API được nạp đầy đủ
            await Task.WhenAny(tcs.Task, Task.Delay(-1, stoppingToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chờ ứng dụng khởi chạy.");
            return;
        }

        if (stoppingToken.IsCancellationRequested) return;

        try
        {
            _logger.LogInformation("🚀 Bắt đầu quét các API endpoints để tự động phân quyền cho service '{ServiceName}'...", _serviceName);

            var apiEndpoints = _apiExplorer.ApiDescriptionGroups.Items
                .SelectMany(group => group.Items)
                .Where(api => api.ActionDescriptor.RouteValues.ContainsKey("controller"))
                .ToList();

            _logger.LogInformation("🔍 Tìm thấy {Count} API endpoints thô trong '{ServiceName}'.", apiEndpoints.Count, _serviceName);

            var permissions = DiscoveredPermissions(apiEndpoints);

            if (!permissions.Any())
            {
                _logger.LogInformation("ℹ️ Không tìm thấy quyền nào cần đồng bộ cho service '{ServiceName}'.", _serviceName);
                return;
            }

            var message = new PermissionRegistrationMessage
            {
                ServiceName = _serviceName,
                Permissions = permissions
            };

            await PublishPermissionsAsync(message);

            _logger.LogInformation("✅ Đã gửi thành công {Count} quyền của '{ServiceName}' qua RabbitMQ.", permissions.Count, _serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Lỗi xảy ra trong quá trình quét và đồng bộ phân quyền cho '{ServiceName}'.", _serviceName);
        }
    }

    private List<PermissionDto> DiscoveredPermissions(List<ApiDescription> endpoints)
    {
        var result = new List<PermissionDto>();

        // Overrides tương thích ngược với dữ liệu tĩnh trong DB/Menu
        var friendlyResourceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Users", "Người dùng" },
            { "Roles", "Vai trò hệ thống" },
            { "Menus", "Thực đơn điều hướng (Menu)" },
            { "OrganizationUnits", "Đơn vị thành viên" },
            { "UserGroups", "Nhóm người dùng" },
            { "AuditLog", "Nhật ký hoạt động" },
            { "SystemParams", "Tham số hệ thống" },
            { "UploadConfigs", "Cấu hình tải lên" },
            { "Permissions", "Quản lý quyền hạt mịn" },
            { "Signatures", "Chữ ký số" },
            { "Box", "Hộp hồ sơ" },
            { "BorrowRecord", "Yêu cầu mượn trả hồ sơ" },
            { "Catalog", "Danh mục chung" },
            { "Dossier", "Hồ sơ thiết bị" },
            { "DossierPublish", "Xuất bản hồ sơ" },
            { "DossierCatalog", "Danh mục hồ sơ" },
            { "DossierSet", "Bộ hồ sơ" },
            { "DossierType", "Loại hồ sơ" },
            { "Document", "Văn bản tài liệu" },
            { "DocumentType", "Loại văn bản" },
            { "Digitization", "Hiệu đính số hóa OCR" },
            { "DigitizationTask", "Nhiệm vụ số hóa OCR" },
            { "Domain", "Lĩnh vực" },
            { "DynamicReport", "Báo cáo động" },
            { "EavFormTemplate", "Form" },
            { "Equipment", "Thiết bị lưới điện" },
            { "EquipmentType", "Loại thiết bị" },
            { "Floor", "Tầng lưu trữ" },
            { "FormTemplate", "Biểu mẫu" },
            { "Notifications", "Thông báo" },
            { "OcrTrainingData", "Dữ liệu huấn luyện AI OCR" },
            { "PhysicalStatus", "Tình trạng vật lý" },
            { "PhysicalStorage", "Kho lưu trữ vật lý" },
            { "Position", "Chức vụ" },
            { "PrivateCatalog", "Danh mục dùng riêng" },
            { "Report", "Báo cáo" },
            { "ReportGroup", "Nhóm báo cáo" },
            { "SharedCatalog", "Danh mục dùng chung" },
            { "Shelf", "Kệ lưu trữ" },
            { "Substation", "Trạm biến áp" },
            { "Sync", "Đồng bộ dữ liệu" },
            { "TransmissionLine", "Đường dây truyền tải" },
            { "VirtualFolder", "Thư mục ảo (Explorer)" },
            { "Workflow", "Quy trình hồ sơ" },
            { "WorkflowDefinitions", "Thiết lập quy trình" },
            { "DossierWorkflow", "Phê duyệt hồ sơ"},

        };

        var friendlyActionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "VIEW", "Xem" },
            { "CREATE", "Thêm mới" },
            { "EDIT", "Chỉnh sửa" },
            { "DELETE", "Xóa" },
            { "IMPORT", "Nhập tệp (Import)" },
            { "EXPORT", "Xuất tệp (Export)" },
            { "MANAGE", "Quản lý chuyên sâu" },
            { "RELEASE", "Xuất bản" },
        };

        // Group endpoints theo controller
        var endpointsByController = endpoints
            .GroupBy(api => api.ActionDescriptor.RouteValues["controller"])
            .Where(g => !string.IsNullOrEmpty(g.Key) && !g.Key.Equals("Dev", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var controllerGroup in endpointsByController)
        {
            var controllerKey = controllerGroup.Key!;
            var controllerName = controllerKey + "Controller";

            // Kiểm tra xem Controller có bị bỏ qua quét hoàn toàn không
            var controllerDescriptor = controllerGroup.First().ActionDescriptor as ControllerActionDescriptor;
            var controllerType = controllerDescriptor?.ControllerTypeInfo;

            if (controllerType != null)
            {
                if (controllerType.GetCustomAttribute<BypassPermissionScanAttribute>() != null ||
                    controllerType.GetCustomAttribute("BypassDynamicPermissionAttribute") != null ||
                    controllerType.GetCustomAttribute("WorkflowEngineApiAttribute") != null)
                {
                    _logger.LogInformation("🚫 Bỏ qua quét phân quyền cho Controller '{ControllerName}' do có attribute bỏ qua.", controllerName);
                    continue;
                }
            }

            string resourceBase = PermissionCodeResolver.GetResourceBase(controllerKey);
            string resourceFriendly = friendlyResourceNames.TryGetValue(controllerKey, out var val) ? val : controllerKey;

            // Thu thập các endpoints/actions hợp lệ
            var actionsList = new List<(string ActionName, string HttpMethod, string Category, string PermCode)>();

            foreach (var api in controllerGroup)
            {
                var actionName = api.ActionDescriptor.RouteValues["action"] ?? "Unknown";
                var httpMethod = api.HttpMethod ?? "GET";

                // Kiểm tra xem Action cụ thể có bị bỏ qua quét không
                var actionDescriptor = api.ActionDescriptor as ControllerActionDescriptor;
                var methodInfo = actionDescriptor?.MethodInfo;

                if (methodInfo != null)
                {
                    if (methodInfo.GetCustomAttribute<BypassPermissionScanAttribute>() != null ||
                        methodInfo.GetCustomAttribute("BypassDynamicPermissionAttribute") != null ||
                        methodInfo.GetCustomAttribute("WorkflowEngineApiAttribute") != null)
                    {
                        _logger.LogDebug("🚫 Bỏ qua quét Action '{ControllerName}.{ActionName}' do có attribute bỏ qua.", controllerName, actionName);
                        continue;
                    }
                }

                string category = PermissionCodeResolver.CategorizeAction(controllerKey, actionName, httpMethod);
                string permissionCode = PermissionCodeResolver.BuildPermissionCode(controllerKey, category, resourceBase);

                actionsList.Add((actionName, httpMethod, category, permissionCode));
            }

            // Group theo PermissionCode để gom nhóm các chi tiết API vào cùng một Quyền
            var groupsByCode = actionsList.GroupBy(a => a.PermCode).ToList();

            foreach (var codeGroup in groupsByCode)
            {
                var code = codeGroup.Key;
                var category = codeGroup.First().Category;

                var permDto = new PermissionDto
                {
                    Code = code,
                    Name = PermissionCodeResolver.GetFriendlyPermissionName(code)
                           ?? $"{(friendlyActionNames.TryGetValue(category, out var actionLabel) ? actionLabel : category)} {resourceFriendly.ToLower()}",
                    Description = $"Tự động sinh: Cho phép thực thi hành động '{(friendlyActionNames.TryGetValue(category, out var descLabel) ? descLabel : category).ToLower()}' trên tài nguyên '{resourceFriendly}'",
                    Details = codeGroup.Select(a => new PermissionDetailDto
                    {
                        ControllerName = controllerName,
                        ActionName = a.ActionName
                    }).ToList()
                };

                result.Add(permDto);
            }
        }

        return result;
    }

    private async Task PublishPermissionsAsync(PermissionRegistrationMessage message)
    {
        var queueName = "identity_permission_registration_queue";
        var connection = _serviceProvider.GetService<IConnection>();

        if (connection != null)
        {
            // Sử dụng Connection có sẵn của microservice
            await PublishUsingConnectionAsync(connection, queueName, message);
        }
        else
        {
            // Tự khởi tạo connection tạm thời từ cấu hình RabbitMQ
            _logger.LogInformation("🔌 Không tìm thấy IConnection trong DI. Đang tạo kết nối RabbitMQ tạm thời để gửi phân quyền...");
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672
            };

            using var tempConnection = await factory.CreateConnectionAsync();
            await PublishUsingConnectionAsync(tempConnection, queueName, message);
        }
    }

    private async Task PublishUsingConnectionAsync(IConnection connection, string queueName, PermissionRegistrationMessage message)
    {
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties(),
            body: body);
    }
}

// Lớp hỗ trợ lấy Custom Attribute dựa trên Tên chuỗi (phòng khi không có kiểu tĩnh tham chiếu trực tiếp)
public static class ReflectionExtensions
{
    public static object? GetCustomAttribute(this MemberInfo element, string attributeName)
    {
        return element.GetCustomAttributes(true)
            .FirstOrDefault(a => a.GetType().Name.Equals(attributeName, StringComparison.Ordinal) ||
                                 a.GetType().FullName?.Equals(attributeName, StringComparison.Ordinal) == true);
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
