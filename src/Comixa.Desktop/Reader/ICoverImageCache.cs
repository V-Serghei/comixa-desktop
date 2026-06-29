using Avalonia.Media.Imaging;
using Comixa.Core.Models;

namespace Comixa.Desktop.Reader;

public interface ICoverImageCache
{
    Task<Bitmap?> LoadCoverAsync(ComicBook comicBook, CancellationToken cancellationToken = default);
}
