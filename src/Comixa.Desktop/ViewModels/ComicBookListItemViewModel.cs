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
        SeriesName = comicBook.SeriesName;
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
        RaisePropertyChanged(nameof(ReadStatusLabel));
    }
}
