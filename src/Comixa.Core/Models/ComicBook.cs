namespace Comixa.Core.Models;

public sealed record ComicBook(
    Guid Id,
    string Title,
    string? SeriesName,
    int? IssueNumber,
    string FilePath,
    ComicFormat Format,
    int PageCount,
    string? CoverPath,
    DateTimeOffset AddedAt);
