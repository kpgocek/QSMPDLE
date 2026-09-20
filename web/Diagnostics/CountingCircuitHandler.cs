using Microsoft.AspNetCore.Components.Server.Circuits;
namespace QSMPDLE.Web.Diagnostics;

public sealed class CountingCircuitHandler(RuntimeCounters counters) : CircuitHandler, IDisposable
{
    private bool open, connected;
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!open) { open = true; counters.AddOpen(1); }
        return Task.CompletedTask;
    }
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!connected) { connected = true; counters.AddConnected(1); }
        return Task.CompletedTask;
    }
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (connected) { connected = false; counters.AddConnected(-1); }
        return Task.CompletedTask;
    }
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        if (connected) { connected = false; counters.AddConnected(-1); }
        if (open) { open = false; counters.AddOpen(-1); }
    }
}
