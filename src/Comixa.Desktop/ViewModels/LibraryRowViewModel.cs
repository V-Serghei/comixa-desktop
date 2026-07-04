namespace Comixa.Desktop.ViewModels;

public sealed class LibraryRowViewModel(IReadOnlyList<LibraryItemViewModel> items, int columnCount)
{
    public IReadOnlyList<LibraryItemViewModel> Items { get; } = items;
    public int ColumnCount { get; } = columnCount;
}
