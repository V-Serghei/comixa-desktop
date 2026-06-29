using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Comixa.Desktop.ViewModels;

namespace Comixa.Desktop.Views;

public sealed partial class ReaderPaneView : UserControl
{
    private const double ClickEdgeRatio = 0.24;
    private const double SwipeThreshold = 80;

    private Point? _pressPoint;
    private bool _isLeftPress;
    private bool _didSwipe;

    public ReaderPaneView()
    {
        InitializeComponent();
        ReaderGrid.AddHandler(
            PointerWheelChangedEvent,
            OnReaderWheelChanged,
            RoutingStrategies.Tunnel,
            handledEventsToo: false);
        ReaderPageHost.AddHandler(
            PointerPressedEvent,
            OnReaderPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: false);
        ReaderPageHost.AddHandler(
            PointerMovedEvent,
            OnReaderPointerMoved,
            RoutingStrategies.Bubble,
            handledEventsToo: false);
        ReaderPageHost.AddHandler(
            PointerReleasedEvent,
            OnReaderPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: false);
    }

    private void OnReaderWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (!vm.HasSelectedBook || vm.IsVerticalMode || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        vm.AdjustReaderZoom(e.Delta.Y);
        e.Handled = true;
    }

    private void OnReaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanUsePageGestures(e))
        {
            return;
        }

        _pressPoint = e.GetPosition(ReaderPageHost);
        _isLeftPress = true;
        _didSwipe = false;
        e.Pointer.Capture(ReaderPageHost);
    }

    private void OnReaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isLeftPress || _pressPoint is null || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var current = e.GetPosition(ReaderPageHost);
        var deltaX = current.X - _pressPoint.Value.X;
        var deltaY = current.Y - _pressPoint.Value.Y;

        if (Math.Abs(deltaX) < SwipeThreshold || Math.Abs(deltaX) < Math.Abs(deltaY) * 1.25)
        {
            return;
        }

        ExecutePageTurn(vm, deltaX < 0);
        _didSwipe = true;
        _isLeftPress = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnReaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isLeftPress || _pressPoint is null || DataContext is not MainWindowViewModel vm)
        {
            ResetPointerState(e);
            return;
        }

        var releasePoint = e.GetPosition(ReaderPageHost);
        var delta = releasePoint - _pressPoint.Value;

        if (!_didSwipe && Math.Abs(delta.X) < 12 && Math.Abs(delta.Y) < 12)
        {
            var width = ReaderPageHost.Bounds.Width;

            if (releasePoint.X <= width * ClickEdgeRatio)
            {
                ExecutePageTurn(vm, forward: false);
                e.Handled = true;
            }
            else if (releasePoint.X >= width * (1 - ClickEdgeRatio))
            {
                ExecutePageTurn(vm, forward: true);
                e.Handled = true;
            }
        }

        ResetPointerState(e);
    }

    private bool CanUsePageGestures(PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            !vm.HasSelectedBook ||
            vm.IsVerticalMode ||
            vm.IsSettingsPanelVisible)
        {
            return false;
        }

        return e.GetCurrentPoint(ReaderPageHost).Properties.IsLeftButtonPressed;
    }

    private static void ExecutePageTurn(MainWindowViewModel vm, bool forward)
    {
        var command = forward ? vm.PageForwardCommand : vm.PageBackwardCommand;

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void ResetPointerState(PointerEventArgs e)
    {
        _pressPoint = null;
        _isLeftPress = false;
        _didSwipe = false;
        e.Pointer.Capture(null);
    }
}
