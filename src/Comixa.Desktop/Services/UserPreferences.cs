using Comixa.Core.Models;

namespace Comixa.Desktop.Services;

public sealed record UserPreferences(
    bool IsDarkTheme,
    ReadingDirection ReadingDirection,
    ReaderWheelAction ReaderWheelAction,
    bool IsEdgePageTurnEnabled,
    bool IsDragPageTurnEnabled,
    bool IsPageTurnInverted,
    ReaderColorTone ReaderColorTone,
    ReaderPageAnimation ReaderPageAnimation,
    bool IsTwoPageMode,
    bool OpenComicsAtLastPosition,
    bool OpenComicsInFullscreen,
    bool IsReaderPreviewPaneEnabled)
{
    public static UserPreferences Default { get; } = new(
        IsDarkTheme: true,
        ReadingDirection: ReadingDirection.LeftToRight,
        ReaderWheelAction: ReaderWheelAction.Scroll,
        IsEdgePageTurnEnabled: true,
        IsDragPageTurnEnabled: true,
        IsPageTurnInverted: false,
        ReaderColorTone: ReaderColorTone.Original,
        ReaderPageAnimation: ReaderPageAnimation.Fade,
        IsTwoPageMode: false,
        OpenComicsAtLastPosition: true,
        OpenComicsInFullscreen: false,
        IsReaderPreviewPaneEnabled: true);
}
