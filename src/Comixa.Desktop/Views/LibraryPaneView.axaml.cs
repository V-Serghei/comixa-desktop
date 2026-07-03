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
                UpdateShelfMenu(menu, item);
            }
        }
    }

    private static void UpdateShelfMenu(ContextMenu menu, SingleLibraryItemViewModel item)
    {
        var shelfMenu = menu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(menuItem => string.Equals(menuItem.Header?.ToString(), "Add to shelf...", StringComparison.Ordinal));
        var emptyShelfMenu = menu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(menuItem => string.Equals(menuItem.Header?.ToString(), "No shelves yet", StringComparison.Ordinal));

        if (shelfMenu is not null)
        {
            shelfMenu.ItemsSource = item.ShelfItems
                .Select(shelfItem => new MenuItem
                {
                    Header = shelfItem.MenuText,
                    Command = shelfItem.ToggleCommand
                })
                .ToArray();

            shelfMenu.IsVisible = item.HasShelfItems;
            shelfMenu.IsEnabled = item.HasShelfItems;
        }

        if (emptyShelfMenu is not null)
        {
            emptyShelfMenu.IsVisible = !item.HasShelfItems;
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
