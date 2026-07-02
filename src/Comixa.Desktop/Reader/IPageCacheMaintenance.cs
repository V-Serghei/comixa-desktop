using Comixa.Core.Models;

namespace Comixa.Desktop.Reader;

public interface IPageCacheMaintenance
{
    Task PreloadPagesAsync(ComicBook comicBook, int startPageIndex, CancellationToken cancellationToken = default);

    void SetActivePageWindow(ComicBook comicBook, int pageIndex, int pageCount);

    void ClearInactivePages();

    void ClearAllPages();
}
