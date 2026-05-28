using System.Threading;

namespace EvnHanoi.SyncService.Services;

public interface IPmisSyncTriggerService
{
    void TriggerSync();
    CancellationToken GetTriggerToken();
}
