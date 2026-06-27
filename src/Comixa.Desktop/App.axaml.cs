using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Comixa.Desktop.Reader;
using Comixa.Desktop.Services;
using Comixa.Desktop.ViewModels;
using Comixa.Desktop.Views;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel(
                new AvaloniaFolderPicker(mainWindow),
                new LocalComicLibraryScanner(),
                new JsonUserLibrarySettingsStore(),
                new LocalPagePreviewLoader());
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
