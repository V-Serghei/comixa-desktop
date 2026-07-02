using Comixa.Core.Models;

namespace Comixa.Desktop.Reader;

public interface IComicPageCountProvider
{
    Task<int> GetPageCountAsync(ComicBook comicBook, CancellationToken cancellationToken = default);
}
