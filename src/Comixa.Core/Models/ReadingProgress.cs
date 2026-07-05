namespace Comixa.Core.Models;

public sealed record ReadingProgress(
    Guid ComicBookId,
    string ComicSyncId,
    int PageIndex,
    int TotalPages,
    ReadingStatus Status,
    DateTimeOffset UpdatedAt);
