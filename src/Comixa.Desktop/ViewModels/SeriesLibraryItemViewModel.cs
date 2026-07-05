namespace Comixa.Desktop.ViewModels;

public sealed class SeriesLibraryItemViewModel : LibraryItemViewModel
{
    public SeriesLibraryItemViewModel(
        string seriesName,
        IReadOnlyList<ComicBookListItemViewModel> books,
        Action<SeriesLibraryItemViewModel> onOpen)
    {
        SeriesName = seriesName;
        Books = books;
        Cover = books.MinBy(b => b.IssueNumber ?? int.MaxValue) ?? books[0];
        OpenCommand = new RelayCommand(() => onOpen(this));
    }

    public string SeriesName { get; }
    public IReadOnlyList<ComicBookListItemViewModel> Books { get; }
    public ComicBookListItemViewModel Cover { get; }
    public RelayCommand OpenCommand { get; }

    public int ReadCount => Books.Count(b => b.Progress is not null && b.Progress.PageIndex > 0);
    public double ProgressValue => Books.Count == 0 ? 0.0 : (double)ReadCount / Books.Count;
    public bool HasProgress => ReadCount > 0;
    public string IssueCountLabel => Books.Count == 1 ? "1 issue" : $"{Books.Count} issues";
}
