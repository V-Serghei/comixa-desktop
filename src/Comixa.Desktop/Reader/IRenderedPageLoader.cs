using Avalonia;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;

namespace Comixa.Desktop.Reader;

public interface IRenderedPageLoader
{
    Task<Bitmap?> LoadRenderedPageAsync(
        ComicBook comicBook,
        int pageIndex,
        PixelSize targetSize,
        CancellationToken cancellationToken = default);
}
