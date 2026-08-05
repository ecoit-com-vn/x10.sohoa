using EvnHanoi.NotificationService.Services;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using RedLockNet;

namespace EvnHanoi.NotificationService.Schedulers;

[DisallowConcurrentExecution]
public sealed class AuditLogRetentionJob : IJob
{
    private const string LockResource = "lock:audit-log-retention";

    private readonly IDistributedLockFactory _lockFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogRetentionJob> _logger;

    public AuditLogRetentionJob(
        IDistributedLockFactory lockFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogRetentionJob> logger)
    {
        _lockFactory = lockFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var expiry = TimeSpan.FromHours(2);
        var wait = TimeSpan.FromSeconds(10);
        var retry = TimeSpan.FromSeconds(1);

        using var redLock = await _lockFactory.CreateLockAsync(LockResource, expiry, wait, retry);
        if (!redLock.IsAcquired)
        {
            _logger.LogWarning(
                "Bỏ qua job xóa audit log quá hạn vì không lấy được distributed lock {LockResource}.",
                LockResource);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var settingsClient = scope.ServiceProvider.GetRequiredService<IAuditLogRetentionSettingsClient>();
        var retentionDays = await settingsClient.GetRetentionDaysAsync(context.CancellationToken);
        if (!retentionDays.HasValue)
            return;

        var cutoffUtc = DateTime.UtcNow.Date.AddDays(-retentionDays.Value);
        _logger.LogInformation(
            "Bắt đầu xóa vật lý audit log quá hạn. CutoffUtc: {CutoffUtc}, RetentionDays: {RetentionDays}",
            cutoffUtc,
            retentionDays.Value);

        try
        {
            var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var result = await auditLogService.PurgeExpiredAuditLogsAsync(cutoffUtc, context.CancellationToken);

            _logger.LogInformation(
                "Đã xóa vật lý audit log quá hạn. CutoffUtc: {CutoffUtc}, DeletedIndices: {DeletedIndices}, DeletedDocumentsInCutoffIndex: {DeletedDocuments}",
                cutoffUtc,
                result.DeletedIndices,
                result.DeletedDocuments);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Xóa vật lý audit log quá hạn thất bại. CutoffUtc: {CutoffUtc}",
                cutoffUtc);
            throw;
        }
    }
}
