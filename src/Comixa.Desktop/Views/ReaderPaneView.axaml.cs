using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Comixa.Desktop.Services;
using Comixa.Desktop.ViewModels;
using System.ComponentModel;

namespace Comixa.Desktop.Views;

public sealed partial class ReaderPaneView : UserControl
{
    private const double ClickEdgeRatio = 0.24;
    private const double SwipeThreshold = 80;

    private Point? _pressPoint;
    private bool _isLeftPress;
    private bool _didSwipe;
    private readonly TranslateTransform _pageAnimationTransform = new();
    private CancellationTokenSource? _pageAnimationCts;
    private MainWindowViewModel? _viewModel;
    private int _lastAnimatedPageIndex;

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
        ReaderPageHost.RenderTransform = _pageAnimationTransform;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnReaderWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (!vm.HasSelectedBook || vm.IsVerticalMode)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || vm.ReaderWheelAction == ReaderWheelAction.Zoom)
        {
            vm.AdjustReaderZoom(e.Delta.Y);
            e.Handled = true;
            return;
        }

        if (vm.ReaderWheelAction == ReaderWheelAction.PageTurn)
        {
            vm.TurnPageFromWheel(e.Delta.Y);
            e.Handled = true;
        }
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

        if (!vm.IsDragPageTurnEnabled)
        {
            return;
        }

        vm.TurnPageFromSwipe(deltaX);
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

        if (!_didSwipe && vm.IsEdgePageTurnEnabled && Math.Abs(delta.X) < 12 && Math.Abs(delta.Y) < 12)
        {
            var width = ReaderPageHost.Bounds.Width;

            if (releasePoint.X <= width * ClickEdgeRatio)
            {
                vm.TurnPageFromEdge(rightEdge: false);
                e.Handled = true;
            }
            else if (releasePoint.X >= width * (1 - ClickEdgeRatio))
            {
                vm.TurnPageFromEdge(rightEdge: true);
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
            vm.IsSettingsPanelVisible ||
            (!vm.IsEdgePageTurnEnabled && !vm.IsDragPageTurnEnabled))
        {
            return false;
        }

        return e.GetCurrentPoint(ReaderPageHost).Properties.IsLeftButtonPressed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _lastAnimatedPageIndex = _viewModel.CurrentPageIndex;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentPageIndex) ||
            sender is not MainWindowViewModel vm)
        {
            return;
        }

        var direction = vm.CurrentPageIndex >= _lastAnimatedPageIndex ? 1 : -1;
        _lastAnimatedPageIndex = vm.CurrentPageIndex;
        PlayPageAnimation(vm.ReaderPageAnimation, direction);
    }

    private void OnVerticalPageAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control { DataContext: ReaderPageViewModel page })
        {
            page.AttachAndLoad();
        }
    }

    private void OnVerticalPageDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control { DataContext: ReaderPageViewModel page })
        {
            page.Detach();
        }
    }

    private async void PlayPageAnimation(ReaderPageAnimation animation, int direction)
    {
        if (animation == ReaderPageAnimation.None)
        {
            ReaderPageHost.Opacity = 1;
            _pageAnimationTransform.X = 0;
            return;
        }

        _pageAnimationCts?.Cancel();
        _pageAnimationCts = new CancellationTokenSource();
        var token = _pageAnimationCts.Token;

        try
        {
            const int frames = 8;
            for (var frame = 0; frame <= frames; frame++)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var progress = (double)frame / frames;
                ReaderPageHost.Opacity = 0.62 + 0.38 * progress;
                _pageAnimationTransform.X = animation == ReaderPageAnimation.Slide
                    ? (1 - progress) * 28 * direction
                    : 0;

                await Task.Delay(16, token).ConfigureAwait(true);
            }

            ReaderPageHost.Opacity = 1;
            _pageAnimationTransform.X = 0;
        }
        catch (OperationCanceledException) { }
    }

    private void ResetPointerState(PointerEventArgs e)
    {
        _pressPoint = null;
        _isLeftPress = false;
        _didSwipe = false;
        e.Pointer.Capture(null);
    }
}
