namespace Comixa.Sync.Contracts;

public sealed record WatchedFolderContract(
    string? LocalId,
    string SyncId,
    string DisplayName,
    long AddedAtUtcMillis,
    long UpdatedAtUtcMillis,
    long? DeletedAtUtcMillis);
