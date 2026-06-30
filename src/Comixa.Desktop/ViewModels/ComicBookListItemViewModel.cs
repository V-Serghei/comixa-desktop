using Avalonia.Media.Imaging;
using Comixa.Core.Models;

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

    public ComicBook ComicBook { get; }

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

    public bool HasProgress => _progress is not null && _progress.PageNumber > 0;

    public bool IsRead => _progress is not null && PageCount > 0 && _progress.PageNumber >= PageCount - 1;

    public bool IsStarted => _progress is not null && _progress.PageNumber > 0 && !IsRead;

    public bool IsUnread => !IsStarted && !IsRead;

    public string Title { get; }
    public string? SeriesName { get; }
    public int? IssueNumber { get; }
    public string Format { get; }
    public int PageCount { get; }
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

    private string PageLabel => PageCount == 0 ? "? pages" : $"{PageCount} pages";

    public bool MatchesSearch(string normalizedQuery)
    {
        return normalizedQuery.Length == 0 || _searchText.Contains(normalizedQuery, StringComparison.Ordinal);
    }

    public void SetCoverImage(Bitmap? coverImage) => CoverImage = coverImage;

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
        if (comicBook.IssueNumber is null || !ShouldPreferParentFolder(comicBook))
        {
            return comicBook.SeriesName;
        }

        var parent = Directory.GetParent(comicBook.FilePath);
        return string.IsNullOrWhiteSpace(parent?.Name)
            ? comicBook.SeriesName
            : parent.Name;
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
}
