namespace Comixa.Desktop.ViewModels;

public sealed class LibraryFolderViewModel : ViewModelBase
{
    private bool _isActive;
    private int _bookCount;

    public LibraryFolderViewModel(string path, Action<string> onSelect, Func<string, Task> onRemove)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = path;
        }

        SelectCommand = new RelayCommand(() => onSelect(Path));
        RemoveCommand = new AsyncRelayCommand(() => onRemove(Path));
    }

    public string Path { get; }

    public string Name { get; }

    public RelayCommand SelectCommand { get; }
    public AsyncRelayCommand RemoveCommand { get; }
    public string CountLabel => _bookCount == 1 ? "1 comic" : $"{_bookCount} comics";

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            RaisePropertyChanged();
        }
    }

    public void SetBookCount(int bookCount)
    {
        bookCount = Math.Max(0, bookCount);
        if (_bookCount == bookCount) return;

        _bookCount = bookCount;
        RaisePropertyChanged(nameof(CountLabel));
    }
}
