using Comixa.Core.Models;

namespace Comixa.Reader.Scanning;

public sealed record ScannedComicFile(
    string FilePath,
    string FileName,
    ComicFormat Format,
    long SizeBytes,
    int PageCount)
{
    public ComicBook ToComicBook(DateTimeOffset addedAt)
    {
        var parsed = ComicTitleParser.Parse(FileName);

        return new ComicBook(
            Guid.NewGuid(),
            parsed.DisplayTitle,
            parsed.SeriesName,
            parsed.IssueNumber,
            FilePath,
            Format,
            PageCount,
            null,
            addedAt);
    }
}
