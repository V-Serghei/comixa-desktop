using System.Text.Json;

namespace Comixa.Desktop.Services;

public sealed class JsonUserLibrarySettingsStore : IUserLibrarySettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonUserLibrarySettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsPath = Path.Combine(appData, "Comixa", "Desktop", "library-settings.json");
    }

    public async Task<UserLibrarySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new UserLibrarySettings([]);
        }

        await using var stream = File.OpenRead(_settingsPath);
        return await JsonSerializer.DeserializeAsync<UserLibrarySettings>(stream, SerializerOptions, cancellationToken)
            ?? new UserLibrarySettings([]);
    }

    public async Task SaveAsync(UserLibrarySettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
