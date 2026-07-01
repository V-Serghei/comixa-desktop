using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Comixa.Desktop.ViewModels;

namespace Comixa.Desktop.Views;

public sealed partial class LibraryPaneView : UserControl
{
    public LibraryPaneView()
    {
        InitializeComponent();
        LibraryItemsList.SizeChanged += OnLibraryItemsListSizeChanged;
        DataContextChanged += (_, _) => UpdateLibraryColumnCount();
    }

    private void OnLibraryItemsListSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLibraryColumnCount();
    }

    private void UpdateLibraryColumnCount()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetLibraryViewportWidth(LibraryItemsList.Bounds.Width);
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
