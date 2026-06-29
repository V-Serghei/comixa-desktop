using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Comixa.Desktop.ViewModels;
using System.Windows.Input;

namespace Comixa.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm ||
            e.KeyModifiers != KeyModifiers.None ||
            IsTextInputActive(e.Source))
        {
            return;
        }

        ICommand? command = e.Key switch
        {
            Key.Left or Key.PageUp => vm.PageBackwardCommand,
            Key.Right or Key.PageDown => vm.PageForwardCommand,
            Key.Home => vm.GoToFirstPageCommand,
            Key.End => vm.GoToLastPageCommand,
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
}
