namespace Comixa.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public string Title => "Comixa Desktop";

    public string Phase => "MVP 1 - Local Desktop Reader Core";

    public string EmptyLibraryMessage => "Choose a local folder to begin building your comic library.";

    public string ReaderPlaceholder => "Open a CBZ archive from the library to read pages, track progress, and add bookmarks.";
}
