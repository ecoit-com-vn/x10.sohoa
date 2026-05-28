using System.Threading;

namespace EvnHanoi.SyncService.Services;

public class PmisSyncTriggerService : IPmisSyncTriggerService
{
    private CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly object _lock = new object();

    public void TriggerSync()
    {
        lock (_lock)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }
    }

    public CancellationToken GetTriggerToken()
    {
        lock (_lock)
        {
            return _cts.Token;
        }
    }
}
