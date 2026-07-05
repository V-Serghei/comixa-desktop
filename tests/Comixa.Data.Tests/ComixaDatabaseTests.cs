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

    [Fact]
    public async Task EnsureCreatedMigratesLegacyDatabaseSchema()
    {
        var databasePath = Path.Join(Path.GetTempPath(), "comixa-data-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        try
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE comics (
                        id           TEXT NOT NULL PRIMARY KEY,
                        title        TEXT NOT NULL,
                        series_name  TEXT,
                        issue_number INTEGER,
                        file_path    TEXT NOT NULL UNIQUE,
                        format       INTEGER NOT NULL,
                        page_count   INTEGER NOT NULL DEFAULT 0,
                        cover_path   TEXT,
                        added_at     TEXT NOT NULL
                    );

                    CREATE TABLE reading_progress (
                        comic_book_id TEXT NOT NULL PRIMARY KEY,
                        page_number   INTEGER NOT NULL,
                        updated_at    TEXT NOT NULL
                    );

                    CREATE TABLE bookmarks (
                        id            TEXT NOT NULL PRIMARY KEY,
                        comic_book_id TEXT NOT NULL,
                        page_number   INTEGER NOT NULL,
                        note          TEXT,
                        created_at    TEXT NOT NULL
                    );

                    CREATE TABLE shelves (
                        id         TEXT NOT NULL PRIMARY KEY,
                        name       TEXT NOT NULL,
                        created_at TEXT NOT NULL
                    );

                    CREATE TABLE shelf_entries (
                        shelf_id      TEXT NOT NULL,
                        comic_book_id TEXT NOT NULL,
                        added_at      TEXT NOT NULL,
                        PRIMARY KEY (shelf_id, comic_book_id)
                    );
                    """;
                command.ExecuteNonQuery();
            }

            var now = DateTimeOffset.UtcNow;
            var supportedComicId = Guid.NewGuid();
            var removedComicId = Guid.NewGuid();
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO comics (id, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at)
                    VALUES ($supported_id, 'Supported', 'Supported', 1, 'supported.cbz', 0, 10, NULL, $now),
                           ($removed_id, 'Loose Folder', 'Loose Folder', NULL, 'loose-folder', 3, 2, NULL, $now);

                    INSERT INTO reading_progress (comic_book_id, page_number, updated_at)
                    VALUES ($supported_id, 4, $now),
                           ($removed_id, 1, $now);

                    INSERT INTO bookmarks (id, comic_book_id, page_number, note, created_at)
                    VALUES ($bookmark_id, $supported_id, 4, 'note', $now);
                    """;
                command.Parameters.AddWithValue("$supported_id", supportedComicId.ToString());
                command.Parameters.AddWithValue("$removed_id", removedComicId.ToString());
                command.Parameters.AddWithValue("$bookmark_id", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("$now", now.ToString("O"));
                command.ExecuteNonQuery();
            }

            var database = new ComixaDatabase(databasePath);
            database.EnsureCreated();

            var comics = new SqliteComicLibraryRepository(database);
            var progressRepository = new SqliteReadingProgressRepository(database);
            var bookmarkRepository = new SqliteBookmarkRepository(database);

            var storedComic = Assert.Single(await comics.GetAllAsync());
            var storedProgress = Assert.Single(await progressRepository.GetAllAsync());
            var storedBookmark = Assert.Single(await bookmarkRepository.GetForComicAsync(supportedComicId));

            Assert.Equal(supportedComicId, storedComic.Id);
            Assert.False(string.IsNullOrWhiteSpace(storedComic.SyncId));
            Assert.Equal($"legacy:{supportedComicId}", storedComic.ContentFingerprint);
            Assert.Equal(now, storedComic.UpdatedAt);
            Assert.Equal(4, storedProgress.PageIndex);
            Assert.Equal(storedComic.SyncId, storedProgress.ComicSyncId);
            Assert.Equal(10, storedProgress.TotalPages);
            Assert.Equal(ReadingStatus.InProgress, storedProgress.Status);
            Assert.Equal(4, storedBookmark.PageIndex);
            Assert.Equal(storedComic.SyncId, storedBookmark.ComicSyncId);
            Assert.False(string.IsNullOrWhiteSpace(storedBookmark.SyncId));
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
