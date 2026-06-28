using System.Text.Json;
using Comixa.Core.Models;

namespace Comixa.Desktop.Services;

public sealed class JsonUserPreferencesStore : IUserPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public JsonUserPreferencesStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _path = Path.Combine(appData, "Comixa", "Desktop", "preferences.json");
    }

    public async Task<UserPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
            return UserPreferences.Default;

        try
        {
            await using var stream = File.OpenRead(_path);
            var dto = await JsonSerializer.DeserializeAsync<PreferencesDto>(stream, SerializerOptions, cancellationToken);
            if (dto is null)
                return UserPreferences.Default;

            return new UserPreferences(
                dto.IsDarkTheme,
                Enum.TryParse<ReadingDirection>(dto.ReadingDirection, out var dir) ? dir : ReadingDirection.LeftToRight);
        }
        catch
        {
            return UserPreferences.Default;
        }
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        var dto = new PreferencesDto(preferences.IsDarkTheme, preferences.ReadingDirection.ToString());

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
    }

    private sealed record PreferencesDto(bool IsDarkTheme, string ReadingDirection);
}
