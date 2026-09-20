using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using QSMPDLE.Web.Diagnostics;
namespace QSMPDLE.Web.Workers;

public sealed class MemoryDiagnosticsWorker(ILogger<MemoryDiagnosticsWorker> logger,
    RuntimeCounters counters, IMemoryCache cache) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var process = Process.GetCurrentProcess();
        var started = process.StartTime.ToUniversalTime();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            do
            {
                process.Refresh();
                if (logger.IsEnabled(LogLevel.Information))
                {
                    var gc = GC.GetGCMemoryInfo();
                    logger.LogInformation("Memory sample PID={ProcessId} StartedUtc={StartedUtc} UptimeSeconds={UptimeSeconds} WorkingSetBytes={WorkingSetBytes} PrivateBytes={PrivateBytes} ManagedBytes={ManagedBytes} TotalAllocatedBytes={TotalAllocatedBytes} LastGcHeapBytes={LastGcHeapBytes} LastGcFragmentedBytes={LastGcFragmentedBytes} Gen0={Gen0} Gen1={Gen1} Gen2={Gen2} OpenCircuits={OpenCircuits} ConnectedCircuits={ConnectedCircuits} DisconnectedCircuits={DisconnectedCircuits} Subscriptions={Subscriptions} ArchiveCacheEntries={ArchiveCacheEntries} SharedCacheEntries={SharedCacheEntries}",
                        process.Id, started, (DateTime.UtcNow - started).TotalSeconds,
                        process.WorkingSet64, process.PrivateMemorySize64, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(),
                        gc.HeapSizeBytes, gc.FragmentedBytes, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
                        counters.Open, counters.Connected, counters.Disconnected, counters.Subscriptions, counters.ArchiveEntries,
                        (cache as MemoryCache)?.Count);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
