using System.Collections.ObjectModel;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Comixa.Core.Models;
using Comixa.Core.Repositories;
using Comixa.Desktop.Reader;
using Comixa.Desktop.Services;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.ViewModels;

public enum FitMode { FitPage, FitWidth }

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IFolderPicker _folderPicker;
    private readonly IComicLibraryScanner _comicLibraryScanner;
    private readonly IUserLibrarySettingsStore _settingsStore;
    private readonly IPagePreviewLoader _pagePreviewLoader;
    private readonly IComicLibraryRepository _comicRepository;
    private readonly IReadingProgressRepository _progressRepository;
    private readonly IUserPreferencesStore _preferencesStore;
    private readonly IShelfRepository _shelfRepository;
    private readonly IBookmarkRepository _bookmarkRepository;

    private readonly List<ComicBookListItemViewModel> _allBooks = [];
    private readonly Dictionary<Guid, ReadingProgress> _progressMap = [];
    private readonly List<Shelf> _shelves = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _shelfEntriesMap = [];

    // Navigation state
    private bool _isSeriesView;
    private Guid? _activeShelfId;
    private string _activeShelfName = "";

    // Library filter state
    private string _searchQuery = "";
    private SortOrder _sortOrder = SortOrder.TitleAsc;
    private bool _showUnreadOnly;
    private ComicFormat? _formatFilter;

    // Series detail state
    private bool _isInSeriesDetail;
    private string _currentSeriesName = "";
    private IReadOnlyList<ComicBookListItemViewModel> _seriesBooks = [];

    // Settings state
    private bool _isSettingsPanelVisible;
    private bool _isDarkTheme = true;
    private ReadingDirection _readingDirection = ReadingDirection.LeftToRight;
    private FitMode _fitMode = FitMode.FitPage;

    // Shelf creation state
    private bool _isCreatingShelf;
    private string _newShelfName = "";

    // Reader state
    private ComicBookListItemViewModel? _selectedBook;
    private Bitmap? _currentPageImage;
    private int _currentPageIndex;
    private string _statusMessage = "Add a local folder to begin.";
    private string _readerStatus = "Select a book to start reading.";
    private bool _isLoadingLibrary;
    private bool _isPageLoading;

    // Bookmarks
    private List<Bookmark> _currentBookBookmarks = [];

    private CancellationTokenSource? _verticalPagesCts;
    private SingleLibraryItemViewModel? _openSingleItem;

    public MainWindowViewModel(
        IFolderPicker folderPicker,
        IComicLibraryScanner comicLibraryScanner,
        IUserLibrarySettingsStore settingsStore,
        IPagePreviewLoader pagePreviewLoader,
        IComicLibraryRepository comicRepository,
        IReadingProgressRepository progressRepository,
        IUserPreferencesStore preferencesStore,
        IShelfRepository shelfRepository,
        IBookmarkRepository bookmarkRepository)
    {
        _folderPicker = folderPicker;
        _comicLibraryScanner = comicLibraryScanner;
        _settingsStore = settingsStore;
        _pagePreviewLoader = pagePreviewLoader;
        _comicRepository = comicRepository;
        _progressRepository = progressRepository;
        _preferencesStore = preferencesStore;
        _shelfRepository = shelfRepository;
        _bookmarkRepository = bookmarkRepository;

        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
        BackToLibraryCommand = new RelayCommand(BackToLibrary);
        OpenBookCommand = new RelayCommand<ComicBookListItemViewModel>(OpenBook);

        // Navigation commands
        NavigateToAllBooksCommand = new RelayCommand(NavigateToAllBooks);
        NavigateToSeriesCommand = new RelayCommand(NavigateToSeries);

        // UI Prev/Next (direction-aware for RTL)
        PreviousPageCommand = new RelayCommand(
            () => MovePageByIndex(_readingDirection == ReadingDirection.RightToLeft ? 1 : -1),
            () => SelectedBook is not null &&
                  (_readingDirection == ReadingDirection.RightToLeft
                      ? CurrentPageIndex + 1 < (SelectedBook?.ComicBook.PageCount ?? 0)
                      : CurrentPageIndex > 0));
        NextPageCommand = new RelayCommand(
            () => MovePageByIndex(_readingDirection == ReadingDirection.RightToLeft ? -1 : 1),
            () => SelectedBook is not null &&
                  (_readingDirection == ReadingDirection.RightToLeft
                      ? CurrentPageIndex > 0
                      : CurrentPageIndex + 1 < (SelectedBook?.ComicBook.PageCount ?? 0)));

        PageBackwardCommand = new RelayCommand(
            () => MovePageByIndex(-1),
            () => SelectedBook is not null && CurrentPageIndex > 0);
        PageForwardCommand = new RelayCommand(
            () => MovePageByIndex(1),
            () => SelectedBook is not null && CurrentPageIndex + 1 < (SelectedBook?.ComicBook.PageCount ?? 0));
        GoToFirstPageCommand = new RelayCommand(
            () => JumpToPage(0),
            () => SelectedBook is not null && CurrentPageIndex > 0);
        GoToLastPageCommand = new RelayCommand(
            () => JumpToPage(Math.Max(0, (SelectedBook?.ComicBook.PageCount ?? 1) - 1)),
            () => SelectedBook is not null &&
                  CurrentPageIndex < (SelectedBook?.ComicBook.PageCount ?? 1) - 1);

        ToggleSettingsPanelCommand = new RelayCommand(ToggleSettingsPanel);
        ToggleFitModeCommand = new RelayCommand(ToggleFitMode);
        SetSortOrderCommand = new RelayCommand<string>(SetSortOrder);
        ToggleUnreadOnlyCommand = new RelayCommand(ToggleUnreadOnly);
        ToggleCbzFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Cbz));
        ToggleZipFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Zip));
        TogglePdfFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Pdf));
        SetReadingDirectionCommand = new RelayCommand<string>(SetReadingDirection);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ClearSearchCommand = new RelayCommand(() => SearchQuery = "");

        StartCreateShelfCommand = new RelayCommand(() =>
        {
            _isCreatingShelf = true;
            RaisePropertyChanged(nameof(IsCreatingShelf));
        });
        ConfirmNewShelfCommand = new AsyncRelayCommand(CreateShelfAsync);
        CancelNewShelfCommand = new RelayCommand(() =>
        {
            _isCreatingShelf = false;
            _newShelfName = "";
            RaisePropertyChanged(nameof(IsCreatingShelf));
            RaisePropertyChanged(nameof(NewShelfName));
        });

        ToggleBookmarkCommand = new AsyncRelayCommand(ToggleBookmarkAsync);

        _ = InitializeAsync();
    }

    // Collections
    public ObservableCollection<LibraryItemViewModel> DisplayedItems { get; } = [];
    public ObservableCollection<Bitmap?> VerticalPages { get; } = [];
    public ObservableCollection<ShelfViewModel> Shelves { get; } = [];

    // Commands — navigation
    public RelayCommand NavigateToAllBooksCommand { get; }
    public RelayCommand NavigateToSeriesCommand { get; }

    // Commands — reader
    public AsyncRelayCommand ScanFolderCommand { get; }
    public RelayCommand BackToLibraryCommand { get; }
    public RelayCommand<ComicBookListItemViewModel> OpenBookCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PageBackwardCommand { get; }
    public RelayCommand PageForwardCommand { get; }
    public RelayCommand GoToFirstPageCommand { get; }
    public RelayCommand GoToLastPageCommand { get; }
    public RelayCommand ToggleFitModeCommand { get; }
    public AsyncRelayCommand ToggleBookmarkCommand { get; }

    // Commands — library
    public RelayCommand ToggleSettingsPanelCommand { get; }
    public RelayCommand<string> SetSortOrderCommand { get; }
    public RelayCommand ToggleUnreadOnlyCommand { get; }
    public RelayCommand ToggleCbzFilterCommand { get; }
    public RelayCommand ToggleZipFilterCommand { get; }
    public RelayCommand TogglePdfFilterCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

    // Commands — shelves
    public RelayCommand StartCreateShelfCommand { get; }
    public AsyncRelayCommand ConfirmNewShelfCommand { get; }
    public RelayCommand CancelNewShelfCommand { get; }

    // Commands — settings
    public RelayCommand<string> SetReadingDirectionCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    // Window title
    public string Title => SelectedBook is null
        ? "Comixa Desktop"
        : $"{SelectedBook.Title} — Comixa";

    // Navigation state
    public bool IsViewAllBooks => _activeShelfId is null && !_isSeriesView;
    public bool IsViewSeries => _activeShelfId is null && _isSeriesView;

    public string LibraryViewTitle => _activeShelfId.HasValue
        ? _activeShelfName
        : _isSeriesView ? "Series" : "All Books";

    // Library filter state
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value) return;
            _searchQuery = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasSearchQuery));
            RefreshDisplayedItems();
        }
    }

    public bool HasSearchQuery => !string.IsNullOrEmpty(_searchQuery);
    public bool ShowUnreadOnly => _showUnreadOnly;
    public bool IsCbzFilterActive => _formatFilter == ComicFormat.Cbz;
    public bool IsZipFilterActive => _formatFilter == ComicFormat.Zip;
    public bool IsPdfFilterActive => _formatFilter == ComicFormat.Pdf;
    public bool IsSortTitleAsc => _sortOrder == SortOrder.TitleAsc;
    public bool IsSortTitleDesc => _sortOrder == SortOrder.TitleDesc;
    public bool IsSortRecentlyAdded => _sortOrder == SortOrder.RecentlyAdded;
    public bool IsSortRecentlyRead => _sortOrder == SortOrder.RecentlyRead;

    public bool IsInSeriesDetail => _isInSeriesDetail;
    public string CurrentSeriesName => _currentSeriesName;
    public IReadOnlyList<ComicBookListItemViewModel> SeriesBooks => _seriesBooks;
    public bool IsLibraryEmpty => _allBooks.Count == 0 && !_isLoadingLibrary;

    // Shelf creation state
    public bool IsCreatingShelf => _isCreatingShelf;

    public string NewShelfName
    {
        get => _newShelfName;
        set
        {
            if (_newShelfName == value) return;
            _newShelfName = value;
            RaisePropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            RaisePropertyChanged();
        }
    }

    // Settings state
    public bool IsSettingsPanelVisible => _isSettingsPanelVisible;
    public bool IsDarkTheme => _isDarkTheme;
    public bool IsLightTheme => !_isDarkTheme;
    public bool IsLeftToRight => _readingDirection == ReadingDirection.LeftToRight;
    public bool IsRightToLeft => _readingDirection == ReadingDirection.RightToLeft;
    public bool IsTopToBottom => _readingDirection == ReadingDirection.TopToBottom;
    public bool IsVerticalMode => _readingDirection == ReadingDirection.TopToBottom;

    // Fit mode
    public bool IsFitPageMode => _fitMode == FitMode.FitPage;
    public bool IsFitWidthMode => _fitMode == FitMode.FitWidth;
    public string FitModeLabel => _fitMode == FitMode.FitPage ? "Fit Page" : "Fit Width";

    // Reader state
    public ComicBookListItemViewModel? SelectedBook
    {
        get => _selectedBook;
        private set
        {
            if (_selectedBook == value) return;
            _selectedBook = value;
            CurrentPageIndex = 0;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasSelectedBook));
            RaisePropertyChanged(nameof(Title));

            if (_readingDirection == ReadingDirection.TopToBottom && value is not null)
                _ = LoadVerticalPagesAsync(value.ComicBook);
            else
                _ = LoadCurrentPageAsync();

            _ = LoadBookmarksAsync();
        }
    }

    public bool HasSelectedBook => SelectedBook is not null;

    public bool IsPageLoading
    {
        get => _isPageLoading;
        private set
        {
            if (_isPageLoading == value) return;
            _isPageLoading = value;
            RaisePropertyChanged();
        }
    }

    public Bitmap? CurrentPageImage
    {
        get => _currentPageImage;
        private set
        {
            if (_currentPageImage == value) return;
            _currentPageImage = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasCurrentPageImage));
        }
    }

    public bool HasCurrentPageImage => CurrentPageImage is not null;

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set
        {
            if (_currentPageIndex == value) return;
            _currentPageIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CurrentPageLabel));
            RaisePropertyChanged(nameof(IsCurrentPageBookmarked));
            RaisePropertyChanged(nameof(BookmarkIcon));
            RefreshAllNavCanExecute();
        }
    }

    public string CurrentPageLabel
    {
        get
        {
            if (SelectedBook is null) return "";
            return SelectedBook.ComicBook.PageCount == 0
                ? "Loading..."
                : $"{CurrentPageIndex + 1} / {SelectedBook.ComicBook.PageCount}";
        }
    }

    public string ReaderStatus
    {
        get => _readerStatus;
        private set
        {
            if (_readerStatus == value) return;
            _readerStatus = value;
            RaisePropertyChanged();
        }
    }

    // Bookmarks
    public bool IsCurrentPageBookmarked =>
        _currentBookBookmarks.Any(b => b.PageNumber == _currentPageIndex);

    public string BookmarkIcon => IsCurrentPageBookmarked ? "🔖" : "📄";

    // --- Initialization ---

    private async Task InitializeAsync()
    {
        var prefs = await _preferencesStore.LoadAsync();
        _isDarkTheme = prefs.IsDarkTheme;
        _readingDirection = prefs.ReadingDirection;
        ApplyTheme();
        RaisePropertyChanged(nameof(IsDarkTheme));
        RaisePropertyChanged(nameof(IsLightTheme));
        NotifyAllDirectionProps();

        await LoadShelvesAsync();
        await LoadSavedFoldersAsync();
    }

    private async Task LoadShelvesAsync()
    {
        var shelves = await _shelfRepository.GetAllAsync();
        _shelves.Clear();
        _shelves.AddRange(shelves);

        _shelfEntriesMap.Clear();
        foreach (var shelf in shelves)
        {
            var bookIds = await _shelfRepository.GetBookIdsAsync(shelf.Id);
            _shelfEntriesMap[shelf.Id] = [..bookIds];
        }

        RebuildShelfVMs();
    }

    private async Task LoadSavedFoldersAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        if (settings.WatchedFolders.Count == 0) return;
        await ScanFoldersAsync(settings.WatchedFolders);
    }

    private async Task ScanFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync();
        if (folder is null) return;

        var settings = await _settingsStore.LoadAsync();
        var folders = settings.WatchedFolders
            .Append(folder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await _settingsStore.SaveAsync(new UserLibrarySettings(folders));
        await ScanFoldersAsync(folders);
    }

    private async Task ScanFoldersAsync(IReadOnlyList<string> folders)
    {
        _isLoadingLibrary = true;
        StatusMessage = "Scanning library...";
        RaisePropertyChanged(nameof(IsLibraryEmpty));

        var scannedFiles = new List<ScannedComicFile>();
        foreach (var folder in folders.Where(Directory.Exists))
            scannedFiles.AddRange(await _comicLibraryScanner.ScanAsync(folder));

        var now = DateTimeOffset.UtcNow;
        foreach (var file in scannedFiles)
            await _comicRepository.UpsertAsync(file.ToComicBook(now));

        var allDbBooks = await _comicRepository.GetAllAsync();
        var allProgress = await _progressRepository.GetAllAsync();

        _progressMap.Clear();
        foreach (var p in allProgress)
            _progressMap[p.ComicBookId] = p;

        _allBooks.Clear();
        foreach (var book in allDbBooks)
        {
            var vm = new ComicBookListItemViewModel(book);
            _progressMap.TryGetValue(book.Id, out var progress);
            vm.SetProgress(progress);
            _allBooks.Add(vm);
        }

        _isLoadingLibrary = false;
        StatusMessage = _allBooks.Count > 0
            ? $"{_allBooks.Count} comic{(_allBooks.Count == 1 ? "" : "s")} in library"
            : "No CBZ, ZIP, PDF or image folders found.";

        RaisePropertyChanged(nameof(IsLibraryEmpty));
        RefreshDisplayedItems();

        _ = LoadCoversAsync();
    }

    // --- Navigation ---

    private void NavigateToAllBooks()
    {
        _isSeriesView = false;
        _activeShelfId = null;
        _activeShelfName = "";
        _isInSeriesDetail = false;
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private void NavigateToSeries()
    {
        _isSeriesView = true;
        _activeShelfId = null;
        _activeShelfName = "";
        _isInSeriesDetail = false;
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private void NotifyNavigationProps()
    {
        foreach (var vm in Shelves)
            vm.IsActive = vm.Id == _activeShelfId;
        RaisePropertyChanged(nameof(IsViewAllBooks));
        RaisePropertyChanged(nameof(IsViewSeries));
        RaisePropertyChanged(nameof(LibraryViewTitle));
        RaisePropertyChanged(nameof(IsInSeriesDetail));
    }

    // --- Shelf management ---

    private void SelectShelf(Guid shelfId)
    {
        _isSeriesView = false;
        _activeShelfId = shelfId;
        _activeShelfName = _shelves.FirstOrDefault(s => s.Id == shelfId)?.Name ?? "";
        _isInSeriesDetail = false;
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private async void DeleteShelf(Guid shelfId)
    {
        await _shelfRepository.DeleteAsync(shelfId);
        _shelves.RemoveAll(s => s.Id == shelfId);
        _shelfEntriesMap.Remove(shelfId);
        if (_activeShelfId == shelfId)
        {
            _activeShelfId = null;
            _activeShelfName = "";
        }
        RebuildShelfVMs();
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private async Task CreateShelfAsync()
    {
        var name = _newShelfName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var shelf = await _shelfRepository.CreateAsync(name);
        _shelves.Add(shelf);
        _shelfEntriesMap[shelf.Id] = [];

        _newShelfName = "";
        _isCreatingShelf = false;
        RaisePropertyChanged(nameof(NewShelfName));
        RaisePropertyChanged(nameof(IsCreatingShelf));

        RebuildShelfVMs();
        RefreshDisplayedItems();
    }

    private async void ToggleBookInShelf(Guid shelfId, Guid bookId, bool addToShelf)
    {
        if (addToShelf)
        {
            await _shelfRepository.AddBookAsync(shelfId, bookId);
            if (!_shelfEntriesMap.TryGetValue(shelfId, out var set))
                _shelfEntriesMap[shelfId] = set = [];
            set.Add(bookId);
        }
        else
        {
            await _shelfRepository.RemoveBookAsync(shelfId, bookId);
            if (_shelfEntriesMap.TryGetValue(shelfId, out var set))
                set.Remove(bookId);
        }

        RefreshDisplayedItems();
    }

    private void RebuildShelfVMs()
    {
        Shelves.Clear();
        foreach (var shelf in _shelves)
        {
            var vm = new ShelfViewModel(shelf, SelectShelf, DeleteShelf);
            vm.IsActive = shelf.Id == _activeShelfId;
            Shelves.Add(vm);
        }
    }

    private IReadOnlyList<ShelfMenuItemViewModel> BuildShelfMenuItems(Guid bookId)
    {
        return _shelves
            .Select(s =>
            {
                var isInShelf = _shelfEntriesMap.TryGetValue(s.Id, out var ids) && ids.Contains(bookId);
                return new ShelfMenuItemViewModel(s.Id, s.Name, isInShelf, (shelfId, add) =>
                    ToggleBookInShelf(shelfId, bookId, add));
            })
            .ToList();
    }

    // --- Mark as read / unread ---

    private async void MarkBookAsRead(ComicBookListItemViewModel book)
    {
        var lastPage = Math.Max(0, book.ComicBook.PageCount - 1);
        var progress = new ReadingProgress(book.ComicBook.Id, lastPage, DateTimeOffset.UtcNow);
        await _progressRepository.SaveAsync(progress);
        book.SetProgress(progress);
        _progressMap[progress.ComicBookId] = progress;
        RefreshDisplayedItems();
    }

    private async void MarkBookAsUnread(ComicBookListItemViewModel book)
    {
        await _progressRepository.DeleteAsync(book.ComicBook.Id);
        book.SetProgress(null);
        _progressMap.Remove(book.ComicBook.Id);
        RefreshDisplayedItems();
    }

    // --- Library navigation (series detail) ---

    private void OpenSeries(SeriesLibraryItemViewModel series)
    {
        _isInSeriesDetail = true;
        _currentSeriesName = series.SeriesName;
        _seriesBooks = series.Books;
        RaisePropertyChanged(nameof(IsInSeriesDetail));
        RaisePropertyChanged(nameof(CurrentSeriesName));
        RaisePropertyChanged(nameof(SeriesBooks));
    }

    private void BackToLibrary()
    {
        _isInSeriesDetail = false;
        RaisePropertyChanged(nameof(IsInSeriesDetail));
    }

    private void OpenBook(ComicBookListItemViewModel? book)
    {
        if (book is null) return;

        if (_isSettingsPanelVisible)
        {
            _isSettingsPanelVisible = false;
            RaisePropertyChanged(nameof(IsSettingsPanelVisible));
        }

        if (_openSingleItem is not null)
            _openSingleItem.IsCurrentlyOpen = false;

        _openSingleItem = DisplayedItems
            .OfType<SingleLibraryItemViewModel>()
            .FirstOrDefault(i => i.Item.ComicBook.Id == book.ComicBook.Id);

        if (_openSingleItem is not null)
            _openSingleItem.IsCurrentlyOpen = true;

        SelectedBook = book;

        // Mark as "started" (page 0) if no prior progress
        if (!book.HasProgress)
            _ = SaveStartedProgressAsync(book);
    }

    private async Task SaveStartedProgressAsync(ComicBookListItemViewModel book)
    {
        var progress = new ReadingProgress(book.ComicBook.Id, 0, DateTimeOffset.UtcNow);
        await _progressRepository.SaveAsync(progress);
        book.SetProgress(progress);
        _progressMap[progress.ComicBookId] = progress;
    }

    // --- Filter / Sort ---

    private void SetSortOrder(string? key)
    {
        _sortOrder = key switch
        {
            "title_desc" => SortOrder.TitleDesc,
            "recent_added" => SortOrder.RecentlyAdded,
            "recent_read" => SortOrder.RecentlyRead,
            _ => SortOrder.TitleAsc
        };
        NotifyAllSortProps();
        RefreshDisplayedItems();
    }

    private void ToggleUnreadOnly()
    {
        _showUnreadOnly = !_showUnreadOnly;
        RaisePropertyChanged(nameof(ShowUnreadOnly));
        RefreshDisplayedItems();
    }

    private void ToggleFormatFilter(ComicFormat format)
    {
        _formatFilter = _formatFilter == format ? null : format;
        NotifyAllFilterProps();
        RefreshDisplayedItems();
    }

    private void RefreshDisplayedItems()
    {
        IEnumerable<ComicBookListItemViewModel> filtered = _allBooks;

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var q = _searchQuery;
            filtered = filtered.Where(b =>
                b.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (b.SeriesName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
        }

        if (_showUnreadOnly)
            filtered = filtered.Where(b => !b.HasProgress);

        if (_formatFilter.HasValue)
            filtered = filtered.Where(b => b.ComicBook.Format == _formatFilter.Value);

        if (_activeShelfId.HasValue && _shelfEntriesMap.TryGetValue(_activeShelfId.Value, out var shelfBookIds))
            filtered = filtered.Where(b => shelfBookIds.Contains(b.ComicBook.Id));

        var grouped = BuildLibraryItems(filtered.ToList());

        DisplayedItems.Clear();
        foreach (var item in grouped)
            DisplayedItems.Add(item);

        if (_selectedBook is not null)
        {
            _openSingleItem = DisplayedItems
                .OfType<SingleLibraryItemViewModel>()
                .FirstOrDefault(i => i.Item.ComicBook.Id == _selectedBook.ComicBook.Id);
            if (_openSingleItem is not null)
                _openSingleItem.IsCurrentlyOpen = true;
        }

        RaisePropertyChanged(nameof(IsLibraryEmpty));
    }

    private IEnumerable<LibraryItemViewModel> BuildLibraryItems(List<ComicBookListItemViewModel> books)
    {
        const string noSeriesPrefix = "\x0000";
        var groups = books
            .GroupBy(b => b.SeriesName ?? noSeriesPrefix + b.Title)
            .ToList();

        var result = new List<LibraryItemViewModel>();
        foreach (var group in groups)
        {
            var list = group.ToList();
            if (!group.Key.StartsWith(noSeriesPrefix) && list.Count >= 2)
            {
                result.Add(new SeriesLibraryItemViewModel(group.Key, list, OpenSeries));
            }
            else
            {
                // In series-only view, skip standalone books
                if (_isSeriesView) continue;

                foreach (var b in list)
                    result.Add(new SingleLibraryItemViewModel(
                        b,
                        book => OpenBook(book),
                        MarkBookAsRead,
                        MarkBookAsUnread,
                        BuildShelfMenuItems(b.ComicBook.Id)));
            }
        }

        return _sortOrder switch
        {
            SortOrder.TitleAsc => result.OrderBy(GetSortTitle),
            SortOrder.TitleDesc => result.OrderByDescending(GetSortTitle),
            SortOrder.RecentlyAdded => result.OrderByDescending(GetSortAddedAt),
            SortOrder.RecentlyRead => result.OrderByDescending(GetSortRecentlyRead),
            _ => result
        };
    }

    private static string GetSortTitle(LibraryItemViewModel item) => item switch
    {
        SingleLibraryItemViewModel s => s.Item.Title,
        SeriesLibraryItemViewModel series => series.SeriesName,
        _ => ""
    };

    private static DateTimeOffset GetSortAddedAt(LibraryItemViewModel item) => item switch
    {
        SingleLibraryItemViewModel s => s.Item.ComicBook.AddedAt,
        SeriesLibraryItemViewModel series => series.Books.Min(b => b.ComicBook.AddedAt),
        _ => DateTimeOffset.MinValue
    };

    private DateTimeOffset GetSortRecentlyRead(LibraryItemViewModel item)
    {
        IEnumerable<ComicBookListItemViewModel> books = item switch
        {
            SingleLibraryItemViewModel s => [s.Item],
            SeriesLibraryItemViewModel series => series.Books,
            _ => []
        };

        return books
            .Where(b => _progressMap.ContainsKey(b.ComicBook.Id))
            .Select(b => _progressMap[b.ComicBook.Id].UpdatedAt)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
    }

    // --- Settings ---

    private void ToggleSettingsPanel()
    {
        _isSettingsPanelVisible = !_isSettingsPanelVisible;
        RaisePropertyChanged(nameof(IsSettingsPanelVisible));
    }

    private void ToggleFitMode()
    {
        _fitMode = _fitMode == FitMode.FitPage ? FitMode.FitWidth : FitMode.FitPage;
        RaisePropertyChanged(nameof(IsFitPageMode));
        RaisePropertyChanged(nameof(IsFitWidthMode));
        RaisePropertyChanged(nameof(FitModeLabel));
    }

    private void ToggleTheme()
    {
        _isDarkTheme = !_isDarkTheme;
        RaisePropertyChanged(nameof(IsDarkTheme));
        RaisePropertyChanged(nameof(IsLightTheme));
        ApplyTheme();
        _ = SavePreferencesAsync();
    }

    private void SetReadingDirection(string? key)
    {
        _readingDirection = key switch
        {
            "rtl" => ReadingDirection.RightToLeft,
            "ttb" => ReadingDirection.TopToBottom,
            _ => ReadingDirection.LeftToRight
        };
        NotifyAllDirectionProps();

        if (SelectedBook is not null && _readingDirection == ReadingDirection.TopToBottom)
            _ = LoadVerticalPagesAsync(SelectedBook.ComicBook);
        else if (_readingDirection != ReadingDirection.TopToBottom)
        {
            VerticalPages.Clear();
            _ = LoadCurrentPageAsync();
        }

        _ = SavePreferencesAsync();
    }

    private void ApplyTheme()
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant =
            _isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private async Task SavePreferencesAsync()
    {
        await _preferencesStore.SaveAsync(new UserPreferences(_isDarkTheme, _readingDirection));
    }

    // --- Bookmarks ---

    private async Task LoadBookmarksAsync()
    {
        if (SelectedBook is null)
        {
            _currentBookBookmarks = [];
            RaisePropertyChanged(nameof(IsCurrentPageBookmarked));
            RaisePropertyChanged(nameof(BookmarkIcon));
            return;
        }

        var bookmarks = await _bookmarkRepository.GetForComicAsync(SelectedBook.ComicBook.Id);
        _currentBookBookmarks = [..bookmarks];
        RaisePropertyChanged(nameof(IsCurrentPageBookmarked));
        RaisePropertyChanged(nameof(BookmarkIcon));
    }

    private async Task ToggleBookmarkAsync()
    {
        if (SelectedBook is null) return;

        if (IsCurrentPageBookmarked)
        {
            await _bookmarkRepository.DeleteForPageAsync(SelectedBook.ComicBook.Id, _currentPageIndex);
            _currentBookBookmarks.RemoveAll(b => b.PageNumber == _currentPageIndex);
        }
        else
        {
            var bookmark = new Bookmark(
                Guid.NewGuid(),
                SelectedBook.ComicBook.Id,
                _currentPageIndex,
                null,
                DateTimeOffset.UtcNow);
            await _bookmarkRepository.AddAsync(bookmark);
            _currentBookBookmarks.Add(bookmark);
        }

        RaisePropertyChanged(nameof(IsCurrentPageBookmarked));
        RaisePropertyChanged(nameof(BookmarkIcon));
    }

    // --- Reader ---

    private void MovePageByIndex(int delta)
    {
        if (SelectedBook is null) return;
        var next = Math.Clamp(
            CurrentPageIndex + delta,
            0,
            Math.Max(0, SelectedBook.ComicBook.PageCount - 1));
        if (next == CurrentPageIndex) return;
        CurrentPageIndex = next;
        _ = LoadCurrentPageAsync();
        _ = SaveProgressAsync();
    }

    private void JumpToPage(int pageIndex)
    {
        if (SelectedBook is null) return;
        var clamped = Math.Clamp(pageIndex, 0, Math.Max(0, SelectedBook.ComicBook.PageCount - 1));
        if (clamped == CurrentPageIndex) return;
        CurrentPageIndex = clamped;
        _ = LoadCurrentPageAsync();
        _ = SaveProgressAsync();
    }

    private async Task LoadCurrentPageAsync()
    {
        CurrentPageImage = null;
        IsPageLoading = false;

        if (SelectedBook is null)
        {
            ReaderStatus = "Select a book to start reading.";
            return;
        }

        var book = SelectedBook.ComicBook;
        if (book.Format is ComicFormat.Cbr or ComicFormat.Rar or ComicFormat.SevenZip or ComicFormat.Epub)
        {
            ReaderStatus = $"{book.Format} format not supported yet.";
            RaisePropertyChanged(nameof(CurrentPageLabel));
            RefreshAllNavCanExecute();
            return;
        }

        IsPageLoading = true;
        ReaderStatus = book.Title;
        CurrentPageImage = await _pagePreviewLoader.LoadPageAsync(book, CurrentPageIndex);
        IsPageLoading = false;

        if (CurrentPageImage is null)
            ReaderStatus = "Page could not be loaded.";

        RaisePropertyChanged(nameof(CurrentPageLabel));
        RefreshAllNavCanExecute();
    }

    private async Task LoadVerticalPagesAsync(ComicBook comicBook)
    {
        _verticalPagesCts?.Cancel();
        _verticalPagesCts = new CancellationTokenSource();
        var cts = _verticalPagesCts;

        VerticalPages.Clear();
        IsPageLoading = true;
        ReaderStatus = $"Loading {comicBook.Title}...";

        try
        {
            var pages = await _pagePreviewLoader.LoadPagesAsync(comicBook, cts.Token);
            if (cts.IsCancellationRequested) return;

            foreach (var page in pages)
            {
                if (cts.IsCancellationRequested) break;
                VerticalPages.Add(page);
            }

            ReaderStatus = comicBook.Title;
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsPageLoading = false;
        }
    }

    private static readonly string _coverCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Comixa", "Desktop", "covers");

    private async Task LoadCoversAsync()
    {
        Directory.CreateDirectory(_coverCacheDir);
        var semaphore = new SemaphoreSlim(4);
        var tasks = _allBooks.ToList().Select(async book =>
        {
            await semaphore.WaitAsync();
            try
            {
                var cover = await LoadCoverWithCacheAsync(book.ComicBook);
                book.SetCoverImage(cover);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    private async Task<Bitmap?> LoadCoverWithCacheAsync(ComicBook comicBook)
    {
        var cacheFile = Path.Combine(_coverCacheDir, $"{comicBook.Id}.png");

        if (File.Exists(cacheFile))
        {
            try { return await Task.Run(() => new Bitmap(cacheFile)); }
            catch { /* regenerate if cache is corrupted */ }
        }

        var fullPage = await _pagePreviewLoader.LoadPageAsync(comicBook, 0);
        if (fullPage is null) return null;

        var thumbnail = await Task.Run(() => CreateThumbnail(fullPage, 200, 300));
        if (!ReferenceEquals(thumbnail, fullPage))
            fullPage.Dispose();

        try { await Task.Run(() => thumbnail.Save(cacheFile)); }
        catch { /* ignore cache write errors (read-only fs, permissions, etc.) */ }

        return thumbnail;
    }

    private static Bitmap CreateThumbnail(Bitmap source, int maxWidth, int maxHeight)
    {
        if (source.PixelSize.Width <= maxWidth && source.PixelSize.Height <= maxHeight)
            return source;

        var scaleX = (double)maxWidth / source.PixelSize.Width;
        var scaleY = (double)maxHeight / source.PixelSize.Height;
        var scale = Math.Min(scaleX, scaleY);

        var newWidth = Math.Max(1, (int)(source.PixelSize.Width * scale));
        var newHeight = Math.Max(1, (int)(source.PixelSize.Height * scale));

        return source.CreateScaledBitmap(
            new PixelSize(newWidth, newHeight),
            BitmapInterpolationMode.LowQuality);
    }

    private async Task SaveProgressAsync()
    {
        if (SelectedBook is null) return;
        var progress = new ReadingProgress(SelectedBook.ComicBook.Id, CurrentPageIndex, DateTimeOffset.UtcNow);
        await _progressRepository.SaveAsync(progress);
        SelectedBook.SetProgress(progress);
        _progressMap[progress.ComicBookId] = progress;
    }

    // --- Notification helpers ---

    private void RefreshAllNavCanExecute()
    {
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
        PageBackwardCommand.RaiseCanExecuteChanged();
        PageForwardCommand.RaiseCanExecuteChanged();
        GoToFirstPageCommand.RaiseCanExecuteChanged();
        GoToLastPageCommand.RaiseCanExecuteChanged();
    }

    private void NotifyAllSortProps()
    {
        RaisePropertyChanged(nameof(IsSortTitleAsc));
        RaisePropertyChanged(nameof(IsSortTitleDesc));
        RaisePropertyChanged(nameof(IsSortRecentlyAdded));
        RaisePropertyChanged(nameof(IsSortRecentlyRead));
    }

    private void NotifyAllFilterProps()
    {
        RaisePropertyChanged(nameof(IsCbzFilterActive));
        RaisePropertyChanged(nameof(IsZipFilterActive));
        RaisePropertyChanged(nameof(IsPdfFilterActive));
    }

    private void NotifyAllDirectionProps()
    {
        RaisePropertyChanged(nameof(IsLeftToRight));
        RaisePropertyChanged(nameof(IsRightToLeft));
        RaisePropertyChanged(nameof(IsTopToBottom));
        RaisePropertyChanged(nameof(IsVerticalMode));
    }
}
