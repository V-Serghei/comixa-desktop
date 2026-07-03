using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Reader.Archives;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.ViewModels;

public sealed class ComicBookListItemViewModel : ViewModelBase
{
    private readonly string _searchText;
    private Bitmap? _coverImage;
    private ReadingProgress? _progress;

    public ComicBookListItemViewModel(ComicBook comicBook)
    {
        ComicBook = comicBook;
        Title = comicBook.Title;
        SeriesName = ResolveSeriesName(comicBook);
        IssueNumber = comicBook.IssueNumber;
        Format = comicBook.Format.ToString().ToUpperInvariant();
        PageCount = comicBook.PageCount;
        FilePath = comicBook.FilePath;
        _searchText = $"{Title}\n{SeriesName}\n{FilePath}".ToUpperInvariant();
    }

    public ComicBook ComicBook { get; private set; }

    public Bitmap? CoverImage
    {
        get => _coverImage;
        private set
        {
            if (_coverImage == value) return;
            _coverImage = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasCoverImage));
        }
    }

    public bool HasCoverImage => CoverImage is not null;

    public ReadingProgress? Progress => _progress;

    public double ProgressValue => _progress is null || ComicBook.PageCount == 0
        ? 0.0
        : (double)(_progress.PageNumber + 1) / ComicBook.PageCount;

    public bool HasProgress => _progress is not null;

    public bool IsRead => _progress is not null && PageCount > 0 && _progress.PageNumber >= PageCount - 1;

    public bool IsStarted => _progress is not null && !IsRead;

    public bool IsUnread => !IsStarted && !IsRead;

    public string Title { get; }
    public string? SeriesName { get; }
    public int? IssueNumber { get; }
    public string Format { get; }
    public int PageCount { get; private set; }
    public string FilePath { get; }

    public string IssueLabel => IssueNumber is null ? "" : $"#{IssueNumber}";

    public string Metadata => IssueNumber is null
        ? $"{Format} - {PageLabel}"
        : $"{Format} - #{IssueNumber} - {PageLabel}";

    public string ReadStatusLabel
    {
        get
        {
            if (_progress is null) return "";
            if (_progress.PageNumber == 0) return "Started";
            if (PageCount > 0 && _progress.PageNumber >= PageCount - 1) return "Read";
            return $"Page {_progress.PageNumber + 1} of {PageCount}";
        }
    }

    private string PageLabel => PageCount == 0
        ? Format switch
        {
            "SEVENZIP" or "EPUB" => "unsupported",
            "CBR" or "RAR" or "ZIP" => "no readable pages",
            _ => "? pages"
        }
        : $"{PageCount} pages";

    public bool MatchesSearch(string normalizedQuery)
    {
        return normalizedQuery.Length == 0 || _searchText.Contains(normalizedQuery, StringComparison.Ordinal);
    }

    public void SetCoverImage(Bitmap? coverImage) => CoverImage = coverImage;

    public void SetPageCount(int pageCount)
    {
        pageCount = Math.Max(0, pageCount);
        if (PageCount == pageCount) return;

        ComicBook = ComicBook with { PageCount = pageCount };
        PageCount = pageCount;
        RaisePropertyChanged(nameof(ComicBook));
        RaisePropertyChanged(nameof(PageCount));
        RaisePropertyChanged(nameof(Metadata));
        RaisePropertyChanged(nameof(ProgressValue));
        RaisePropertyChanged(nameof(IsRead));
        RaisePropertyChanged(nameof(IsStarted));
        RaisePropertyChanged(nameof(IsUnread));
        RaisePropertyChanged(nameof(ReadStatusLabel));
    }

    public void SetProgress(ReadingProgress? progress)
    {
        _progress = progress;
        RaisePropertyChanged(nameof(Progress));
        RaisePropertyChanged(nameof(ProgressValue));
        RaisePropertyChanged(nameof(HasProgress));
        RaisePropertyChanged(nameof(IsRead));
        RaisePropertyChanged(nameof(IsStarted));
        RaisePropertyChanged(nameof(IsUnread));
        RaisePropertyChanged(nameof(ReadStatusLabel));
    }

    private static string? ResolveSeriesName(ComicBook comicBook)
    {
        var titleSeries = ResolveSeriesFromTitle(comicBook);
        var storedSeries = string.IsNullOrWhiteSpace(comicBook.SeriesName)
            ? null
            : ComicTitleParser.NormalizeDisplayName(comicBook.SeriesName);
        var hasUsefulStoredSeries = !string.IsNullOrWhiteSpace(storedSeries) &&
            !IsGenericLibraryFolderName(storedSeries);

        if (comicBook.IssueNumber is null)
        {
            return hasUsefulStoredSeries ? storedSeries : titleSeries;
        }

        if (!ShouldPreferParentFolder(comicBook))
        {
            return hasUsefulStoredSeries ? storedSeries : titleSeries;
        }

        var parent = Directory.GetParent(ComicArchiveLocator.GetPhysicalArchivePath(comicBook.FilePath));
        if (!string.IsNullOrWhiteSpace(parent?.Name) && !IsGenericLibraryFolderName(parent.Name))
        {
            return ComicTitleParser.NormalizeDisplayName(parent.Name);
        }

        return titleSeries ?? (hasUsefulStoredSeries ? storedSeries : null);
    }

    private static string? ResolveSeriesFromTitle(ComicBook comicBook)
    {
        if (string.IsNullOrWhiteSpace(comicBook.Title))
        {
            return null;
        }

        var parsed = ComicTitleParser.Parse(comicBook.Title);
        return parsed.IssueNumber is null || string.IsNullOrWhiteSpace(parsed.SeriesName)
            ? null
            : parsed.SeriesName;
    }

    private static bool ShouldPreferParentFolder(ComicBook comicBook)
    {
        if (string.IsNullOrWhiteSpace(comicBook.SeriesName))
        {
            return true;
        }

        return comicBook.SeriesName.Equals(comicBook.Title, StringComparison.OrdinalIgnoreCase)
            || StartsWithIssueNumber(comicBook.Title, comicBook.IssueNumber.Value)
            || int.TryParse(comicBook.SeriesName, out _)
            || comicBook.SeriesName.StartsWith("chapter", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("ch ", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("part", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("pt ", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("episode", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("ep ", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("\u0433\u043b\u0430\u0432\u0430", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("\u0447\u0430\u0441\u0442\u044c", StringComparison.OrdinalIgnoreCase)
            || comicBook.SeriesName.StartsWith("\u0442\u043e\u043c", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithIssueNumber(string title, int issueNumber)
    {
        var trimmed = title.TrimStart();
        for (var width = 1; width <= 4; width++)
        {
            var value = issueNumber.ToString($"D{width}");
            if (!trimmed.StartsWith(value, StringComparison.Ordinal))
            {
                continue;
            }

            return trimmed.Length == value.Length || IsIssueSeparator(trimmed[value.Length]);
        }

        return false;
    }

    private static bool IsIssueSeparator(char value)
    {
        return char.IsWhiteSpace(value) || value is '-' or '_' or '.' or ',';
    }

    private static bool IsGenericLibraryFolderName(string folderName)
    {
        var normalized = ComicTitleParser.NormalizeDisplayName(folderName);
        return normalized.Equals("Com", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Comic", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Comics", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Manga", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Library", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Books", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Downloads", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Desktop", StringComparison.OrdinalIgnoreCase);
    }
}
