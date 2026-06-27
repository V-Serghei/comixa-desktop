using Comixa.Core.Models;

namespace Comixa.Desktop.ViewModels;

public sealed class ComicBookListItemViewModel
{
    public ComicBookListItemViewModel(ComicBook comicBook)
    {
        ComicBook = comicBook;
        Title = comicBook.Title;
        SeriesName = comicBook.SeriesName;
        IssueNumber = comicBook.IssueNumber;
        Format = comicBook.Format.ToString().ToUpperInvariant();
        PageCount = comicBook.PageCount;
        FilePath = comicBook.FilePath;
    }

    public ComicBook ComicBook { get; }

    public string Title { get; }

    public string? SeriesName { get; }

    public int? IssueNumber { get; }

    public string Format { get; }

    public int PageCount { get; }

    public string FilePath { get; }

    public string Metadata => IssueNumber is null
        ? $"{Format} · {PageLabel}"
        : $"{Format} · issue {IssueNumber} · {PageLabel}";

    private string PageLabel => PageCount == 0 ? "pages unknown" : $"{PageCount} pages";
}
