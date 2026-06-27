namespace Comixa.Core.Models;

public sealed record ComicPage(
    Guid ComicBookId,
    int PageNumber,
    string EntryName,
    long SizeBytes);
