namespace Comixa.Desktop.ViewModels;

public sealed class SeriesVariantViewModel(
    string title,
    string subtitle,
    IReadOnlyList<ComicBookListItemViewModel> books,
    IReadOnlyList<LibraryRowViewModel> rows)
{
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;
    public IReadOnlyList<ComicBookListItemViewModel> Books { get; } = books;
    public IReadOnlyList<LibraryRowViewModel> Rows { get; } = rows;
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
}
