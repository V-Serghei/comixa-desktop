using Comixa.Core.Models;

namespace Comixa.Desktop.Services;

public sealed record UserPreferences(
    bool IsDarkTheme,
    ReadingDirection ReadingDirection)
{
    public static UserPreferences Default { get; } = new(IsDarkTheme: true, ReadingDirection: ReadingDirection.LeftToRight);
}
