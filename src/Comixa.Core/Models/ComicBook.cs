namespace Comixa.Core.Models;

public sealed record ComicBook(
    Guid Id,
    string Title,
    string FilePath,
    int PageCount,
    string? CoverPath,
    DateTimeOffset AddedAt);
