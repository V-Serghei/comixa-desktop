namespace Comixa.Core.Models;

public sealed record ReadingProgress(
    Guid ComicBookId,
    int PageNumber,
    DateTimeOffset UpdatedAt);
