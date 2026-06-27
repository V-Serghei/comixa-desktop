using Comixa.Core.Models;

namespace Comixa.Desktop.ViewModels;

public sealed class ComicBookListItemViewModel
{
    public ComicBookListItemViewModel(ComicBook comicBook)
    {
        Title = comicBook.Title;
        SeriesName = comicBook.SeriesName;
        IssueNumber = comicBook.IssueNumber;
        Format = comicBook.Format.ToString().ToUpperInvariant();
        PageCount = comicBook.PageCount;
        FilePath = comicBook.FilePath;
    }

    public string Title { get; }

    public string? SeriesName { get; }

    public int? IssueNumber { get; }

    public string Format { get; }

    public int PageCount { get; }

    public string FilePath { get; }

    public string Metadata => IssueNumber is null
        ? $"{Format} - {PageCount} pages"
        : $"{Format} - issue {IssueNumber} - {PageCount} pages";
}
