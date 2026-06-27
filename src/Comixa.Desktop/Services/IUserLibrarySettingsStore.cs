namespace Comixa.Desktop.Services;

public interface IUserLibrarySettingsStore
{
    Task<UserLibrarySettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserLibrarySettings settings, CancellationToken cancellationToken = default);
}
