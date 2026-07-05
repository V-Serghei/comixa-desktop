namespace Comixa.Core.Models;

public sealed record Bookmark(
    Guid Id,
    string SyncId,
    Guid ComicBookId,
    string ComicSyncId,
    int PageIndex,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt);
