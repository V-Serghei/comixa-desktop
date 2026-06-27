using Avalonia.Media.Imaging;
using Comixa.Core.Models;

namespace Comixa.Desktop.Reader;

public interface IPagePreviewLoader
{
    Task<Bitmap?> LoadPageAsync(ComicBook comicBook, int pageIndex, CancellationToken cancellationToken = default);
}
