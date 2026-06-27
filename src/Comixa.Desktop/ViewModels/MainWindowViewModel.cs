using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Desktop.Reader;
using Comixa.Desktop.Services;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IFolderPicker _folderPicker;
    private readonly IComicLibraryScanner _comicLibraryScanner;
    private readonly IUserLibrarySettingsStore _settingsStore;
    private readonly IPagePreviewLoader _pagePreviewLoader;
    private string _statusMessage = "Choose a local folder to begin building your comic library.";
    private string _readerStatus = "Select a book to open the reader.";
    private ComicBookListItemViewModel? _selectedBook;
    private int _currentPageIndex;

    public MainWindowViewModel(
        IFolderPicker folderPicker,
        IComicLibraryScanner comicLibraryScanner,
        IUserLibrarySettingsStore settingsStore,
        IPagePreviewLoader pagePreviewLoader)
    {
        _folderPicker = folderPicker;
        _comicLibraryScanner = comicLibraryScanner;
        _settingsStore = settingsStore;
        _pagePreviewLoader = pagePreviewLoader;
        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
        PreviousPageCommand = new RelayCommand(
            () => MovePage(-1),
            () => SelectedBook is not null && CurrentPageIndex > 0);
        NextPageCommand = new RelayCommand(
            () => MovePage(1),
            () => SelectedBook is not null && CurrentPageIndex + 1 < SelectedBook.ComicBook.PageCount);

        _ = LoadSavedFoldersAsync();
    }

    public string Title => "Comixa Desktop";

    public string Phase => "MVP 1 - Local Desktop Reader Core";

    public ObservableCollection<ComicBookListItemViewModel> Books { get; } = [];

    public ObservableCollection<ReaderPageViewModel> ReaderPages { get; } = [];

    public AsyncRelayCommand ScanFolderCommand { get; }

    public RelayCommand PreviousPageCommand { get; }

    public RelayCommand NextPageCommand { get; }

    public ComicBookListItemViewModel? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (_selectedBook == value)
            {
                return;
            }

            _selectedBook = value;
            CurrentPageIndex = 0;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasSelectedBook));
            _ = LoadReaderPagesAsync();
        }
    }

    public bool HasSelectedBook => SelectedBook is not null;

    public bool HasReaderPages => ReaderPages.Count > 0;

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set
        {
            if (_currentPageIndex == value)
            {
                return;
            }

            _currentPageIndex = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(CurrentPageLabel));
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }
    }

    public string CurrentPageLabel
    {
        get
        {
            if (SelectedBook is null)
            {
                return "No book selected";
            }

            return SelectedBook.ComicBook.PageCount == 0
                ? "Pages unknown"
                : $"{SelectedBook.ComicBook.PageCount} pages";
        }
    }

    public string ReaderStatus
    {
        get => _readerStatus;
        private set
        {
            if (_readerStatus == value)
            {
                return;
            }

            _readerStatus = value;
            RaisePropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            RaisePropertyChanged();
        }
    }

    public bool HasBooks => Books.Count > 0;

    public string ReaderPlaceholder => "Open a local comic from the library to preview pages.";

    private async Task ScanFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync();
        if (folder is null)
        {
            return;
        }

        var settings = await _settingsStore.LoadAsync();
        var folders = settings.WatchedFolders
            .Append(folder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await _settingsStore.SaveAsync(new UserLibrarySettings(folders));
        await ScanFoldersAsync(folders);
    }

    private async Task LoadSavedFoldersAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        if (settings.WatchedFolders.Count == 0)
        {
            return;
        }

        await ScanFoldersAsync(settings.WatchedFolders);
    }

    private async Task ScanFoldersAsync(IReadOnlyList<string> folders)
    {
        StatusMessage = "Scanning local library...";
        Books.Clear();
        SelectedBook = null;
        ReaderPages.Clear();
        RaisePropertyChanged(nameof(HasReaderPages));

        var scannedFiles = new List<ScannedComicFile>();
        foreach (var folder in folders.Where(Directory.Exists))
        {
            scannedFiles.AddRange(await _comicLibraryScanner.ScanAsync(folder));
        }

        foreach (var file in scannedFiles)
        {
            Books.Add(new ComicBookListItemViewModel(file.ToComicBook(DateTimeOffset.UtcNow)));
        }

        RaisePropertyChanged(nameof(HasBooks));
        StatusMessage = HasBooks
            ? $"Found {Books.Count} local comic item(s)."
            : "No CBZ, ZIP, PDF, or image folders were found.";

        await LoadCoversAsync();
    }

    private void MovePage(int delta)
    {
        if (SelectedBook is null)
        {
            return;
        }

        var nextPage = Math.Clamp(CurrentPageIndex + delta, 0, Math.Max(0, SelectedBook.ComicBook.PageCount - 1));
        if (nextPage == CurrentPageIndex)
        {
            return;
        }

        CurrentPageIndex = nextPage;
        RaisePropertyChanged(nameof(CurrentPageLabel));
    }

    private async Task LoadReaderPagesAsync()
    {
        ReaderPages.Clear();
        RaisePropertyChanged(nameof(HasReaderPages));

        if (SelectedBook is null)
        {
            ReaderStatus = "Select a book to open the reader.";
            return;
        }

        var comicBook = SelectedBook.ComicBook;
        if (comicBook.Format is ComicFormat.Pdf or ComicFormat.Cbr or ComicFormat.Rar or ComicFormat.SevenZip or ComicFormat.Epub)
        {
            ReaderStatus = $"{comicBook.Format} detected. Rendering for this format is not implemented yet.";
            RaisePropertyChanged(nameof(CurrentPageLabel));
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
            return;
        }

        ReaderStatus = $"Loading {comicBook.Title}...";
        var pages = await _pagePreviewLoader.LoadPagesAsync(comicBook);
        for (var i = 0; i < pages.Count; i++)
        {
            ReaderPages.Add(new ReaderPageViewModel(i + 1, pages[i]));
        }

        ReaderStatus = ReaderPages.Count == 0
            ? "No renderable page was found for this item."
            : comicBook.Title;
        RaisePropertyChanged(nameof(HasReaderPages));
        RaisePropertyChanged(nameof(CurrentPageLabel));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private async Task LoadCoversAsync()
    {
        foreach (var book in Books)
        {
            var cover = await _pagePreviewLoader.LoadPageAsync(book.ComicBook, 0);
            book.SetCoverImage(cover);
        }
    }
}
