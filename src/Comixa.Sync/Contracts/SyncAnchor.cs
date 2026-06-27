namespace Comixa.Sync.Contracts;

public sealed record SyncAnchor(
    string DeviceId,
    DateTimeOffset LastLocalChangeAt);
