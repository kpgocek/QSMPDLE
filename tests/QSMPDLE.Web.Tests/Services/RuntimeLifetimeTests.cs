using QSMPDLE.Web.Diagnostics;

namespace QSMPDLE.Web.Tests.Services;

public sealed class RuntimeLifetimeTests
{
    [Fact]
    public async Task CircuitCountsHandleReconnectAndRepeatedClose()
    {
        var counters = new RuntimeCounters();
        using var handler = new CountingCircuitHandler(counters);
        await handler.OnCircuitOpenedAsync(null!, default);
        await handler.OnConnectionUpAsync(null!, default);
        await handler.OnConnectionUpAsync(null!, default);
        Assert.Equal(1, counters.Open);
        Assert.Equal(1, counters.Connected);
        await handler.OnConnectionDownAsync(null!, default);
        Assert.Equal(1, counters.Disconnected);
        await handler.OnConnectionUpAsync(null!, default);
        Assert.Equal(0, counters.Disconnected);
        await handler.OnCircuitClosedAsync(null!, default);
        handler.Dispose();
        Assert.Equal(0, counters.Open);
        Assert.Equal(0, counters.Connected);
    }
}
