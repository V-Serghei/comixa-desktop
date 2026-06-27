using Comixa.Core.Models;

namespace Comixa.Core.Repositories;

public interface IReadingProgressRepository
{
    Task<ReadingProgress?> GetAsync(Guid comicBookId, CancellationToken cancellationToken = default);

    Task SaveAsync(ReadingProgress progress, CancellationToken cancellationToken = default);
}
