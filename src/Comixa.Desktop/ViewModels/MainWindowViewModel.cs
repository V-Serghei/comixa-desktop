using System.Collections.ObjectModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Comixa.Core.Models;
using Comixa.Core.Repositories;
using Comixa.Desktop.Reader;
using Comixa.Desktop.Services;
using Comixa.Reader.Archives;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const double LibraryCardSlotWidth = 120;
    private const double LibraryViewportPadding = 48;
    private const int MaxLibraryItemsPerRow = 40;

    private readonly IFolderPicker _folderPicker;
    private readonly IComicLibraryScanner _comicLibraryScanner;
    private readonly IUserLibrarySettingsStore _settingsStore;
    private readonly IPagePreviewLoader _pagePreviewLoader;
    private readonly IComicLibraryRepository _comicRepository;
    private readonly IReadingProgressRepository _progressRepository;
    private readonly IUserPreferencesStore _preferencesStore;
    private readonly IShelfRepository _shelfRepository;
    private readonly IBookmarkRepository _bookmarkRepository;
    private readonly ICoverImageCache _coverImageCache;

    private readonly List<ComicBookListItemViewModel> _allBooks = [];
    private readonly Dictionary<Guid, ReadingProgress> _progressMap = [];
    private readonly List<Shelf> _shelves = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _shelfEntriesMap = [];

    // Navigation state
    private bool _isSeriesView;
    private Guid? _activeShelfId;
    private string _activeShelfName = "";
    private string? _activeFolderPath;

    // Library filter state
    private string _searchQuery = "";
    private string _normalizedSearchQuery = "";
    private SortOrder _sortOrder = SortOrder.TitleAsc;
    private bool _showUnreadOnly;
    private LibraryStatusFilter _statusFilter = LibraryStatusFilter.All;
    private ComicFormat? _formatFilter;

    // Series detail state
    private bool _isInSeriesDetail;
    private string _currentSeriesName = "";
    private IReadOnlyList<ComicBookListItemViewModel> _seriesBooks = [];

    // Settings state
    private bool _isSettingsPanelVisible;
    private bool _isReaderSettingsPanelVisible;
    private bool _isDarkTheme = true;
    private ReadingDirection _readingDirection = ReadingDirection.LeftToRight;
    private FitMode _fitMode = FitMode.FitPage;
    private ReaderWheelAction _readerWheelAction = ReaderWheelAction.Scroll;
    private bool _isEdgePageTurnEnabled = true;
    private bool _isDragPageTurnEnabled = true;
    private bool _isPageTurnInverted;
    private ReaderColorTone _readerColorTone = ReaderColorTone.Original;
    private ReaderPageAnimation _readerPageAnimation = ReaderPageAnimation.Fade;
    private bool _isTwoPageMode;
    private bool _isReaderFullscreen;
    private bool _openComicsAtLastPosition = true;
    private bool _openComicsInFullscreen;
    private bool _isReaderPreviewPaneEnabled = true;
    private bool _openPreviousChapterAtLastPage = true;

    // Shelf creation state
    private bool _isCreatingShelf;
    private string _newShelfName = "";

    // Reader state
    private ComicBookListItemViewModel? _selectedBook;
    private Bitmap? _currentPageImage;
    private Bitmap? _nextPageImage;
    private int _currentPageIndex;
    private string _statusMessage = "Add a local folder to begin.";
    private string _readerStatus = "Select a book to start reading.";
    private bool _isLoadingLibrary;
    private bool _isPageLoading;
    private double _readerZoom = 1.0;
    private ComicBookListItemViewModel? _nextSeriesBook;
    private ComicBookListItemViewModel? _previousSeriesBook;
    private bool _isNextSeriesBookPromptVisible;
    private bool _isPreviousSeriesBookPromptVisible;
    private string _nextSeriesBookActionLabel = "";
    private string _previousSeriesBookActionLabel = "";

    // Bookmarks
    private List<Bookmark> _currentBookBookmarks = [];

    private CancellationTokenSource? _verticalPagesCts;
    private CancellationTokenSource? _currentPageCts;
    private CancellationTokenSource? _bookPreloadCts;
    private CancellationTokenSource? _coverLoadCts;
    private Guid? _preloadingBookId;
    private int _preloadAnchorPageIndex = -1;
    private int _currentPageLoadVersion;
    private SingleLibraryItemViewModel? _openSingleItem;
    private int _libraryItemsPerRow = 2;
    private int? _nextOpenPageIndexOverride;

    public MainWindowViewModel(
        IFolderPicker folderPicker,
        IComicLibraryScanner comicLibraryScanner,
        IUserLibrarySettingsStore settingsStore,
        IPagePreviewLoader pagePreviewLoader,
        IComicLibraryRepository comicRepository,
        IReadingProgressRepository progressRepository,
        IUserPreferencesStore preferencesStore,
        IShelfRepository shelfRepository,
        IBookmarkRepository bookmarkRepository,
        ICoverImageCache coverImageCache)
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
        _coverImageCache = coverImageCache;

        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
        RefreshLibraryCommand = new AsyncRelayCommand(RefreshLibraryAsync);
        BackToLibraryCommand = new RelayCommand(BackToLibrary);
        OpenBookCommand = new RelayCommand<ComicBookListItemViewModel>(OpenBook);
        OpenBookFullscreenCommand = new RelayCommand<ComicBookListItemViewModel>(OpenBookFullscreen);

        // Navigation commands
        NavigateToAllBooksCommand = new RelayCommand(NavigateToAllBooks);
        NavigateToSeriesCommand = new RelayCommand(NavigateToSeries);
        NavigateToStartedCommand = new RelayCommand(() => NavigateToStatusFilter(LibraryStatusFilter.Started));
        NavigateToReadCommand = new RelayCommand(() => NavigateToStatusFilter(LibraryStatusFilter.Read));
        NavigateToUnreadCommand = new RelayCommand(() => NavigateToStatusFilter(LibraryStatusFilter.Unread));

        // UI Prev/Next (direction-aware for RTL)
        PreviousPageCommand = new RelayCommand(
            () => MovePageByIndex(_readingDirection == ReadingDirection.RightToLeft ? PageStep : -PageStep),
            () => SelectedBook is not null &&
                  (_readingDirection == ReadingDirection.RightToLeft
                      ? CurrentPageIndex + PageStep < (SelectedBook?.ComicBook.PageCount ?? 0) || _nextSeriesBook is not null
                      : CurrentPageIndex > 0 || _previousSeriesBook is not null));
        NextPageCommand = new RelayCommand(
            () => MovePageByIndex(_readingDirection == ReadingDirection.RightToLeft ? -PageStep : PageStep),
            () => SelectedBook is not null &&
                  (_readingDirection == ReadingDirection.RightToLeft
                      ? CurrentPageIndex > 0 || _previousSeriesBook is not null
                      : CurrentPageIndex + PageStep < (SelectedBook?.ComicBook.PageCount ?? 0) || _nextSeriesBook is not null));

        PageBackwardCommand = new RelayCommand(
            () => MovePageByIndex(-PageStep),
            () => SelectedBook is not null && (CurrentPageIndex > 0 || _previousSeriesBook is not null));
        PageForwardCommand = new RelayCommand(
            () => MovePageByIndex(PageStep),
            () => SelectedBook is not null && (CurrentPageIndex + PageStep < (SelectedBook?.ComicBook.PageCount ?? 0) || _nextSeriesBook is not null));
        GoToFirstPageCommand = new RelayCommand(
            () => JumpToPage(0),
            () => SelectedBook is not null && CurrentPageIndex > 0);
        GoToLastPageCommand = new RelayCommand(
            () => JumpToPage(Math.Max(0, (SelectedBook?.ComicBook.PageCount ?? 1) - 1)),
            () => SelectedBook is not null &&
                  CurrentPageIndex < (SelectedBook?.ComicBook.PageCount ?? 1) - 1);

        ToggleSettingsPanelCommand = new RelayCommand(ToggleSettingsPanel);
        ToggleReaderSettingsPanelCommand = new RelayCommand(ToggleReaderSettingsPanel);
        ToggleFitModeCommand = new RelayCommand(ToggleFitMode);
        ToggleReaderFullscreenCommand = new RelayCommand(ToggleReaderFullscreen);
        ToggleOpenComicsAtLastPositionCommand = new RelayCommand(ToggleOpenComicsAtLastPosition);
        ToggleOpenComicsInFullscreenCommand = new RelayCommand(ToggleOpenComicsInFullscreen);
        ToggleReaderPreviewPaneCommand = new RelayCommand(ToggleReaderPreviewPane);
        ToggleOpenPreviousChapterAtLastPageCommand = new RelayCommand(ToggleOpenPreviousChapterAtLastPage);
        ToggleTwoPageModeCommand = new RelayCommand(ToggleTwoPageMode);
        ToggleEdgePageTurnsCommand = new RelayCommand(ToggleEdgePageTurns);
        ToggleDragPageTurnsCommand = new RelayCommand(ToggleDragPageTurns);
        TogglePageTurnInversionCommand = new RelayCommand(TogglePageTurnInversion);
        ResetReaderDefaultsCommand = new RelayCommand(ResetReaderDefaults);
        SetReaderWheelActionCommand = new RelayCommand<string>(SetReaderWheelAction);
        SetReaderColorToneCommand = new RelayCommand<string>(SetReaderColorTone);
        SetReaderAnimationCommand = new RelayCommand<string>(SetReaderAnimation);
        SetSortOrderCommand = new RelayCommand<string>(SetSortOrder);
        ToggleUnreadOnlyCommand = new RelayCommand(ToggleUnreadOnly);
        ToggleCbzFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Cbz));
        ToggleZipFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Zip));
        TogglePdfFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Pdf));
        ToggleCbrFilterCommand = new RelayCommand(() => ToggleFormatFilter(ComicFormat.Cbr));
        SetReadingDirectionCommand = new RelayCommand<string>(SetReadingDirection);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ClearSearchCommand = new RelayCommand(() => SearchQuery = "");

        StartCreateShelfCommand = new RelayCommand(StartCreatingShelf);
        ConfirmNewShelfCommand = new AsyncRelayCommand(CreateShelfAsync);
        CancelNewShelfCommand = new RelayCommand(() =>
        {
            _isCreatingShelf = false;
            _newShelfName = "";
            RaisePropertyChanged(nameof(IsCreatingShelf));
            RaisePropertyChanged(nameof(NewShelfName));
        });

        ToggleBookmarkCommand = new AsyncRelayCommand(ToggleBookmarkAsync);
        OpenPreviousSeriesBookCommand = new RelayCommand(OpenPreviousSeriesBook, () => HasPreviousSeriesBookPrompt);
        OpenNextSeriesBookCommand = new RelayCommand(OpenNextSeriesBook, () => HasNextSeriesBookPrompt);

        _ = InitializeAsync();
    }

    // Collections
    public ObservableCollection<LibraryItemViewModel> DisplayedItems { get; } = [];
    public ObservableCollection<LibraryRowViewModel> DisplayedRows { get; } = [];
    public ObservableCollection<SeriesVariantViewModel> SeriesVariants { get; } = [];
    public ObservableCollection<ReaderPageViewModel> VerticalPages { get; } = [];
    public ObservableCollection<ShelfViewModel> Shelves { get; } = [];
    public ObservableCollection<LibraryFolderViewModel> LibraryFolders { get; } = [];

    // Commands - navigation
    public RelayCommand NavigateToAllBooksCommand { get; }
    public RelayCommand NavigateToSeriesCommand { get; }
    public RelayCommand NavigateToStartedCommand { get; }
    public RelayCommand NavigateToReadCommand { get; }
    public RelayCommand NavigateToUnreadCommand { get; }

    // Commands - reader
    public AsyncRelayCommand ScanFolderCommand { get; }
    public AsyncRelayCommand RefreshLibraryCommand { get; }
    public RelayCommand BackToLibraryCommand { get; }
    public RelayCommand<ComicBookListItemViewModel> OpenBookCommand { get; }
    public RelayCommand<ComicBookListItemViewModel> OpenBookFullscreenCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public RelayCommand PageBackwardCommand { get; }
    public RelayCommand PageForwardCommand { get; }
    public RelayCommand GoToFirstPageCommand { get; }
    public RelayCommand GoToLastPageCommand { get; }
    public RelayCommand ToggleFitModeCommand { get; }
    public RelayCommand ToggleReaderFullscreenCommand { get; }
    public RelayCommand ToggleReaderSettingsPanelCommand { get; }
    public RelayCommand ToggleOpenComicsAtLastPositionCommand { get; }
    public RelayCommand ToggleOpenComicsInFullscreenCommand { get; }
    public RelayCommand ToggleReaderPreviewPaneCommand { get; }
    public RelayCommand ToggleOpenPreviousChapterAtLastPageCommand { get; }
    public RelayCommand ToggleTwoPageModeCommand { get; }
    public RelayCommand ToggleEdgePageTurnsCommand { get; }
    public RelayCommand ToggleDragPageTurnsCommand { get; }
    public RelayCommand TogglePageTurnInversionCommand { get; }
    public RelayCommand ResetReaderDefaultsCommand { get; }
    public RelayCommand<string> SetReaderWheelActionCommand { get; }
    public RelayCommand<string> SetReaderColorToneCommand { get; }
    public RelayCommand<string> SetReaderAnimationCommand { get; }
    public AsyncRelayCommand ToggleBookmarkCommand { get; }
    public RelayCommand OpenPreviousSeriesBookCommand { get; }
    public RelayCommand OpenNextSeriesBookCommand { get; }

    // Commands - library
    public RelayCommand ToggleSettingsPanelCommand { get; }
    public RelayCommand<string> SetSortOrderCommand { get; }
    public RelayCommand ToggleUnreadOnlyCommand { get; }
    public RelayCommand ToggleCbzFilterCommand { get; }
    public RelayCommand ToggleZipFilterCommand { get; }
    public RelayCommand TogglePdfFilterCommand { get; }
    public RelayCommand ToggleCbrFilterCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

    // Commands - shelves
    public RelayCommand StartCreateShelfCommand { get; }
    public AsyncRelayCommand ConfirmNewShelfCommand { get; }
    public RelayCommand CancelNewShelfCommand { get; }

    // Commands - settings
    public RelayCommand<string> SetReadingDirectionCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public void SetLibraryViewportWidth(double width)
    {
        if (double.IsNaN(width) || width <= 0)
        {
            return;
        }

        var usableWidth = Math.Max(1, width - LibraryViewportPadding);
        var nextItemsPerRow = Math.Clamp(
            (int)Math.Floor(usableWidth / LibraryCardSlotWidth),
            1,
            MaxLibraryItemsPerRow);

        if (nextItemsPerRow == _libraryItemsPerRow)
        {
            return;
        }

        _libraryItemsPerRow = nextItemsPerRow;
        RebuildDisplayedRows();
        if (_isInSeriesDetail)
        {
            RebuildSeriesVariants();
            RaisePropertyChanged(nameof(HasSeriesVariants));
        }
    }

    // Window title
    public string Title => SelectedBook is null
        ? "Comixa Desktop"
        : $"{SelectedBook.Title} - Comixa";

    // Navigation state
    public bool IsViewAllBooks =>
        _activeShelfId is null &&
        _activeFolderPath is null &&
        !_isSeriesView &&
        _statusFilter == LibraryStatusFilter.All;

    public bool IsViewSeries => _activeShelfId is null && _activeFolderPath is null && _isSeriesView;
    public bool IsViewStarted => _activeShelfId is null && _activeFolderPath is null && _statusFilter == LibraryStatusFilter.Started;
    public bool IsViewRead => _activeShelfId is null && _activeFolderPath is null && _statusFilter == LibraryStatusFilter.Read;
    public bool IsViewUnread => _activeShelfId is null && _activeFolderPath is null && _statusFilter == LibraryStatusFilter.Unread;
    public bool IsHomeView => IsViewAllBooks;
    public bool IsLibraryToolsVisible => !IsHomeView;
    public bool HasLibraryFolders => LibraryFolders.Count > 0;

    public string LibraryViewTitle
    {
        get
        {
            if (_activeShelfId.HasValue)
            {
                return _activeShelfName;
            }

            if (_activeFolderPath is not null)
            {
                return $"Folder: {GetFolderDisplayName(_activeFolderPath)}";
            }

            if (_isSeriesView)
            {
                return "Series";
            }

            return _statusFilter switch
            {
                LibraryStatusFilter.Started => "Started",
                LibraryStatusFilter.Read => "Read",
                LibraryStatusFilter.Unread => "Unread",
                _ => _allBooks.Any(book => book.IsStarted) ? "Continue Reading" : "Recommended Series"
            };
        }
    }

    // Library filter state
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value) return;
            _searchQuery = value;
            _normalizedSearchQuery = value.Trim().ToUpperInvariant();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasSearchQuery));
            RefreshDisplayedItems();
        }
    }

    public bool HasSearchQuery => _normalizedSearchQuery.Length > 0;
    public bool ShowUnreadOnly => _showUnreadOnly;
    public bool IsStatusAllActive => _statusFilter == LibraryStatusFilter.All;
    public bool IsStatusStartedActive => _statusFilter == LibraryStatusFilter.Started;
    public bool IsStatusReadActive => _statusFilter == LibraryStatusFilter.Read;
    public bool IsStatusUnreadActive => _statusFilter == LibraryStatusFilter.Unread;
    public bool IsCbzFilterActive => _formatFilter == ComicFormat.Cbz;
    public bool IsZipFilterActive => _formatFilter == ComicFormat.Zip;
    public bool IsPdfFilterActive => _formatFilter == ComicFormat.Pdf;
    public bool IsCbrFilterActive => _formatFilter == ComicFormat.Cbr;
    public bool IsSortTitleAsc => _sortOrder == SortOrder.TitleAsc;
    public bool IsSortTitleDesc => _sortOrder == SortOrder.TitleDesc;
    public bool IsSortRecentlyAdded => _sortOrder == SortOrder.RecentlyAdded;
    public bool IsSortRecentlyRead => _sortOrder == SortOrder.RecentlyRead;

    public bool IsInSeriesDetail => _isInSeriesDetail;
    public string CurrentSeriesName => _currentSeriesName;
    public IReadOnlyList<ComicBookListItemViewModel> SeriesBooks => _seriesBooks;
    public bool HasSeriesVariants => SeriesVariants.Count > 0;
    public bool IsLibraryEmpty => DisplayedItems.Count == 0 && !_isLoadingLibrary;

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
    public bool IsReaderSettingsPanelVisible => _isReaderSettingsPanelVisible;
    public bool IsDarkTheme => _isDarkTheme;
    public bool IsLightTheme => !_isDarkTheme;
    public string ThemeToggleLabel => _isDarkTheme ? "Light" : "Dark";
    public bool IsLeftToRight => _readingDirection == ReadingDirection.LeftToRight;
    public bool IsRightToLeft => _readingDirection == ReadingDirection.RightToLeft;
    public bool IsTopToBottom => _readingDirection == ReadingDirection.TopToBottom;
    public bool IsVerticalMode => _readingDirection == ReadingDirection.TopToBottom;
    public bool IsReaderFullscreen => _isReaderFullscreen;
    public string ReaderFullscreenLabel => _isReaderFullscreen ? "Exit Fullscreen" : "Fullscreen";
    public bool OpenComicsAtLastPosition => _openComicsAtLastPosition;
    public bool OpenComicsInFullscreen => _openComicsInFullscreen;
    public bool IsReaderPreviewPaneEnabled => _isReaderPreviewPaneEnabled;
    public bool OpenPreviousChapterAtLastPage => _openPreviousChapterAtLastPage;
    public string ReaderPreviewPaneLabel => _isReaderPreviewPaneEnabled ? "Hide Reader" : "Show Reader";
    public GridLength LibraryPaneWidth =>
        _isReaderPreviewPaneEnabled ? new GridLength(340) : new GridLength(1, GridUnitType.Star);
    public GridLength ReaderPreviewSplitterWidth =>
        _isReaderPreviewPaneEnabled ? new GridLength(4) : new GridLength(0);
    public GridLength ReaderPreviewPaneWidth =>
        _isReaderPreviewPaneEnabled ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    public bool IsTwoPageMode => _isTwoPageMode;
    public bool IsSinglePageMode => !_isTwoPageMode;
    public string PageLayoutLabel => _isTwoPageMode ? "2 Pages" : "1 Page";
    public bool IsEdgePageTurnEnabled => _isEdgePageTurnEnabled;
    public bool IsDragPageTurnEnabled => _isDragPageTurnEnabled;
    public bool IsPageTurnInverted => _isPageTurnInverted;
    public ReaderWheelAction ReaderWheelAction => _readerWheelAction;
    public bool IsWheelScrollAction => _readerWheelAction == ReaderWheelAction.Scroll;
    public bool IsWheelPageTurnAction => _readerWheelAction == ReaderWheelAction.PageTurn;
    public bool IsWheelZoomAction => _readerWheelAction == ReaderWheelAction.Zoom;
    public ReaderColorTone ReaderColorTone => _readerColorTone;
    public bool IsReaderToneOriginal => _readerColorTone == ReaderColorTone.Original;
    public bool IsReaderToneWarm => _readerColorTone == ReaderColorTone.Warm;
    public bool IsReaderTonePaper => _readerColorTone == ReaderColorTone.Paper;
    public bool IsReaderToneCool => _readerColorTone == ReaderColorTone.Cool;
    public ReaderPageAnimation ReaderPageAnimation => _readerPageAnimation;
    public bool IsReaderAnimationNone => _readerPageAnimation == ReaderPageAnimation.None;
    public bool IsReaderAnimationFade => _readerPageAnimation == ReaderPageAnimation.Fade;
    public bool IsReaderAnimationSlide => _readerPageAnimation == ReaderPageAnimation.Slide;
    public IBrush ReaderBackgroundBrush => _readerColorTone switch
    {
        ReaderColorTone.Warm => new SolidColorBrush(Color.FromRgb(31, 23, 15)),
        ReaderColorTone.Paper => new SolidColorBrush(Color.FromRgb(239, 229, 207)),
        ReaderColorTone.Cool => new SolidColorBrush(Color.FromRgb(8, 13, 23)),
        _ => Brushes.Black
    };

    public IBrush ReaderPageTintBrush => _readerColorTone switch
    {
        ReaderColorTone.Warm => new SolidColorBrush(Color.FromArgb(34, 219, 149, 79)),
        ReaderColorTone.Paper => new SolidColorBrush(Color.FromArgb(30, 242, 210, 138)),
        ReaderColorTone.Cool => new SolidColorBrush(Color.FromArgb(30, 70, 120, 255)),
        _ => Brushes.Transparent
    };

    public bool IsReaderTintVisible => _readerColorTone != ReaderColorTone.Original;

    // Fit mode
    public bool IsFitPageMode => _fitMode == FitMode.FitPage;
    public bool IsFitWidthMode => _fitMode == FitMode.FitWidth;
    public string FitModeLabel => _fitMode == FitMode.FitPage ? "Fit Page" : "Fit Width";
    public double ReaderZoom => _readerZoom;
    public string ZoomLabel => $"{_readerZoom:P0}";
    private int PageStep => _isTwoPageMode && !IsVerticalMode ? 2 : 1;

    // Reader state
    public ComicBookListItemViewModel? SelectedBook
    {
        get => _selectedBook;
        private set
        {
            if (_selectedBook == value) return;
            CancelBookPreload();
            HideSeriesBookPrompts();
            _selectedBook = value;
            CurrentPageIndex = _nextOpenPageIndexOverride ?? GetInitialPageIndex(value);
            _nextOpenPageIndexOverride = null;
            UpdateSeriesBookNavigation();
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
            RaisePropertyChanged(nameof(IsSinglePageVisible));
            RaisePropertyChanged(nameof(IsTwoPageVisible));
        }
    }

    public bool HasCurrentPageImage => CurrentPageImage is not null;
    public bool IsSinglePageVisible => HasCurrentPageImage && (!_isTwoPageMode || IsVerticalMode);
    public bool IsTwoPageVisible => HasCurrentPageImage && _isTwoPageMode && !IsVerticalMode;

    public Bitmap? NextPageImage
    {
        get => _nextPageImage;
        private set
        {
            if (_nextPageImage == value) return;
            _nextPageImage = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasNextPageImage));
        }
    }

    public bool HasNextPageImage => NextPageImage is not null;

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
            var pageCount = SelectedBook.ComicBook.PageCount;
            if (pageCount == 0)
            {
                return "No readable pages";
            }

            if (_isTwoPageMode && !IsVerticalMode && CurrentPageIndex + 1 < pageCount)
            {
                return $"{CurrentPageIndex + 1}-{CurrentPageIndex + 2} / {pageCount}";
            }

            return $"{CurrentPageIndex + 1} / {pageCount}";
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

    public string BookmarkIcon => IsCurrentPageBookmarked ? "Bookmarked" : "Bookmark";

    public bool HasNextSeriesBookPrompt => _isNextSeriesBookPromptVisible && _nextSeriesBook is not null;

    public string NextSeriesBookActionLabel => _nextSeriesBookActionLabel;

    public bool HasPreviousSeriesBookPrompt => _isPreviousSeriesBookPromptVisible && _previousSeriesBook is not null;

    public string PreviousSeriesBookActionLabel => _previousSeriesBookActionLabel;

    private int GetInitialPageIndex(ComicBookListItemViewModel? book)
    {
        if (book is null || !_openComicsAtLastPosition)
        {
            return 0;
        }

        var pageCount = Math.Max(0, book.ComicBook.PageCount - 1);
        if (!_progressMap.TryGetValue(book.ComicBook.Id, out var progress))
        {
            return 0;
        }

        var pageNumber = Math.Clamp(progress.PageNumber, 0, pageCount);
        return pageCount > 0 && pageNumber >= pageCount ? 0 : pageNumber;
    }

    // --- Initialization ---

    private async Task InitializeAsync()
    {
        var prefs = await _preferencesStore.LoadAsync();
        _isDarkTheme = prefs.IsDarkTheme;
        _readingDirection = prefs.ReadingDirection;
        _readerWheelAction = prefs.ReaderWheelAction;
        _isEdgePageTurnEnabled = prefs.IsEdgePageTurnEnabled;
        _isDragPageTurnEnabled = prefs.IsDragPageTurnEnabled;
        _isPageTurnInverted = prefs.IsPageTurnInverted;
        _readerColorTone = prefs.ReaderColorTone;
        _readerPageAnimation = prefs.ReaderPageAnimation;
        _isTwoPageMode = prefs.IsTwoPageMode;
        _openComicsAtLastPosition = prefs.OpenComicsAtLastPosition;
        _openComicsInFullscreen = prefs.OpenComicsInFullscreen;
        _isReaderPreviewPaneEnabled = prefs.IsReaderPreviewPaneEnabled;
        _openPreviousChapterAtLastPage = prefs.OpenPreviousChapterAtLastPage;
        ApplyTheme();
        RaisePropertyChanged(nameof(IsDarkTheme));
        RaisePropertyChanged(nameof(IsLightTheme));
        RaisePropertyChanged(nameof(ThemeToggleLabel));
        NotifyAllDirectionProps();
        NotifyGlobalSettingsProps();
        NotifyReaderSettingsProps();

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
        RebuildFolderVMs(settings.WatchedFolders);
        await LoadLibraryFromDatabaseAsync();
        if (settings.WatchedFolders.Count == 0) return;
        _ = ScanFoldersAsync(settings.WatchedFolders);
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
        RebuildFolderVMs(folders);
        await ScanFoldersAsync(folders);
    }

    private async Task RefreshLibraryAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        RebuildFolderVMs(settings.WatchedFolders);
        if (settings.WatchedFolders.Count == 0)
        {
            StatusMessage = "No watched folders yet.";
            return;
        }

        await ScanFoldersAsync(settings.WatchedFolders);
    }

    private async Task ScanFoldersAsync(IReadOnlyList<string> folders)
    {
        _isLoadingLibrary = true;
        StatusMessage = "Scanning library...";
        RaisePropertyChanged(nameof(IsLibraryEmpty));

        var existingFolders = folders.Where(Directory.Exists).ToArray();
        var scannedFiles = new List<ScannedComicFile>();
        try
        {
            foreach (var folder in existingFolders)
                scannedFiles.AddRange(await _comicLibraryScanner.ScanAsync(folder));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await LoadLibraryFromDatabaseAsync("Scan failed. Showing saved library.");
            return;
        }

        if (existingFolders.Length > 0 && scannedFiles.Count == 0)
        {
            await LoadLibraryFromDatabaseAsync(_allBooks.Count > 0
                ? $"{_allBooks.Count} saved comics shown. Scan found no readable files."
                : "No CBZ, ZIP, PDF, CBR or image folders found.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var scannedPaths = scannedFiles
            .Select(file => file.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in scannedFiles)
            await _comicRepository.UpsertAsync(file.ToComicBook(now));

        var allDbBooks = await _comicRepository.GetAllAsync();
        var obsoleteBooks = allDbBooks
            .Where(book => existingFolders.Any(folder => IsInsideFolder(book.FilePath, folder))
                && !scannedPaths.Contains(book.FilePath))
            .ToArray();

        foreach (var obsoleteBook in obsoleteBooks)
        {
            await _comicRepository.DeleteAsync(obsoleteBook.Id);
        }

        await LoadLibraryFromDatabaseAsync();
    }

    private async Task LoadLibraryFromDatabaseAsync(string? statusMessage = null)
    {
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
        StatusMessage = statusMessage ?? (_allBooks.Count > 0
            ? $"{_allBooks.Count} comic{(_allBooks.Count == 1 ? "" : "s")} in library"
            : "No CBZ, ZIP, PDF, CBR or image folders found.");

        RaisePropertyChanged(nameof(IsLibraryEmpty));
        RaisePropertyChanged(nameof(LibraryViewTitle));
        UpdateSeriesBookNavigation();
        RefreshDisplayedItems();

        _ = LoadCoversAsync();
    }

    // --- Navigation ---

    private void NavigateToAllBooks()
    {
        _isSeriesView = false;
        _activeShelfId = null;
        _activeShelfName = "";
        _activeFolderPath = null;
        _statusFilter = LibraryStatusFilter.All;
        _isInSeriesDetail = false;
        ResetLibraryFilters();
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private void NavigateToSeries()
    {
        _isSeriesView = true;
        _activeShelfId = null;
        _activeShelfName = "";
        _activeFolderPath = null;
        _statusFilter = LibraryStatusFilter.All;
        _isInSeriesDetail = false;
        ResetLibraryFilters();
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private void NavigateToStatusFilter(LibraryStatusFilter filter)
    {
        _isSeriesView = false;
        _activeShelfId = null;
        _activeShelfName = "";
        _activeFolderPath = null;
        _statusFilter = filter;
        _showUnreadOnly = filter == LibraryStatusFilter.Unread;
        _isInSeriesDetail = false;
        RaisePropertyChanged(nameof(ShowUnreadOnly));
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private void NotifyNavigationProps()
    {
        foreach (var vm in Shelves)
            vm.IsActive = vm.Id == _activeShelfId;
        foreach (var vm in LibraryFolders)
            vm.IsActive = _activeFolderPath is not null && vm.Path.Equals(_activeFolderPath, StringComparison.OrdinalIgnoreCase);

        RaisePropertyChanged(nameof(IsViewAllBooks));
        RaisePropertyChanged(nameof(IsHomeView));
        RaisePropertyChanged(nameof(IsLibraryToolsVisible));
        RaisePropertyChanged(nameof(IsViewSeries));
        RaisePropertyChanged(nameof(IsViewStarted));
        RaisePropertyChanged(nameof(IsViewRead));
        RaisePropertyChanged(nameof(IsViewUnread));
        RaisePropertyChanged(nameof(IsStatusAllActive));
        RaisePropertyChanged(nameof(IsStatusStartedActive));
        RaisePropertyChanged(nameof(IsStatusReadActive));
        RaisePropertyChanged(nameof(IsStatusUnreadActive));
        RaisePropertyChanged(nameof(LibraryViewTitle));
        RaisePropertyChanged(nameof(IsInSeriesDetail));
    }

    private void ResetLibraryFilters()
    {
        _searchQuery = "";
        _normalizedSearchQuery = "";
        _showUnreadOnly = false;
        _formatFilter = null;
        RaisePropertyChanged(nameof(SearchQuery));
        RaisePropertyChanged(nameof(HasSearchQuery));
        RaisePropertyChanged(nameof(ShowUnreadOnly));
        NotifyAllFilterProps();
    }

    // --- Shelf management ---

    private void SelectShelf(Guid shelfId)
    {
        _isSeriesView = false;
        _activeShelfId = shelfId;
        _activeShelfName = _shelves.FirstOrDefault(s => s.Id == shelfId)?.Name ?? "";
        _activeFolderPath = null;
        _statusFilter = LibraryStatusFilter.All;
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

    private void StartCreatingShelf()
    {
        _isCreatingShelf = true;
        RaisePropertyChanged(nameof(IsCreatingShelf));
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

    private void SelectFolder(string folderPath)
    {
        _isSeriesView = false;
        _activeShelfId = null;
        _activeShelfName = "";
        _activeFolderPath = folderPath;
        _statusFilter = LibraryStatusFilter.All;
        _isInSeriesDetail = false;
        NotifyNavigationProps();
        RefreshDisplayedItems();
    }

    private void RebuildFolderVMs(IReadOnlyList<string> folderPaths)
    {
        LibraryFolders.Clear();
        foreach (var folderPath in folderPaths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var vm = new LibraryFolderViewModel(folderPath, SelectFolder);
            vm.IsActive = _activeFolderPath is not null && folderPath.Equals(_activeFolderPath, StringComparison.OrdinalIgnoreCase);
            LibraryFolders.Add(vm);
        }

        RaisePropertyChanged(nameof(HasLibraryFolders));
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
        RebuildSeriesVariants();
        RaisePropertyChanged(nameof(IsInSeriesDetail));
        RaisePropertyChanged(nameof(CurrentSeriesName));
        RaisePropertyChanged(nameof(SeriesBooks));
        RaisePropertyChanged(nameof(HasSeriesVariants));
    }

    private void BackToLibrary()
    {
        _isInSeriesDetail = false;
        RaisePropertyChanged(nameof(IsInSeriesDetail));
    }

    private void OpenBook(ComicBookListItemViewModel? book)
    {
        OpenBook(book, openFullscreen: false, startPageIndexOverride: null);
    }

    private void OpenBookFullscreen(ComicBookListItemViewModel? book)
    {
        OpenBook(book, openFullscreen: true, startPageIndexOverride: null);
    }

    private void OpenBook(ComicBookListItemViewModel? book, bool openFullscreen, int? startPageIndexOverride)
    {
        if (book is null) return;

        if (_isSettingsPanelVisible)
        {
            _isSettingsPanelVisible = false;
            RaisePropertyChanged(nameof(IsSettingsPanelVisible));
        }

        if (_isReaderSettingsPanelVisible)
        {
            _isReaderSettingsPanelVisible = false;
            RaisePropertyChanged(nameof(IsReaderSettingsPanelVisible));
        }

        if (_openSingleItem is not null)
            _openSingleItem.IsCurrentlyOpen = false;

        _openSingleItem = DisplayedItems
            .OfType<SingleLibraryItemViewModel>()
            .FirstOrDefault(i => i.Item.ComicBook.Id == book.ComicBook.Id);

        if (_openSingleItem is not null)
            _openSingleItem.IsCurrentlyOpen = true;

        ResetReaderViewToFitPage();
        HideSeriesBookPrompts();
        _nextOpenPageIndexOverride = startPageIndexOverride is null
            ? null
            : Math.Clamp(startPageIndexOverride.Value, 0, Math.Max(0, book.ComicBook.PageCount - 1));
        var wasSelected = SelectedBook?.ComicBook.Id == book.ComicBook.Id;
        SelectedBook = book;
        if (wasSelected)
        {
            CurrentPageIndex = _nextOpenPageIndexOverride ?? GetInitialPageIndex(book);
            _nextOpenPageIndexOverride = null;

            if (_readingDirection == ReadingDirection.TopToBottom)
                _ = LoadVerticalPagesAsync(book.ComicBook);
            else
                _ = LoadCurrentPageAsync();
        }

        if (openFullscreen || _openComicsInFullscreen)
        {
            SetReaderFullscreen(true);
        }

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
        _statusFilter = _showUnreadOnly ? LibraryStatusFilter.Unread : LibraryStatusFilter.All;
        _activeShelfId = null;
        _activeShelfName = "";
        _activeFolderPath = null;
        _isSeriesView = false;
        _isInSeriesDetail = false;
        RaisePropertyChanged(nameof(ShowUnreadOnly));
        NotifyNavigationProps();
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
        if (IsHomeView)
        {
            ReplaceDisplayedItems(BuildHomeItems());
            RaisePropertyChanged(nameof(IsLibraryEmpty));
            RaisePropertyChanged(nameof(LibraryViewTitle));
            return;
        }

        IEnumerable<ComicBookListItemViewModel> filtered = _allBooks;

        if (_normalizedSearchQuery.Length > 0)
        {
            filtered = filtered.Where(b => b.MatchesSearch(_normalizedSearchQuery));
        }

        if (_showUnreadOnly)
            filtered = filtered.Where(b => !b.HasProgress);

        filtered = _statusFilter switch
        {
            LibraryStatusFilter.Started => filtered.Where(b => b.IsStarted),
            LibraryStatusFilter.Read => filtered.Where(b => b.IsRead),
            LibraryStatusFilter.Unread => filtered.Where(b => b.IsUnread),
            _ => filtered
        };

        if (_formatFilter.HasValue)
            filtered = filtered.Where(b => b.ComicBook.Format == _formatFilter.Value);

        if (_activeShelfId.HasValue && _shelfEntriesMap.TryGetValue(_activeShelfId.Value, out var shelfBookIds))
            filtered = filtered.Where(b => shelfBookIds.Contains(b.ComicBook.Id));

        if (_activeFolderPath is not null)
            filtered = filtered.Where(b => IsInsideFolder(b.FilePath, _activeFolderPath));

        var filteredList = filtered.ToList();
        var grouped = _isSeriesView
            ? BuildSeriesItems(filteredList, includeSingleIssueSeries: true)
            : BuildLibraryItems(filteredList);

        ReplaceDisplayedItems(grouped);

        RaisePropertyChanged(nameof(IsLibraryEmpty));
    }

    private void ReplaceDisplayedItems(IEnumerable<LibraryItemViewModel> items)
    {
        DisplayedItems.Clear();
        foreach (var item in items)
            DisplayedItems.Add(item);

        RebuildDisplayedRows();

        if (_selectedBook is not null)
        {
            _openSingleItem = DisplayedItems
                .OfType<SingleLibraryItemViewModel>()
                .FirstOrDefault(i => i.Item.ComicBook.Id == _selectedBook.ComicBook.Id);
            if (_openSingleItem is not null)
                _openSingleItem.IsCurrentlyOpen = true;
        }
    }

    private void RebuildDisplayedRows()
    {
        DisplayedRows.Clear();

        for (var index = 0; index < DisplayedItems.Count; index += _libraryItemsPerRow)
        {
            var rowItems = DisplayedItems.Skip(index).Take(_libraryItemsPerRow).ToArray();
            DisplayedRows.Add(new LibraryRowViewModel(
                rowItems,
                rowItems.Length));
        }
    }

    private void RebuildSeriesVariants()
    {
        SeriesVariants.Clear();

        var variants = _seriesBooks
            .GroupBy(GetSeriesVariantKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var books = SortSeriesBooks(group).ToArray();
                var first = books[0];
                var rows = BuildLibraryRows(books.Select(CreateSingleLibraryItem).Cast<LibraryItemViewModel>().ToArray());
                return new SeriesVariantViewModel(
                    GetSeriesVariantTitle(first, books),
                    GetSeriesVariantSubtitle(first, books),
                    books,
                    rows);
            })
            .OrderByDescending(variant => variant.Books.Count)
            .ThenBy(variant => variant.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var variant in variants)
        {
            SeriesVariants.Add(variant);
        }

    }

    private IReadOnlyList<LibraryRowViewModel> BuildLibraryRows(IReadOnlyList<LibraryItemViewModel> items)
    {
        var rows = new List<LibraryRowViewModel>();
        for (var index = 0; index < items.Count; index += _libraryItemsPerRow)
        {
            var rowItems = items.Skip(index).Take(_libraryItemsPerRow).ToArray();
            rows.Add(new LibraryRowViewModel(
                rowItems,
                rowItems.Length));
        }

        return rows;
    }

    private static string GetSeriesVariantKey(ComicBookListItemViewModel book)
    {
        var path = book.FilePath;
        if (ComicArchiveLocator.TrySplitNestedArchivePath(path, out var outerArchivePath, out _))
        {
            return $"archive:{outerArchivePath}";
        }

        var physicalPath = ComicArchiveLocator.GetPhysicalArchivePath(path);
        var fileName = Path.GetFileName(physicalPath);
        var lowerName = fileName.ToLowerInvariant();
        if (lowerName.Contains("manga-chan"))
        {
            return $"source:manga-chan:{Directory.GetParent(physicalPath)?.FullName}";
        }

        return $"container:{physicalPath}";
    }

    private static string GetSeriesVariantTitle(
        ComicBookListItemViewModel first,
        IReadOnlyList<ComicBookListItemViewModel> books)
    {
        var physicalPath = ComicArchiveLocator.GetPhysicalArchivePath(first.FilePath);
        var fileName = Path.GetFileNameWithoutExtension(physicalPath);
        if (first.FilePath.Contains("::", StringComparison.Ordinal))
        {
            return ComicTitleParser.NormalizeDisplayName(fileName);
        }

        if (Path.GetFileName(physicalPath).Contains("manga-chan", StringComparison.OrdinalIgnoreCase))
        {
            return "manga-chan.me";
        }

        if (books.Count == 1)
        {
            return first.Title;
        }

        return ComicTitleParser.NormalizeDisplayName(Directory.GetParent(physicalPath)?.Name ?? fileName);
    }

    private static string GetSeriesVariantSubtitle(
        ComicBookListItemViewModel first,
        IReadOnlyList<ComicBookListItemViewModel> books)
    {
        var issueNumbers = books
            .Select(book => book.IssueNumber)
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .Order()
            .ToArray();
        var range = issueNumbers.Length == 0
            ? $"{books.Count} item{(books.Count == 1 ? "" : "s")}"
            : issueNumbers.Length == 1
                ? $"#{issueNumbers[0]}"
                : $"#{issueNumbers[0]}-{issueNumbers[^1]}";
        var formats = string.Join(", ", books
            .Select(book => book.Format)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase));
        var source = Path.GetFileName(ComicArchiveLocator.GetPhysicalArchivePath(first.FilePath));

        return $"{range} · {formats} · {source}";
    }

    private IEnumerable<LibraryItemViewModel> BuildLibraryItems(List<ComicBookListItemViewModel> books)
    {
        const string noSeriesPrefix = "\x0000";
        var forceStructuredGroups = _activeFolderPath is not null || _isSeriesView;
        var forceFlatBooks = _statusFilter is LibraryStatusFilter.Started or LibraryStatusFilter.Read or LibraryStatusFilter.Unread;
        var groups = books
            .GroupBy(b => GetLibraryGroupKey(b, forceStructuredGroups, noSeriesPrefix))
            .GroupBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Key = group.First().Key,
                Books = group.SelectMany(items => items).ToList()
            })
            .ToList();

        var result = new List<LibraryItemViewModel>();
        foreach (var group in groups)
        {
            var list = SortSeriesBooks(group.Books).ToList();
            var hasSeriesName = !group.Key.StartsWith(noSeriesPrefix);
            var shouldGroupAsSeries = hasSeriesName && !forceFlatBooks &&
                (list.Count >= 2 || list.Any(book => book.IssueNumber.HasValue));
            if (_isSeriesView && hasSeriesName && !forceFlatBooks)
            {
                shouldGroupAsSeries = true;
            }

            if (forceStructuredGroups && hasSeriesName && !forceFlatBooks)
            {
                shouldGroupAsSeries = true;
            }

            if (shouldGroupAsSeries)
            {
                result.Add(new SeriesLibraryItemViewModel(group.Key, list, OpenSeries));
            }
            else
            {
                // In series-only view, skip standalone books
                if (_isSeriesView) continue;

                foreach (var b in list)
                    result.Add(CreateSingleLibraryItem(b));
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

    private IEnumerable<LibraryItemViewModel> BuildHomeItems()
    {
        var continueBooks = _allBooks
            .Where(book => book.IsStarted)
            .OrderByDescending(book => GetProgressUpdatedAt(book))
            .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .Select(CreateSingleLibraryItem)
            .ToArray();

        if (continueBooks.Length > 0)
        {
            return continueBooks;
        }

        var seriesItems = BuildSeriesItems(_allBooks, includeSingleIssueSeries: true)
            .OrderBy(item => GetSortTitle(item))
            .ToArray();

        return seriesItems;
    }

    private IEnumerable<SeriesLibraryItemViewModel> BuildSeriesItems(
        IEnumerable<ComicBookListItemViewModel> books,
        bool includeSingleIssueSeries)
    {
        return books
            .Select(book => new
            {
                Book = book,
                SeriesName = ResolveSeriesGroupName(book)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.SeriesName))
            .GroupBy(item => item.SeriesName!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                SeriesName = group.First().SeriesName!,
                Books = SortSeriesBooks(group.Select(item => item.Book)).ToArray()
            })
            .Where(group => group.Books.Length >= 2
                || includeSingleIssueSeries
                || group.Books.Any(book => book.IssueNumber.HasValue))
            .Select(group => new SeriesLibraryItemViewModel(group.SeriesName, group.Books, OpenSeries));
    }

    private SingleLibraryItemViewModel CreateSingleLibraryItem(ComicBookListItemViewModel book)
    {
        return new SingleLibraryItemViewModel(
            book,
            item => OpenBook(item),
            item => OpenBookFullscreen(item),
            MarkBookAsRead,
            MarkBookAsUnread,
            StartCreatingShelf,
            () => BuildShelfMenuItems(book.ComicBook.Id));
    }

    private static string? ResolveSeriesGroupName(ComicBookListItemViewModel book)
    {
        if (!string.IsNullOrWhiteSpace(book.SeriesName))
        {
            return book.SeriesName;
        }

        var parsed = ComicTitleParser.Parse(book.Title);
        if (!string.IsNullOrWhiteSpace(parsed.SeriesName) &&
            !parsed.SeriesName.Equals(parsed.DisplayTitle, StringComparison.OrdinalIgnoreCase))
        {
            return parsed.SeriesName;
        }

        var physicalPath = ComicArchiveLocator.GetPhysicalArchivePath(book.FilePath);
        var parent = Directory.GetParent(physicalPath);
        if (parent is null || IsGenericSeriesFolder(parent.Name))
        {
            return null;
        }

        return ComicTitleParser.NormalizeDisplayName(parent.Name);
    }

    private static bool IsGenericSeriesFolder(string folderName)
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

    private string GetLibraryGroupKey(
        ComicBookListItemViewModel book,
        bool forceStructuredGroups,
        string noSeriesPrefix)
    {
        if (!string.IsNullOrWhiteSpace(book.SeriesName))
        {
            return book.SeriesName!;
        }

        if (forceStructuredGroups && _activeFolderPath is not null)
        {
            var physicalPath = ComicArchiveLocator.GetPhysicalArchivePath(book.FilePath);
            var parent = Directory.GetParent(physicalPath);
            if (parent is not null &&
                !parent.FullName.Equals(_activeFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                var parentName = ComicTitleParser.NormalizeDisplayName(parent.Name);
                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    return parentName;
                }
            }
        }

        return noSeriesPrefix + book.Title;
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

    private static IEnumerable<ComicBookListItemViewModel> SortSeriesBooks(IEnumerable<ComicBookListItemViewModel> books)
    {
        return books
            .OrderBy(book => book.IssueNumber is null)
            .ThenBy(book => book.IssueNumber ?? int.MaxValue)
            .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(book => book.FilePath, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsInsideFolder(string filePath, string folderPath)
    {
        var physicalPath = ComicArchiveLocator.GetPhysicalArchivePath(filePath);
        var relativePath = Path.GetRelativePath(folderPath, physicalPath);
        return relativePath.Length > 0 &&
            !relativePath.StartsWith("..", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relativePath);
    }

    private static string GetFolderDisplayName(string folderPath)
    {
        var name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? folderPath : name;
    }

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

    private DateTimeOffset GetProgressUpdatedAt(ComicBookListItemViewModel book)
    {
        return _progressMap.TryGetValue(book.ComicBook.Id, out var progress)
            ? progress.UpdatedAt
            : DateTimeOffset.MinValue;
    }

    // --- Settings ---

    private void ToggleSettingsPanel()
    {
        _isSettingsPanelVisible = !_isSettingsPanelVisible;
        if (_isSettingsPanelVisible && _isReaderSettingsPanelVisible)
        {
            _isReaderSettingsPanelVisible = false;
            RaisePropertyChanged(nameof(IsReaderSettingsPanelVisible));
        }

        RaisePropertyChanged(nameof(IsSettingsPanelVisible));
    }

    private void ToggleReaderSettingsPanel()
    {
        _isReaderSettingsPanelVisible = !_isReaderSettingsPanelVisible;
        if (_isReaderSettingsPanelVisible && _isSettingsPanelVisible)
        {
            _isSettingsPanelVisible = false;
            RaisePropertyChanged(nameof(IsSettingsPanelVisible));
        }

        RaisePropertyChanged(nameof(IsReaderSettingsPanelVisible));
    }

    private void ToggleFitMode()
    {
        _fitMode = _fitMode == FitMode.FitPage ? FitMode.FitWidth : FitMode.FitPage;
        RaisePropertyChanged(nameof(IsFitPageMode));
        RaisePropertyChanged(nameof(IsFitWidthMode));
        RaisePropertyChanged(nameof(FitModeLabel));
    }

    private void ResetReaderViewToFitPage()
    {
        if (_fitMode != FitMode.FitPage)
        {
            _fitMode = FitMode.FitPage;
            RaisePropertyChanged(nameof(IsFitPageMode));
            RaisePropertyChanged(nameof(IsFitWidthMode));
            RaisePropertyChanged(nameof(FitModeLabel));
        }

        if (Math.Abs(_readerZoom - 1.0) > 0.001)
        {
            _readerZoom = 1.0;
            RaisePropertyChanged(nameof(ReaderZoom));
            RaisePropertyChanged(nameof(ZoomLabel));
        }
    }

    private void ToggleReaderFullscreen()
    {
        SetReaderFullscreen(!_isReaderFullscreen);
    }

    private void SetReaderFullscreen(bool isFullscreen)
    {
        if (_isReaderFullscreen == isFullscreen)
        {
            return;
        }

        _isReaderFullscreen = isFullscreen;
        RaisePropertyChanged(nameof(IsReaderFullscreen));
        RaisePropertyChanged(nameof(ReaderFullscreenLabel));
        NotifyGlobalSettingsProps();
    }

    private void ToggleOpenComicsAtLastPosition()
    {
        _openComicsAtLastPosition = !_openComicsAtLastPosition;
        NotifyGlobalSettingsProps();
        _ = SavePreferencesAsync();
    }

    private void ToggleOpenComicsInFullscreen()
    {
        _openComicsInFullscreen = !_openComicsInFullscreen;
        NotifyGlobalSettingsProps();

        if (_openComicsInFullscreen && SelectedBook is not null)
        {
            SetReaderFullscreen(true);
        }

        _ = SavePreferencesAsync();
    }

    private void ToggleReaderPreviewPane()
    {
        _isReaderPreviewPaneEnabled = !_isReaderPreviewPaneEnabled;
        NotifyGlobalSettingsProps();

        _ = SavePreferencesAsync();
    }

    private void ToggleOpenPreviousChapterAtLastPage()
    {
        _openPreviousChapterAtLastPage = !_openPreviousChapterAtLastPage;
        NotifyReaderSettingsProps();
        _ = SavePreferencesAsync();
    }

    private void ToggleTwoPageMode()
    {
        _isTwoPageMode = !_isTwoPageMode;
        NotifyReaderSettingsProps();
        RefreshAllNavCanExecute();

        if (SelectedBook is not null && !IsVerticalMode)
        {
            _ = LoadCurrentPageAsync();
        }

        _ = SavePreferencesAsync();
    }

    private void ToggleEdgePageTurns()
    {
        _isEdgePageTurnEnabled = !_isEdgePageTurnEnabled;
        RaisePropertyChanged(nameof(IsEdgePageTurnEnabled));
        _ = SavePreferencesAsync();
    }

    private void ToggleDragPageTurns()
    {
        _isDragPageTurnEnabled = !_isDragPageTurnEnabled;
        RaisePropertyChanged(nameof(IsDragPageTurnEnabled));
        _ = SavePreferencesAsync();
    }

    private void TogglePageTurnInversion()
    {
        _isPageTurnInverted = !_isPageTurnInverted;
        RaisePropertyChanged(nameof(IsPageTurnInverted));
        _ = SavePreferencesAsync();
    }

    private void ResetReaderDefaults()
    {
        var defaults = UserPreferences.Default;
        _readingDirection = defaults.ReadingDirection;
        _readerWheelAction = defaults.ReaderWheelAction;
        _isEdgePageTurnEnabled = defaults.IsEdgePageTurnEnabled;
        _isDragPageTurnEnabled = defaults.IsDragPageTurnEnabled;
        _isPageTurnInverted = defaults.IsPageTurnInverted;
        _readerColorTone = defaults.ReaderColorTone;
        _readerPageAnimation = defaults.ReaderPageAnimation;
        _isTwoPageMode = defaults.IsTwoPageMode;
        _openPreviousChapterAtLastPage = defaults.OpenPreviousChapterAtLastPage;
        NotifyAllDirectionProps();
        NotifyReaderSettingsProps();

        if (SelectedBook is not null && !IsVerticalMode)
        {
            _ = LoadCurrentPageAsync();
        }

        _ = SavePreferencesAsync();
    }

    private void SetReaderWheelAction(string? key)
    {
        _readerWheelAction = key switch
        {
            "page" => ReaderWheelAction.PageTurn,
            "zoom" => ReaderWheelAction.Zoom,
            _ => ReaderWheelAction.Scroll
        };
        NotifyReaderSettingsProps();
        _ = SavePreferencesAsync();
    }

    private void SetReaderColorTone(string? key)
    {
        _readerColorTone = key switch
        {
            "warm" => ReaderColorTone.Warm,
            "paper" => ReaderColorTone.Paper,
            "cool" => ReaderColorTone.Cool,
            _ => ReaderColorTone.Original
        };
        NotifyReaderSettingsProps();
        _ = SavePreferencesAsync();
    }

    private void SetReaderAnimation(string? key)
    {
        _readerPageAnimation = key switch
        {
            "fade" => ReaderPageAnimation.Fade,
            "slide" => ReaderPageAnimation.Slide,
            _ => ReaderPageAnimation.None
        };
        NotifyReaderSettingsProps();
        _ = SavePreferencesAsync();
    }

    public void AdjustReaderZoom(double wheelDelta)
    {
        var factor = wheelDelta > 0 ? 1.1 : 1 / 1.1;
        var nextZoom = Math.Clamp(_readerZoom * factor, 0.25, 4.0);

        if (Math.Abs(nextZoom - _readerZoom) < 0.001)
        {
            return;
        }

        _readerZoom = nextZoom;
        RaisePropertyChanged(nameof(ReaderZoom));
        RaisePropertyChanged(nameof(ZoomLabel));
    }

    public void TurnPageFromEdge(bool rightEdge)
    {
        TurnPage(rightEdge);
    }

    public void TurnPageFromSwipe(double horizontalDelta)
    {
        TurnPage(horizontalDelta < 0);
    }

    public void TurnPageFromWheel(double wheelDelta)
    {
        TurnPage(wheelDelta < 0);
    }

    private void TurnPage(bool forward)
    {
        if (_isPageTurnInverted)
        {
            forward = !forward;
        }

        var command = forward ? PageForwardCommand : PageBackwardCommand;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void ToggleTheme()
    {
        _isDarkTheme = !_isDarkTheme;
        RaisePropertyChanged(nameof(IsDarkTheme));
        RaisePropertyChanged(nameof(IsLightTheme));
        RaisePropertyChanged(nameof(ThemeToggleLabel));
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
            ClearVerticalPages();
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
        await _preferencesStore.SaveAsync(new UserPreferences(
            _isDarkTheme,
            _readingDirection,
            _readerWheelAction,
            _isEdgePageTurnEnabled,
            _isDragPageTurnEnabled,
            _isPageTurnInverted,
            _readerColorTone,
            _readerPageAnimation,
            _isTwoPageMode,
            _openComicsAtLastPosition,
            _openComicsInFullscreen,
            _isReaderPreviewPaneEnabled,
            _openPreviousChapterAtLastPage));
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
        var pageCount = SelectedBook.ComicBook.PageCount;
        if (pageCount <= 0) return;

        if (delta < 0 && CurrentPageIndex <= 0)
        {
            if (HasPreviousSeriesBookPrompt)
            {
                OpenPreviousSeriesBook(startAtLastPage: _openPreviousChapterAtLastPage);
                return;
            }

            ShowPreviousSeriesBookPrompt();
            return;
        }

        if (delta > 0 && CurrentPageIndex + PageStep >= pageCount)
        {
            if (HasNextSeriesBookPrompt)
            {
                OpenNextSeriesBook();
                return;
            }

            ShowNextSeriesBookPrompt();
            return;
        }

        var next = Math.Clamp(
            CurrentPageIndex + delta,
            0,
            Math.Max(0, pageCount - 1));
        if (next == CurrentPageIndex) return;
        HideSeriesBookPrompts();
        CurrentPageIndex = next;
        _ = LoadCurrentPageAsync();
        _ = SaveProgressAsync();
    }

    private void JumpToPage(int pageIndex)
    {
        if (SelectedBook is null) return;
        var clamped = Math.Clamp(pageIndex, 0, Math.Max(0, SelectedBook.ComicBook.PageCount - 1));
        if (clamped == CurrentPageIndex) return;
        HideSeriesBookPrompts();
        CurrentPageIndex = clamped;
        _ = LoadCurrentPageAsync();
        _ = SaveProgressAsync();
    }

    private void ShowPreviousSeriesBookPrompt()
    {
        UpdateSeriesBookNavigation();
        if (_previousSeriesBook is null)
        {
            return;
        }

        _isPreviousSeriesBookPromptVisible = true;
        _previousSeriesBookActionLabel = BuildPreviousSeriesBookActionLabel(_previousSeriesBook);
        NotifyPreviousSeriesBookPromptProps();
    }

    private void ShowNextSeriesBookPrompt()
    {
        UpdateSeriesBookNavigation();
        if (_nextSeriesBook is null)
        {
            return;
        }

        _isNextSeriesBookPromptVisible = true;
        _nextSeriesBookActionLabel = BuildNextSeriesBookActionLabel(_nextSeriesBook);
        NotifyNextSeriesBookPromptProps();
    }

    private void HideSeriesBookPrompts()
    {
        HidePreviousSeriesBookPrompt();
        HideNextSeriesBookPrompt();
    }

    private void HidePreviousSeriesBookPrompt()
    {
        if (!_isPreviousSeriesBookPromptVisible && string.IsNullOrEmpty(_previousSeriesBookActionLabel))
        {
            return;
        }

        _isPreviousSeriesBookPromptVisible = false;
        _previousSeriesBookActionLabel = "";
        NotifyPreviousSeriesBookPromptProps();
    }

    private void HideNextSeriesBookPrompt()
    {
        if (!_isNextSeriesBookPromptVisible && string.IsNullOrEmpty(_nextSeriesBookActionLabel))
        {
            return;
        }

        _isNextSeriesBookPromptVisible = false;
        _nextSeriesBookActionLabel = "";
        NotifyNextSeriesBookPromptProps();
    }

    private void OpenPreviousSeriesBook()
    {
        OpenPreviousSeriesBook(startAtLastPage: false);
    }

    private void OpenPreviousSeriesBook(bool startAtLastPage)
    {
        var previousBook = _previousSeriesBook;
        if (previousBook is null)
        {
            return;
        }

        HideSeriesBookPrompts();
        var startPageIndex = startAtLastPage
            ? Math.Max(0, previousBook.ComicBook.PageCount - PageStep)
            : 0;
        OpenBook(previousBook, openFullscreen: false, startPageIndexOverride: startPageIndex);
    }

    private void OpenNextSeriesBook()
    {
        var nextBook = _nextSeriesBook;
        if (nextBook is null)
        {
            return;
        }

        HideSeriesBookPrompts();
        OpenBook(nextBook, openFullscreen: false, startPageIndexOverride: 0);
    }

    private void UpdateSeriesBookNavigation()
    {
        _previousSeriesBook = FindAdjacentSeriesBook(-1);
        _nextSeriesBook = FindAdjacentSeriesBook(1);
        if (_previousSeriesBook is null)
        {
            HidePreviousSeriesBookPrompt();
        }

        if (_nextSeriesBook is null)
        {
            HideNextSeriesBookPrompt();
        }

        RefreshAllNavCanExecute();
    }

    private ComicBookListItemViewModel? FindAdjacentSeriesBook(int direction)
    {
        var current = SelectedBook;
        if (current is null || string.IsNullOrWhiteSpace(current.SeriesName))
        {
            return null;
        }

        var currentVariantKey = GetSeriesVariantKey(current);
        var orderedSeries = SortSeriesBooks(_allBooks
            .Where(book => book.SeriesName is not null
                && book.SeriesName.Equals(current.SeriesName, StringComparison.OrdinalIgnoreCase)
                && GetSeriesVariantKey(book).Equals(currentVariantKey, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (current.IssueNumber.HasValue)
        {
            var issueBooks = orderedSeries
                .Where(book => book.ComicBook.Id != current.ComicBook.Id && book.IssueNumber.HasValue)
                .ToArray();

            return direction > 0
                ? issueBooks.FirstOrDefault(book => book.IssueNumber.GetValueOrDefault() > current.IssueNumber.Value)
                : issueBooks.LastOrDefault(book => book.IssueNumber.GetValueOrDefault() < current.IssueNumber.Value);
        }

        var currentIndex = Array.FindIndex(orderedSeries, book => book.ComicBook.Id == current.ComicBook.Id);
        var adjacentIndex = currentIndex + direction;
        return currentIndex >= 0 && adjacentIndex >= 0 && adjacentIndex < orderedSeries.Length
            ? orderedSeries[adjacentIndex]
            : null;
    }

    private string BuildPreviousSeriesBookActionLabel(ComicBookListItemViewModel previousBook)
    {
        var currentIssue = SelectedBook?.IssueNumber;
        if (currentIssue.HasValue && previousBook.IssueNumber == currentIssue.Value - 1)
        {
            return "\u0427\u0438\u0442\u0430\u0442\u044c \u043f\u0440\u0435\u0434\u044b\u0434\u0443\u0449\u0443\u044e";
        }

        return previousBook.IssueNumber.HasValue
            ? $"\u041f\u0435\u0440\u0435\u0439\u0442\u0438 \u043a {previousBook.IssueNumber.Value} \u0447\u0430\u0441\u0442\u0438"
            : "\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u043f\u0440\u0435\u0434\u044b\u0434\u0443\u0449\u0443\u044e \u0447\u0430\u0441\u0442\u044c";
    }

    private string BuildNextSeriesBookActionLabel(ComicBookListItemViewModel nextBook)
    {
        var currentIssue = SelectedBook?.IssueNumber;
        if (currentIssue.HasValue && nextBook.IssueNumber == currentIssue.Value + 1)
        {
            return "\u0427\u0438\u0442\u0430\u0442\u044c \u0441\u043b\u0435\u0434\u0443\u044e\u0449\u0443\u044e";
        }

        return nextBook.IssueNumber.HasValue
            ? $"\u041f\u0435\u0440\u0435\u0439\u0442\u0438 \u043a {nextBook.IssueNumber.Value} \u0447\u0430\u0441\u0442\u0438"
            : "\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u0441\u043b\u0435\u0434\u0443\u044e\u0449\u0443\u044e \u0447\u0430\u0441\u0442\u044c";
    }

    private void NotifyNextSeriesBookPromptProps()
    {
        RaisePropertyChanged(nameof(HasNextSeriesBookPrompt));
        RaisePropertyChanged(nameof(NextSeriesBookActionLabel));
        OpenNextSeriesBookCommand.RaiseCanExecuteChanged();
    }

    private void NotifyPreviousSeriesBookPromptProps()
    {
        RaisePropertyChanged(nameof(HasPreviousSeriesBookPrompt));
        RaisePropertyChanged(nameof(PreviousSeriesBookActionLabel));
        OpenPreviousSeriesBookCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadCurrentPageAsync()
    {
        _currentPageCts?.Cancel();
        _currentPageCts = new CancellationTokenSource();
        var cts = _currentPageCts;
        var loadVersion = ++_currentPageLoadVersion;

        if (SelectedBook is null)
        {
            CurrentPageImage = null;
            NextPageImage = null;
            IsPageLoading = false;
            ReaderStatus = "Select a book to start reading.";
            return;
        }

        var book = SelectedBook.ComicBook;
        if (!IsUnsupportedReaderFormat(book.Format) && book.PageCount <= 0)
        {
            IsPageLoading = true;
            ReaderStatus = "Reading archive...";
            await TryResolveSelectedBookPageCountAsync(book, cts.Token);
            if (cts.IsCancellationRequested || loadVersion != _currentPageLoadVersion || SelectedBook is null)
            {
                return;
            }

            book = SelectedBook.ComicBook;
            CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, Math.Max(0, book.PageCount - 1));
        }

        SetActivePageWindow(book);
        if (IsUnsupportedReaderFormat(book.Format) || book.PageCount <= 0)
        {
            CurrentPageImage = null;
            NextPageImage = null;
            IsPageLoading = false;
            ReaderStatus = GetUnreadableBookMessage(book);
            RaisePropertyChanged(nameof(CurrentPageLabel));
            RefreshAllNavCanExecute();
            return;
        }

        IsPageLoading = true;
        ReaderStatus = book.Title;
        NextPageImage = null;

        try
        {
            var page = await _pagePreviewLoader.LoadPageAsync(book, CurrentPageIndex, cts.Token);
            if (cts.IsCancellationRequested || loadVersion != _currentPageLoadVersion)
            {
                return;
            }

            CurrentPageImage = page;

            if (CurrentPageImage is null)
                ReaderStatus = "Page could not be loaded.";
            else
            {
                if (_isTwoPageMode)
                {
                    _ = LoadNextPageImageAsync(book, CurrentPageIndex, loadVersion, cts.Token);
                }

                StartBookPreload(book, CurrentPageIndex);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!cts.IsCancellationRequested && loadVersion == _currentPageLoadVersion)
            {
                IsPageLoading = false;
            }
        }

        RaisePropertyChanged(nameof(CurrentPageLabel));
        RefreshAllNavCanExecute();
    }

    private async Task TryResolveSelectedBookPageCountAsync(ComicBook book, CancellationToken cancellationToken)
    {
        if (_pagePreviewLoader is not IComicPageCountProvider pageCountProvider)
        {
            return;
        }

        int pageCount;
        try
        {
            pageCount = await pageCountProvider.GetPageCountAsync(book, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return;
        }

        if (pageCount <= 0 ||
            SelectedBook is null ||
            SelectedBook.ComicBook.Id != book.Id)
        {
            return;
        }

        SelectedBook.SetPageCount(pageCount);
        await _comicRepository.UpsertAsync(SelectedBook.ComicBook, cancellationToken);
        RaisePropertyChanged(nameof(CurrentPageLabel));
        UpdateSeriesBookNavigation();
        RefreshDisplayedItems();
    }

    private static bool IsUnsupportedReaderFormat(ComicFormat format)
    {
        return format is ComicFormat.SevenZip or ComicFormat.Epub;
    }

    private static string GetUnreadableBookMessage(ComicBook book)
    {
        if (IsUnsupportedReaderFormat(book.Format))
        {
            return $"{book.Format} detected, but this format is not supported yet.";
        }

        return book.Format == ComicFormat.Zip
            ? "This ZIP does not contain readable image pages."
            : "No readable pages found.";
    }

    private async Task LoadNextPageImageAsync(
        ComicBook book,
        int currentPageIndex,
        int loadVersion,
        CancellationToken cancellationToken)
    {
        var nextPageIndex = currentPageIndex + 1;
        if (nextPageIndex >= book.PageCount)
        {
            return;
        }

        try
        {
            var page = await _pagePreviewLoader.LoadPageAsync(book, nextPageIndex, cancellationToken);
            if (cancellationToken.IsCancellationRequested ||
                loadVersion != _currentPageLoadVersion ||
                CurrentPageIndex != currentPageIndex)
            {
                return;
            }

            NextPageImage = page;
        }
        catch (OperationCanceledException) { }
    }

    private void StartBookPreload(ComicBook book, int pageIndex)
    {
        if (_bookPreloadCts is not null &&
            _preloadingBookId == book.Id &&
            pageIndex >= _preloadAnchorPageIndex &&
            pageIndex <= _preloadAnchorPageIndex + 12)
        {
            return;
        }

        CancelBookPreload();
        if (_pagePreviewLoader is not IPageCacheMaintenance maintenance)
        {
            return;
        }

        _bookPreloadCts = new CancellationTokenSource();
        _preloadingBookId = book.Id;
        _preloadAnchorPageIndex = pageIndex;
        var cts = _bookPreloadCts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await maintenance.PreloadPagesAsync(book, pageIndex, token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (ReferenceEquals(_bookPreloadCts, cts))
                {
                    _bookPreloadCts = null;
                    _preloadingBookId = null;
                    _preloadAnchorPageIndex = -1;
                }

                cts.Dispose();
            }
        });
    }

    private void SetActivePageWindow(ComicBook book)
    {
        if (_pagePreviewLoader is IPageCacheMaintenance maintenance)
        {
            maintenance.SetActivePageWindow(book, CurrentPageIndex, _isTwoPageMode && !IsVerticalMode ? 2 : 1);
        }
    }

    private void CancelBookPreload()
    {
        _bookPreloadCts?.Cancel();
        _bookPreloadCts = null;
        _preloadingBookId = null;
        _preloadAnchorPageIndex = -1;
    }

    private async Task LoadVerticalPagesAsync(ComicBook comicBook)
    {
        _verticalPagesCts?.Cancel();
        _verticalPagesCts = new CancellationTokenSource();
        var cts = _verticalPagesCts;

        CurrentPageImage = null;
        NextPageImage = null;
        ClearVerticalPages();
        IsPageLoading = true;
        ReaderStatus = comicBook.Title;
        if (_pagePreviewLoader is IPageCacheMaintenance maintenance)
        {
            maintenance.SetActivePageWindow(comicBook, CurrentPageIndex, Math.Min(20, comicBook.PageCount));
        }

        try
        {
            await Task.Yield();

            for (var index = 0; index < comicBook.PageCount; index++)
            {
                cts.Token.ThrowIfCancellationRequested();
                VerticalPages.Add(new ReaderPageViewModel(comicBook, index, _pagePreviewLoader));
            }

            StartBookPreload(comicBook, CurrentPageIndex);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsPageLoading = false;
        }
    }

    private void ClearVerticalPages()
    {
        foreach (var page in VerticalPages)
        {
            page.Detach();
        }

        VerticalPages.Clear();
    }

    private async Task LoadCoversAsync()
    {
        _coverLoadCts?.Cancel();
        _coverLoadCts = new CancellationTokenSource();
        var cts = _coverLoadCts;
        var concurrency = Math.Clamp(Environment.ProcessorCount / 3, 2, 4);
        using var semaphore = new SemaphoreSlim(concurrency);
        var visibleBookIds = DisplayedItems
            .SelectMany(GetBooksForLibraryItem)
            .Select(book => book.ComicBook.Id)
            .ToHashSet();
        var books = _allBooks
            .Where(book => !book.HasCoverImage)
            .OrderBy(book => visibleBookIds.Contains(book.ComicBook.Id) ? 0 : 1)
            .ThenBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tasks = books.Select(async book =>
        {
            await semaphore.WaitAsync(cts.Token);
            try
            {
                var cover = await _coverImageCache.LoadCoverAsync(book.ComicBook, cts.Token);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                book.SetCoverImage(cover);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_coverLoadCts, cts))
            {
                _coverLoadCts = null;
            }

            cts.Dispose();
        }
    }

    private static IEnumerable<ComicBookListItemViewModel> GetBooksForLibraryItem(LibraryItemViewModel item)
    {
        return item switch
        {
            SingleLibraryItemViewModel single => [single.Item],
            SeriesLibraryItemViewModel series => series.Books,
            _ => []
        };
    }

    private async Task SaveProgressAsync()
    {
        var selectedBook = SelectedBook;
        if (selectedBook is null) return;

        var progress = new ReadingProgress(selectedBook.ComicBook.Id, CurrentPageIndex, DateTimeOffset.UtcNow);
        await Task.Run(() => _progressRepository.SaveAsync(progress));
        selectedBook.SetProgress(progress);
        _progressMap[progress.ComicBookId] = progress;
    }

    public void Dispose()
    {
        _currentPageCts?.Cancel();
        _verticalPagesCts?.Cancel();
        _coverLoadCts?.Cancel();
        CancelBookPreload();

        CurrentPageImage = null;
        NextPageImage = null;
        ClearVerticalPages();

        foreach (var book in _allBooks)
        {
            book.SetCoverImage(null);
        }

        if (_coverImageCache is IDisposable disposableCoverCache)
        {
            disposableCoverCache.Dispose();
        }

        if (_pagePreviewLoader is IDisposable disposableLoader)
        {
            disposableLoader.Dispose();
        }
        else if (_pagePreviewLoader is IPageCacheMaintenance maintenance)
        {
            maintenance.ClearAllPages();
        }
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
        OpenPreviousSeriesBookCommand.RaiseCanExecuteChanged();
        OpenNextSeriesBookCommand.RaiseCanExecuteChanged();
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
        RaisePropertyChanged(nameof(IsCbrFilterActive));
    }

    private void NotifyAllDirectionProps()
    {
        RaisePropertyChanged(nameof(IsLeftToRight));
        RaisePropertyChanged(nameof(IsRightToLeft));
        RaisePropertyChanged(nameof(IsTopToBottom));
        RaisePropertyChanged(nameof(IsVerticalMode));
        RaisePropertyChanged(nameof(IsSinglePageVisible));
        RaisePropertyChanged(nameof(IsTwoPageVisible));
        RaisePropertyChanged(nameof(CurrentPageLabel));
        RefreshAllNavCanExecute();
    }

    private void NotifyReaderSettingsProps()
    {
        RaisePropertyChanged(nameof(IsReaderFullscreen));
        RaisePropertyChanged(nameof(ReaderFullscreenLabel));
        RaisePropertyChanged(nameof(IsTwoPageMode));
        RaisePropertyChanged(nameof(IsSinglePageMode));
        RaisePropertyChanged(nameof(PageLayoutLabel));
        RaisePropertyChanged(nameof(IsEdgePageTurnEnabled));
        RaisePropertyChanged(nameof(IsDragPageTurnEnabled));
        RaisePropertyChanged(nameof(IsPageTurnInverted));
        RaisePropertyChanged(nameof(OpenPreviousChapterAtLastPage));
        RaisePropertyChanged(nameof(ReaderWheelAction));
        RaisePropertyChanged(nameof(IsWheelScrollAction));
        RaisePropertyChanged(nameof(IsWheelPageTurnAction));
        RaisePropertyChanged(nameof(IsWheelZoomAction));
        RaisePropertyChanged(nameof(ReaderColorTone));
        RaisePropertyChanged(nameof(IsReaderToneOriginal));
        RaisePropertyChanged(nameof(IsReaderToneWarm));
        RaisePropertyChanged(nameof(IsReaderTonePaper));
        RaisePropertyChanged(nameof(IsReaderToneCool));
        RaisePropertyChanged(nameof(ReaderPageAnimation));
        RaisePropertyChanged(nameof(IsReaderAnimationNone));
        RaisePropertyChanged(nameof(IsReaderAnimationFade));
        RaisePropertyChanged(nameof(IsReaderAnimationSlide));
        RaisePropertyChanged(nameof(ReaderBackgroundBrush));
        RaisePropertyChanged(nameof(ReaderPageTintBrush));
        RaisePropertyChanged(nameof(IsReaderTintVisible));
        RaisePropertyChanged(nameof(IsSinglePageVisible));
        RaisePropertyChanged(nameof(IsTwoPageVisible));
        RaisePropertyChanged(nameof(CurrentPageLabel));
    }

    private void NotifyGlobalSettingsProps()
    {
        RaisePropertyChanged(nameof(OpenComicsAtLastPosition));
        RaisePropertyChanged(nameof(OpenComicsInFullscreen));
        RaisePropertyChanged(nameof(IsReaderPreviewPaneEnabled));
        RaisePropertyChanged(nameof(ReaderPreviewPaneLabel));
        RaisePropertyChanged(nameof(LibraryPaneWidth));
        RaisePropertyChanged(nameof(ReaderPreviewSplitterWidth));
        RaisePropertyChanged(nameof(ReaderPreviewPaneWidth));
    }
}
