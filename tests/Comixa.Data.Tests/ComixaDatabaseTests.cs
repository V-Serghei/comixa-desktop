using Comixa.Data;
using Comixa.Core.Models;
using Comixa.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace Comixa.Data.Tests;

public sealed class ComixaDatabaseTests
{
    [Fact]
    public void ConstructorStoresDatabasePath()
    {
        var database = new ComixaDatabase(Path.Join("data", "comixa.db"));

        Assert.EndsWith(Path.Join("data", "comixa.db"), database.DatabasePath);
    }

    [Fact]
    public void ConstructorRejectsBlankDatabasePath()
    {
        Assert.Throws<ArgumentException>(() => new ComixaDatabase(" "));
    }

    [Fact]
    public async Task RepositoriesPersistSyncReadyFields()
    {
        var databasePath = Path.Join(Path.GetTempPath(), "comixa-data-tests", $"{Guid.NewGuid():N}.db");
        var database = new ComixaDatabase(databasePath);

        try
        {
            database.EnsureCreated();
            var comics = new SqliteComicLibraryRepository(database);
            var progressRepository = new SqliteReadingProgressRepository(database);
            var bookmarkRepository = new SqliteBookmarkRepository(database);
            var now = DateTimeOffset.UtcNow;
            var comic = new ComicBook(
                Guid.NewGuid(),
                "comic-sync-id",
                "sha256:abc123",
                "A Quiet Chapter",
                "A Quiet Chapter",
                3,
                Path.Join("library", "quiet.cbz"),
                ComicFormat.Cbz,
                24,
                null,
                now,
                now,
                null);

            await comics.UpsertAsync(comic);
            await progressRepository.SaveAsync(new ReadingProgress(
                comic.Id,
                comic.SyncId,
                5,
                comic.PageCount,
                ReadingStatus.InProgress,
                now));
            await bookmarkRepository.AddAsync(new Bookmark(
                Guid.NewGuid(),
                "bookmark-sync-id",
                comic.Id,
                comic.SyncId,
                5,
                "note",
                now,
                now,
                null));

            var storedComic = Assert.Single(await comics.GetAllAsync());
            var storedProgress = Assert.Single(await progressRepository.GetAllAsync());
            var storedBookmark = Assert.Single(await bookmarkRepository.GetForComicAsync(comic.Id));

            Assert.Equal("comic-sync-id", storedComic.SyncId);
            Assert.Equal("sha256:abc123", storedComic.ContentFingerprint);
            Assert.Equal("comic-sync-id", storedProgress.ComicSyncId);
            Assert.Equal(5, storedProgress.PageIndex);
            Assert.Equal(ReadingStatus.InProgress, storedProgress.Status);
            Assert.Equal("bookmark-sync-id", storedBookmark.SyncId);
            Assert.Equal("comic-sync-id", storedBookmark.ComicSyncId);
            Assert.Equal(5, storedBookmark.PageIndex);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
