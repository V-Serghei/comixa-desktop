namespace Comixa.Core.Models;

public sealed record Bookmark(
    Guid Id,
    Guid ComicBookId,
    int PageNumber,
    string? Note,
    DateTimeOffset CreatedAt);
