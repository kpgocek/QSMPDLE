namespace QSMPDLE.Web.Diagnostics;

// Aggregate numbers only: no references to circuits, components, caches, or identities.
public sealed class RuntimeCounters
{
    private long open, connected, subscriptions, archiveEntries;
    public long Open => Interlocked.Read(ref open);
    public long Connected => Interlocked.Read(ref connected);
    public long Disconnected => Math.Max(0, Open - Connected);
    public long Subscriptions => Interlocked.Read(ref subscriptions);
    public long ArchiveEntries => Interlocked.Read(ref archiveEntries);
    public void AddOpen(long delta) => Interlocked.Add(ref open, delta);
    public void AddConnected(long delta) => Interlocked.Add(ref connected, delta);
    public void AddSubscriptions(long delta) => Interlocked.Add(ref subscriptions, delta);
    public void AddArchiveEntries(long delta) => Interlocked.Add(ref archiveEntries, delta);
}
