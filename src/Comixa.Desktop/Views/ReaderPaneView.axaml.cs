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
    private const double ClickMaxDistance = 10;
    private const double PanStartDistance = 8;
    private const double SwipeThreshold = 90;
    private const double SwipeVelocityThreshold = 780;
    private static readonly TimeSpan ClickMaxDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan SwipeMaxDuration = TimeSpan.FromMilliseconds(460);

    private Point? _pressPoint;
    private Vector _panStartOffset;
    private Vector _panOffset;
    private DateTimeOffset _pressStartedAt;
    private bool _isLeftPress;
    private bool _isPanning;
    private bool _hasMovedBeyondClick;
    private readonly TranslateTransform _pageAnimationTransform = new();
    private readonly TranslateTransform _pagePanTransform = new();
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
        FitPageSurface.RenderTransform = _pagePanTransform;
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
            ClampPanOffset();
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
        _panStartOffset = _panOffset;
        _pressStartedAt = DateTimeOffset.UtcNow;
        _isLeftPress = true;
        _isPanning = false;
        _hasMovedBeyondClick = false;
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
        var totalDelta = new Vector(deltaX, deltaY);
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        if (distance > ClickMaxDistance)
        {
            _hasMovedBeyondClick = true;
        }

        if (!CanPanReader(vm) || distance < PanStartDistance)
        {
            return;
        }

        _isPanning = true;
        SetPanOffset(_panStartOffset + totalDelta);
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
        var duration = DateTimeOffset.UtcNow - _pressStartedAt;

        if (vm.IsDragPageTurnEnabled && IsSwipe(delta, duration))
        {
            ResetPanOffset();
            vm.TurnPageFromSwipe(delta.X);
            e.Handled = true;
            ResetPointerState(e);
            return;
        }

        if (_isPanning)
        {
            e.Handled = true;
            ResetPointerState(e);
            return;
        }

        if (!_hasMovedBeyondClick &&
            duration <= ClickMaxDuration &&
            vm.IsEdgePageTurnEnabled &&
            Math.Abs(delta.X) < ClickMaxDistance &&
            Math.Abs(delta.Y) < ClickMaxDistance)
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
            vm.IsReaderSettingsPanelVisible)
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
        if (sender is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.PropertyName is nameof(MainWindowViewModel.ReaderZoom)
            or nameof(MainWindowViewModel.IsFitPageMode)
            or nameof(MainWindowViewModel.IsTwoPageMode))
        {
            ClampPanOffset();
            return;
        }

        if (e.PropertyName != nameof(MainWindowViewModel.CurrentPageIndex))
        {
            return;
        }

        ResetPanOffset();
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
        _panStartOffset = _panOffset;
        _isLeftPress = false;
        _isPanning = false;
        _hasMovedBeyondClick = false;
        e.Pointer.Capture(null);
    }

    private bool CanPanReader(MainWindowViewModel vm)
    {
        return vm.IsFitPageMode && vm.ReaderZoom > 1.001;
    }

    private static bool IsSwipe(Vector delta, TimeSpan duration)
    {
        if (Math.Abs(delta.X) < SwipeThreshold || Math.Abs(delta.X) < Math.Abs(delta.Y) * 1.35)
        {
            return false;
        }

        var seconds = Math.Max(duration.TotalSeconds, 0.001);
        var velocity = Math.Abs(delta.X) / seconds;
        return duration <= SwipeMaxDuration || velocity >= SwipeVelocityThreshold;
    }

    private void ClampPanOffset()
    {
        if (DataContext is not MainWindowViewModel vm || !CanPanReader(vm))
        {
            ResetPanOffset();
            return;
        }

        SetPanOffset(_panOffset);
    }

    private void SetPanOffset(Vector offset)
    {
        if (DataContext is not MainWindowViewModel vm || !CanPanReader(vm))
        {
            offset = new Vector(0, 0);
        }
        else
        {
            var zoomOverflow = Math.Max(0, vm.ReaderZoom - 1);
            var maxX = ReaderPageHost.Bounds.Width * zoomOverflow / 2;
            var maxY = ReaderPageHost.Bounds.Height * zoomOverflow / 2;
            offset = new Vector(
                Math.Clamp(offset.X, -maxX, maxX),
                Math.Clamp(offset.Y, -maxY, maxY));
        }

        _panOffset = offset;
        _pagePanTransform.X = offset.X;
        _pagePanTransform.Y = offset.Y;
    }

    private void ResetPanOffset()
    {
        _panOffset = new Vector(0, 0);
        _panStartOffset = _panOffset;
        _pagePanTransform.X = 0;
        _pagePanTransform.Y = 0;
    }
}
