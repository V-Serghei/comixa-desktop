using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Comixa.Desktop.ViewModels;

namespace Comixa.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Get<Grid>("ReaderGrid")
            .AddHandler(PointerWheelChangedEvent, OnReaderWheelChanged,
                        RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnReaderWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!vm.HasSelectedBook || vm.IsVerticalMode) return;

        if (e.Delta.Y < 0)
        {
            if (vm.PageForwardCommand.CanExecute(null))
                vm.PageForwardCommand.Execute(null);
        }
        else if (e.Delta.Y > 0)
        {
            if (vm.PageBackwardCommand.CanExecute(null))
                vm.PageBackwardCommand.Execute(null);
        }
        e.Handled = true;
    }
}
