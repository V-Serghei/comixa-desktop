namespace Comixa.Sync.Contracts;

public sealed record ReadingProgressContract(
    string ComicSyncId,
    int PageIndex,
    int TotalPages,
    string Status,
    long UpdatedAtUtcMillis);
