using Comixa.Core.Models;

namespace Comixa.Reader.Archives;

public interface IComicArchiveReader
{
    Task<IReadOnlyList<ComicPage>> GetPagesAsync(string archivePath, CancellationToken cancellationToken = default);

    Task<Stream> OpenPageAsync(string archivePath, ComicPage page, CancellationToken cancellationToken = default);
}
