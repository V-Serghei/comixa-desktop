namespace Comixa.Core.Models;

public sealed record ComicPage(
    Guid ComicBookId,
    int PageIndex,
    string EntryName,
    long SizeBytes);
