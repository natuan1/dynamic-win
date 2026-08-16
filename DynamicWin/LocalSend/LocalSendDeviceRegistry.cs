namespace DynamicWin.LocalSend;

public sealed record LocalSendDevice(DeviceIdentity Identity, string Address, DateTime LastSeenUtc);

public sealed class LocalSendDeviceRegistry(Func<DateTime>? clock = null)
{
    private readonly Dictionary<string, LocalSendDevice> devices = new();
    private readonly object gate = new();
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);

    public int Version { get; private set; }

    public void AddOrUpdate(DeviceIdentity identity, string address)
    {
        if (string.IsNullOrEmpty(identity.Fingerprint)) return;

        lock (gate)
        {
            devices[identity.Fingerprint] = new LocalSendDevice(identity, address, now());
            Version++;
        }
    }

    public void Prune(DateTime utcNow, TimeSpan ttl)
    {
        lock (gate)
        {
            var stale = devices.Where(pair => utcNow - pair.Value.LastSeenUtc > ttl)
                .Select(pair => pair.Key)
                .ToList();

            if (stale.Count == 0) return;

            foreach (var fingerprint in stale)
                devices.Remove(fingerprint);

            Version++;
        }
    }

    public IReadOnlyList<LocalSendDevice> Snapshot()
    {
        lock (gate)
        {
            return devices.Values.OrderBy(device => device.Identity.Alias).ToList();
        }
    }
}

public static class LocalSendPortPicker
{
    public static readonly int[] Candidates = [LocalSendProtocol.DefaultHttpPort, 0];
}
