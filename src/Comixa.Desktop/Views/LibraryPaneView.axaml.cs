using Avalonia;
using Avalonia.Controls;
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
}
