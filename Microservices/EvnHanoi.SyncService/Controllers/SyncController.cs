using EvnHanoi.SyncService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading.Tasks;

namespace EvnHanoi.SyncService.Controllers;

[ApiController]
[Route("api/v1/sync")]
public class SyncController : ControllerBase
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IPmisSyncTriggerService _triggerService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISchedulerFactory schedulerFactory, IPmisSyncTriggerService triggerService, ILogger<SyncController> logger)
    {
        _schedulerFactory = schedulerFactory;
        _triggerService = triggerService;
        _logger = logger;
    }

    [HttpPost("trigger-now")]
    public async Task<IActionResult> TriggerNow()
    {
        _logger.LogInformation("Received request to trigger PMIS Sync immediately.");

        // 1. Trigger the Quartz job
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.TriggerJob(new JobKey("PmisSyncJob"));

        // 2. Trigger the Background Worker (PmisSyncWorker)
        _triggerService.TriggerSync();

        return Ok(new { message = "Sync triggered immediately", success = true });
    }
}
