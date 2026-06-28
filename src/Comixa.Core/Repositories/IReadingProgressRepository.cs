using Comixa.Core.Models;

namespace Comixa.Core.Repositories;

public interface IReadingProgressRepository
{
    Task<ReadingProgress?> GetAsync(Guid comicBookId, CancellationToken cancellationToken = default);

    Task SaveAsync(ReadingProgress progress, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingProgress>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid comicBookId, CancellationToken cancellationToken = default);
}
