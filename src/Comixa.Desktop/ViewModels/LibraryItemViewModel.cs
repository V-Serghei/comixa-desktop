namespace Comixa.Desktop.ViewModels;

public abstract class LibraryItemViewModel : ViewModelBase { }

public sealed class SingleLibraryItemViewModel : LibraryItemViewModel
{
    private bool _isCurrentlyOpen;

    public SingleLibraryItemViewModel(
        ComicBookListItemViewModel item,
        Action<ComicBookListItemViewModel> onOpen,
        IReadOnlyList<ShelfMenuItemViewModel> shelfItems)
    {
        Item = item;
        OpenCommand = new RelayCommand(() => onOpen(item));
        ShelfItems = shelfItems;
    }

    public ComicBookListItemViewModel Item { get; }
    public RelayCommand OpenCommand { get; }
    public IReadOnlyList<ShelfMenuItemViewModel> ShelfItems { get; }

    public bool IsCurrentlyOpen
    {
        get => _isCurrentlyOpen;
        set
        {
            if (_isCurrentlyOpen == value) return;
            _isCurrentlyOpen = value;
            RaisePropertyChanged();
        }
    }
}

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

    public int ReadCount => Books.Count(b => b.Progress is not null && b.Progress.PageNumber > 0);
    public double ProgressValue => Books.Count == 0 ? 0.0 : (double)ReadCount / Books.Count;
    public bool HasProgress => ReadCount > 0;
    public string IssueCountLabel => $"{Books.Count} issues";
}
