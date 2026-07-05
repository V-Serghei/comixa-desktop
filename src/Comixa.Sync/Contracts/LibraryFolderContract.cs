namespace Comixa.Sync.Contracts;

public sealed record LibraryFolderContract(
    string? LocalId,
    string SyncId,
    string DisplayName,
    long AddedAtUtcMillis,
    long UpdatedAtUtcMillis,
    long? DeletedAtUtcMillis);
