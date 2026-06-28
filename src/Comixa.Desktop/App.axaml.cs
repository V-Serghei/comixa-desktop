using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Comixa.Core.Repositories;
using Comixa.Data;
using Comixa.Data.Repositories;
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
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Comixa", "Desktop", "comixa.db");

            var database = new ComixaDatabase(dbPath);
            database.EnsureCreated();

            IComicLibraryRepository comicRepository = new SqliteComicLibraryRepository(database);
            IReadingProgressRepository progressRepository = new SqliteReadingProgressRepository(database);
            IShelfRepository shelfRepository = new SqliteShelfRepository(database);
            IBookmarkRepository bookmarkRepository = new SqliteBookmarkRepository(database);
            IUserPreferencesStore preferencesStore = new JsonUserPreferencesStore();

            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel(
                new AvaloniaFolderPicker(mainWindow),
                new LocalComicLibraryScanner(),
                new JsonUserLibrarySettingsStore(),
                new LocalPagePreviewLoader(),
                comicRepository,
                progressRepository,
                preferencesStore,
                shelfRepository,
                bookmarkRepository);

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
