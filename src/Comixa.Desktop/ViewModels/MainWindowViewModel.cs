using System.Collections.ObjectModel;
using Comixa.Desktop.Services;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IFolderPicker _folderPicker;
    private readonly IComicLibraryScanner _comicLibraryScanner;
    private string _statusMessage = "Choose a local folder to begin building your comic library.";
    private bool _hasBooks;

    public MainWindowViewModel(IFolderPicker folderPicker, IComicLibraryScanner comicLibraryScanner)
    {
        _folderPicker = folderPicker;
        _comicLibraryScanner = comicLibraryScanner;
        ScanFolderCommand = new AsyncRelayCommand(ScanFolderAsync);
    }

    public string Title => "Comixa Desktop";

    public string Phase => "MVP 1 - Local Desktop Reader Core";

    public ObservableCollection<ComicBookListItemViewModel> Books { get; } = [];

    public AsyncRelayCommand ScanFolderCommand { get; }

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

    public bool HasBooks
    {
        get => _hasBooks;
        private set
        {
            if (_hasBooks == value)
            {
                return;
            }

            _hasBooks = value;
            RaisePropertyChanged();
        }
    }

    public string ReaderPlaceholder => "Open a CBZ archive from the library to read pages, track progress, and add bookmarks.";

    private async Task ScanFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync();
        if (folder is null)
        {
            return;
        }

        StatusMessage = "Scanning local comic archives...";
        Books.Clear();

        var scannedFiles = await _comicLibraryScanner.ScanAsync(folder);
        foreach (var file in scannedFiles)
        {
            Books.Add(new ComicBookListItemViewModel(file.ToComicBook(DateTimeOffset.UtcNow)));
        }

        HasBooks = Books.Count > 0;
        StatusMessage = HasBooks
            ? $"Found {Books.Count} local comic archive(s)."
            : "No CBZ or ZIP archives were found in that folder.";
    }
}
