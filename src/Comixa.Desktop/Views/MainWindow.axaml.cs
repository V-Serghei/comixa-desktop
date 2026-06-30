using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Comixa.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows.Input;

namespace Comixa.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private WindowState _windowStateBeforeReaderFullscreen = WindowState.Normal;
    private bool _isApplyingReaderFullscreen;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Escape && vm.IsReaderFullscreen)
        {
            Execute(vm.ToggleReaderFullscreenCommand);
            e.Handled = true;
            return;
        }

        if (IsTextInputActive(e.Source))
        {
            return;
        }

        ICommand? command = e.Key switch
        {
            Key.Left or Key.PageUp => vm.PageBackwardCommand,
            Key.Right or Key.PageDown => vm.PageForwardCommand,
            Key.Home => vm.GoToFirstPageCommand,
            Key.End => vm.GoToLastPageCommand,
            Key.F11 => vm.ToggleReaderFullscreenCommand,
            Key.F => vm.ToggleFitModeCommand,
            Key.B => vm.ToggleBookmarkCommand,
            Key.S => vm.ToggleSettingsPanelCommand,
            _ => null
        };

        if (command is null)
        {
            return;
        }

        Execute(command);
        e.Handled = true;
    }

    private static bool IsTextInputActive(object? source)
    {
        return source is TextBox ||
            source is Visual visual && visual.GetVisualAncestors().OfType<TextBox>().Any();
    }

    private static void Execute(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
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
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyReaderFullscreenState();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsReaderFullscreen))
        {
            ApplyReaderFullscreenState();
        }
    }

    private void ApplyReaderFullscreenState()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.IsReaderFullscreen)
        {
            if (!_isApplyingReaderFullscreen)
            {
                _windowStateBeforeReaderFullscreen = WindowState;
            }

            _isApplyingReaderFullscreen = true;
            WindowState = WindowState.FullScreen;
            return;
        }

        if (_isApplyingReaderFullscreen)
        {
            WindowState = _windowStateBeforeReaderFullscreen;
            _isApplyingReaderFullscreen = false;
        }
    }
}
