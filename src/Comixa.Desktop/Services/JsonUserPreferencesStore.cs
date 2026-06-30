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
                Enum.TryParse<ReadingDirection>(dto.ReadingDirection, out var dir) ? dir : ReadingDirection.LeftToRight,
                Enum.TryParse<ReaderWheelAction>(dto.ReaderWheelAction, out var wheelAction) ? wheelAction : UserPreferences.Default.ReaderWheelAction,
                dto.IsEdgePageTurnEnabled ?? UserPreferences.Default.IsEdgePageTurnEnabled,
                dto.IsDragPageTurnEnabled ?? UserPreferences.Default.IsDragPageTurnEnabled,
                dto.IsPageTurnInverted ?? UserPreferences.Default.IsPageTurnInverted,
                Enum.TryParse<ReaderColorTone>(dto.ReaderColorTone, out var colorTone) ? colorTone : UserPreferences.Default.ReaderColorTone,
                Enum.TryParse<ReaderPageAnimation>(dto.ReaderPageAnimation, out var animation) ? animation : UserPreferences.Default.ReaderPageAnimation,
                dto.IsTwoPageMode ?? UserPreferences.Default.IsTwoPageMode,
                dto.OpenComicsAtLastPosition ?? UserPreferences.Default.OpenComicsAtLastPosition,
                dto.OpenComicsInFullscreen ?? UserPreferences.Default.OpenComicsInFullscreen,
                dto.IsReaderPreviewPaneEnabled ?? UserPreferences.Default.IsReaderPreviewPaneEnabled);
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

        var dto = new PreferencesDto(
            preferences.IsDarkTheme,
            preferences.ReadingDirection.ToString(),
            preferences.ReaderWheelAction.ToString(),
            preferences.IsEdgePageTurnEnabled,
            preferences.IsDragPageTurnEnabled,
            preferences.IsPageTurnInverted,
            preferences.ReaderColorTone.ToString(),
            preferences.ReaderPageAnimation.ToString(),
            preferences.IsTwoPageMode,
            preferences.OpenComicsAtLastPosition,
            preferences.OpenComicsInFullscreen,
            preferences.IsReaderPreviewPaneEnabled);

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, dto, SerializerOptions, cancellationToken);
    }

    private sealed record PreferencesDto(
        bool IsDarkTheme,
        string ReadingDirection,
        string? ReaderWheelAction,
        bool? IsEdgePageTurnEnabled,
        bool? IsDragPageTurnEnabled,
        bool? IsPageTurnInverted,
        string? ReaderColorTone,
        string? ReaderPageAnimation,
        bool? IsTwoPageMode,
        bool? OpenComicsAtLastPosition,
        bool? OpenComicsInFullscreen,
        bool? IsReaderPreviewPaneEnabled);
}
