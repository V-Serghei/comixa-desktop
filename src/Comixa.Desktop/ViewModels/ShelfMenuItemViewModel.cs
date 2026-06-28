namespace Comixa.Desktop.ViewModels;

public sealed class ShelfMenuItemViewModel
{
    public ShelfMenuItemViewModel(Guid shelfId, string shelfName, bool isInShelf, Action<Guid, bool> onToggle)
    {
        ShelfId = shelfId;
        MenuText = isInShelf ? $"✓  {shelfName}" : $"   {shelfName}";
        ToggleCommand = new RelayCommand(() => onToggle(shelfId, !isInShelf));
    }

    public Guid ShelfId { get; }
    public string MenuText { get; }
    public RelayCommand ToggleCommand { get; }
}
