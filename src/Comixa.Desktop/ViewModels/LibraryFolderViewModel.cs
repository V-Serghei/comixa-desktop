namespace Comixa.Desktop.ViewModels;

public sealed class LibraryFolderViewModel : ViewModelBase
{
    private bool _isActive;

    public LibraryFolderViewModel(string path, Action<string> onSelect)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = path;
        }

        SelectCommand = new RelayCommand(() => onSelect(Path));
    }

    public string Path { get; }

    public string Name { get; }

    public RelayCommand SelectCommand { get; }

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
}
