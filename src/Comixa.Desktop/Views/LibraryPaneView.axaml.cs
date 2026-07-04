using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Comixa.Desktop.ViewModels;

namespace Comixa.Desktop.Views;

public sealed partial class LibraryPaneView : UserControl
{
    public LibraryPaneView()
    {
        InitializeComponent();
        LibraryContent.SizeChanged += OnLibraryContentSizeChanged;
        DataContextChanged += (_, _) => UpdateLibraryColumnCount();
    }

    private void OnLibraryContentSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLibraryColumnCount();
    }

    private void OnLibraryItemContextMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { PlacementTarget: Control target } menu)
        {
            menu.DataContext = target.DataContext;
            if (target.DataContext is SingleLibraryItemViewModel item)
            {
                item.RefreshShelfItems();
            }
        }
    }

    private void UpdateLibraryColumnCount()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetLibraryViewportWidth(LibraryContent.Bounds.Width);
        }
    }

    private void OnLibraryItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            sender is not Control control)
        {
            return;
        }

        var book = control.DataContext switch
        {
            SingleLibraryItemViewModel single => single.Item,
            ComicBookListItemViewModel item => item,
            _ => null
        };

        if (book is null)
        {
            return;
        }

        viewModel.OpenBookFullscreenCommand.Execute(book);
        e.Handled = true;
    }
}
