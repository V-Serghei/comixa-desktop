using Comixa.Core.Models;

namespace Comixa.Core.Repositories;

public interface IShelfRepository
{
    Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Shelf> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid shelfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetBookIdsAsync(Guid shelfId, CancellationToken cancellationToken = default);
    Task AddBookAsync(Guid shelfId, Guid comicBookId, CancellationToken cancellationToken = default);
    Task RemoveBookAsync(Guid shelfId, Guid comicBookId, CancellationToken cancellationToken = default);
}
