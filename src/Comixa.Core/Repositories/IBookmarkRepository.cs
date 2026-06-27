using Comixa.Core.Models;

namespace Comixa.Core.Repositories;

public interface IBookmarkRepository
{
    Task<IReadOnlyList<Bookmark>> GetForComicAsync(Guid comicBookId, CancellationToken cancellationToken = default);

    Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
