namespace Comixa.Sync.Contracts;

public sealed record BookmarkContract(
    string SyncId,
    string ComicSyncId,
    int PageIndex,
    string? Note,
    long CreatedAtUtcMillis,
    long UpdatedAtUtcMillis,
    long? DeletedAtUtcMillis);
