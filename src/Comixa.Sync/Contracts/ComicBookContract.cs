namespace Comixa.Sync.Contracts;

public sealed record ComicBookContract(
    string? LocalId,
    string SyncId,
    string ContentFingerprint,
    string Title,
    string? SeriesName,
    int? IssueNumber,
    string Format,
    int PageCount,
    long AddedAtUtcMillis,
    long UpdatedAtUtcMillis,
    long? DeletedAtUtcMillis);
