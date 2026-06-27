using Comixa.Core.Models;

namespace Comixa.Core.Repositories;

public interface IComicLibraryRepository
{
    Task<IReadOnlyList<ComicBook>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ComicBook?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpsertAsync(ComicBook comicBook, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
