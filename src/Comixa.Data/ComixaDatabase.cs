using Microsoft.Data.Sqlite;

namespace Comixa.Data;

public sealed class ComixaDatabase
{
    public ComixaDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();
        return connection;
    }

    public void EnsureCreated()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS comics (
                id                  TEXT NOT NULL PRIMARY KEY,
                sync_id             TEXT NOT NULL UNIQUE,
                content_fingerprint TEXT NOT NULL,
                title               TEXT NOT NULL,
                series_name         TEXT,
                issue_number        INTEGER,
                file_path           TEXT NOT NULL UNIQUE,
                format              INTEGER NOT NULL,
                page_count          INTEGER NOT NULL DEFAULT 0,
                cover_path          TEXT,
                added_at            TEXT NOT NULL,
                updated_at          TEXT NOT NULL,
                deleted_at          TEXT
            );

            CREATE TABLE IF NOT EXISTS reading_progress (
                comic_book_id TEXT NOT NULL PRIMARY KEY,
                comic_sync_id TEXT NOT NULL,
                page_index    INTEGER NOT NULL,
                total_pages   INTEGER NOT NULL,
                status        INTEGER NOT NULL,
                updated_at    TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS bookmarks (
                id            TEXT NOT NULL PRIMARY KEY,
                sync_id       TEXT NOT NULL UNIQUE,
                comic_book_id TEXT NOT NULL,
                comic_sync_id TEXT NOT NULL,
                page_index    INTEGER NOT NULL,
                note          TEXT,
                created_at    TEXT NOT NULL,
                updated_at    TEXT NOT NULL,
                deleted_at    TEXT
            );

            CREATE TABLE IF NOT EXISTS shelves (
                id         TEXT NOT NULL PRIMARY KEY,
                name       TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS shelf_entries (
                shelf_id      TEXT NOT NULL,
                comic_book_id TEXT NOT NULL,
                added_at      TEXT NOT NULL,
                PRIMARY KEY (shelf_id, comic_book_id),
                FOREIGN KEY (shelf_id) REFERENCES shelves(id) ON DELETE CASCADE
            );

            PRAGMA foreign_keys = ON;
            """;
        command.ExecuteNonQuery();
    }
}
