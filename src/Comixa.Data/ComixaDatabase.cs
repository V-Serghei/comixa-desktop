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
            PRAGMA foreign_keys = ON;

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
            """;
        command.ExecuteNonQuery();

        MigrateExistingSchema(connection);
    }

    private static void MigrateExistingSchema(SqliteConnection connection)
    {
        EnsureColumn(connection, "comics", "sync_id", "TEXT");
        EnsureColumn(connection, "comics", "content_fingerprint", "TEXT");
        EnsureColumn(connection, "comics", "updated_at", "TEXT");
        EnsureColumn(connection, "comics", "deleted_at", "TEXT");
        ExecuteNonQuery(connection, """
            DELETE FROM comics
            WHERE format NOT IN (0, 1, 2);

            UPDATE comics
            SET sync_id = lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' ||
                substr(hex(randomblob(2)), 2) || '-' ||
                substr('89ab', abs(random()) % 4 + 1, 1) ||
                substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6)))
            WHERE sync_id IS NULL OR sync_id = '';

            UPDATE comics
            SET content_fingerprint = 'legacy:' || id
            WHERE content_fingerprint IS NULL OR content_fingerprint = '';

            UPDATE comics
            SET updated_at = added_at
            WHERE updated_at IS NULL OR updated_at = '';

            CREATE UNIQUE INDEX IF NOT EXISTS idx_comics_sync_id ON comics(sync_id);
            """);

        EnsureColumn(connection, "reading_progress", "comic_sync_id", "TEXT");
        EnsureColumn(connection, "reading_progress", "page_index", "INTEGER");
        EnsureColumn(connection, "reading_progress", "total_pages", "INTEGER");
        EnsureColumn(connection, "reading_progress", "status", "INTEGER");
        if (ColumnExists(connection, "reading_progress", "page_number"))
        {
            ExecuteNonQuery(connection, """
                UPDATE reading_progress
                SET page_index = page_number
                WHERE page_index IS NULL;
                """);
        }

        ExecuteNonQuery(connection, """
            DELETE FROM reading_progress
            WHERE NOT EXISTS (
                SELECT 1 FROM comics WHERE comics.id = reading_progress.comic_book_id
            );

            UPDATE reading_progress
            SET page_index = 0
            WHERE page_index IS NULL;

            UPDATE reading_progress
            SET comic_sync_id = COALESCE((
                    SELECT sync_id FROM comics WHERE comics.id = reading_progress.comic_book_id
                ), comic_book_id)
            WHERE comic_sync_id IS NULL OR comic_sync_id = '';

            UPDATE reading_progress
            SET total_pages = COALESCE((
                    SELECT page_count FROM comics WHERE comics.id = reading_progress.comic_book_id
                ), 0)
            WHERE total_pages IS NULL;

            UPDATE reading_progress
            SET status = CASE
                WHEN total_pages > 0 AND page_index >= total_pages - 1 THEN 2
                ELSE 1
            END
            WHERE status IS NULL;
            """);

        EnsureColumn(connection, "bookmarks", "sync_id", "TEXT");
        EnsureColumn(connection, "bookmarks", "comic_sync_id", "TEXT");
        EnsureColumn(connection, "bookmarks", "page_index", "INTEGER");
        EnsureColumn(connection, "bookmarks", "updated_at", "TEXT");
        EnsureColumn(connection, "bookmarks", "deleted_at", "TEXT");
        if (ColumnExists(connection, "bookmarks", "page_number"))
        {
            ExecuteNonQuery(connection, """
                UPDATE bookmarks
                SET page_index = page_number
                WHERE page_index IS NULL;
                """);
        }

        ExecuteNonQuery(connection, """
            DELETE FROM bookmarks
            WHERE NOT EXISTS (
                SELECT 1 FROM comics WHERE comics.id = bookmarks.comic_book_id
            );

            UPDATE bookmarks
            SET sync_id = lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' ||
                substr(hex(randomblob(2)), 2) || '-' ||
                substr('89ab', abs(random()) % 4 + 1, 1) ||
                substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6)))
            WHERE sync_id IS NULL OR sync_id = '';

            UPDATE bookmarks
            SET comic_sync_id = COALESCE((
                    SELECT sync_id FROM comics WHERE comics.id = bookmarks.comic_book_id
                ), comic_book_id)
            WHERE comic_sync_id IS NULL OR comic_sync_id = '';

            UPDATE bookmarks
            SET page_index = 0
            WHERE page_index IS NULL;

            UPDATE bookmarks
            SET updated_at = created_at
            WHERE updated_at IS NULL OR updated_at = '';

            CREATE UNIQUE INDEX IF NOT EXISTS idx_bookmarks_sync_id ON bookmarks(sync_id);
            """);
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
