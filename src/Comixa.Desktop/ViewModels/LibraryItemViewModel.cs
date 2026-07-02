namespace Comixa.Desktop.ViewModels;

public abstract class LibraryItemViewModel : ViewModelBase { }

public sealed class LibraryRowViewModel(IReadOnlyList<LibraryItemViewModel> items, int columnCount)
{
    public IReadOnlyList<LibraryItemViewModel> Items { get; } = items;
    public int ColumnCount { get; } = columnCount;
}

public sealed class SeriesVariantViewModel(
    string title,
    string subtitle,
    IReadOnlyList<ComicBookListItemViewModel> books,
    IReadOnlyList<LibraryRowViewModel> rows)
{
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;
    public IReadOnlyList<ComicBookListItemViewModel> Books { get; } = books;
    public IReadOnlyList<LibraryRowViewModel> Rows { get; } = rows;
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
}

public sealed class SingleLibraryItemViewModel : LibraryItemViewModel
{
    private bool _isCurrentlyOpen;

    public SingleLibraryItemViewModel(
        ComicBookListItemViewModel item,
        Action<ComicBookListItemViewModel> onOpen,
        Action<ComicBookListItemViewModel> onOpenFullscreen,
        Action<ComicBookListItemViewModel> onMarkAsRead,
        Action<ComicBookListItemViewModel> onMarkAsUnread,
        Action onCreateShelf,
        Func<IReadOnlyList<ShelfMenuItemViewModel>> getShelfItems)
    {
        Item = item;
        OpenCommand = new RelayCommand(() => onOpen(item));
        OpenFullscreenCommand = new RelayCommand(() => onOpenFullscreen(item));
        MarkAsReadCommand = new RelayCommand(() => onMarkAsRead(item));
        MarkAsUnreadCommand = new RelayCommand(() => onMarkAsUnread(item));
        CreateShelfCommand = new RelayCommand(onCreateShelf);
        _getShelfItems = getShelfItems;
    }

    private readonly Func<IReadOnlyList<ShelfMenuItemViewModel>> _getShelfItems;

    public ComicBookListItemViewModel Item { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand OpenFullscreenCommand { get; }
    public RelayCommand MarkAsReadCommand { get; }
    public RelayCommand MarkAsUnreadCommand { get; }
    public RelayCommand CreateShelfCommand { get; }
    public IReadOnlyList<ShelfMenuItemViewModel> ShelfItems => _getShelfItems();
    public bool HasShelfItems => ShelfItems.Count > 0;

    public void RefreshShelfItems()
    {
        RaisePropertyChanged(nameof(ShelfItems));
        RaisePropertyChanged(nameof(HasShelfItems));
    }

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
    public string IssueCountLabel => Books.Count == 1 ? "1 issue" : $"{Books.Count} issues";
}
