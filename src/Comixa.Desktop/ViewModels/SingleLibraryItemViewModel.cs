namespace Comixa.Desktop.ViewModels;

public sealed class SingleLibraryItemViewModel : LibraryItemViewModel
{
    private readonly Func<IReadOnlyList<ShelfMenuItemViewModel>> _getShelfItems;
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

    public ComicBookListItemViewModel Item { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand OpenFullscreenCommand { get; }
    public RelayCommand MarkAsReadCommand { get; }
    public RelayCommand MarkAsUnreadCommand { get; }
    public RelayCommand CreateShelfCommand { get; }
    public IReadOnlyList<ShelfMenuItemViewModel> ShelfItems => _getShelfItems();
    public bool HasShelfItems => ShelfItems.Count > 0;

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

    public void RefreshShelfItems()
    {
        RaisePropertyChanged(nameof(ShelfItems));
        RaisePropertyChanged(nameof(HasShelfItems));
    }
}
