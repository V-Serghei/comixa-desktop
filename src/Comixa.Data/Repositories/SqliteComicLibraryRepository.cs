using Comixa.Core.Models;
using Comixa.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace Comixa.Data.Repositories;

public sealed class SqliteComicLibraryRepository : IComicLibraryRepository
{
    private readonly ComixaDatabase _database;

    public SqliteComicLibraryRepository(ComixaDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<ComicBook>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, sync_id, content_fingerprint, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at, updated_at, deleted_at " +
            "FROM comics ORDER BY title ASC";

        var books = new List<ComicBook>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            books.Add(ReadComicBook(reader));

        return books;
    }

    public async Task<ComicBook?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, sync_id, content_fingerprint, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at, updated_at, deleted_at " +
            "FROM comics WHERE id = @id";
        command.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return ReadComicBook(reader);

        return null;
    }

    public async Task UpsertAsync(ComicBook comicBook, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO comics (id, sync_id, content_fingerprint, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at, updated_at, deleted_at)
            VALUES (@id, @sync_id, @content_fingerprint, @title, @series_name, @issue_number, @file_path, @format, @page_count, @cover_path, @added_at, @updated_at, @deleted_at)
            ON CONFLICT(file_path) DO UPDATE SET
                sync_id             = excluded.sync_id,
                content_fingerprint = excluded.content_fingerprint,
                title               = excluded.title,
                series_name         = excluded.series_name,
                issue_number        = excluded.issue_number,
                format              = excluded.format,
                page_count          = CASE
                    WHEN excluded.page_count > 0 THEN excluded.page_count
                    ELSE comics.page_count
                END,
                cover_path          = excluded.cover_path,
                updated_at          = excluded.updated_at,
                deleted_at          = excluded.deleted_at
            """;

        command.Parameters.AddWithValue("@id", comicBook.Id.ToString());
        command.Parameters.AddWithValue("@sync_id", comicBook.SyncId);
        command.Parameters.AddWithValue("@content_fingerprint", comicBook.ContentFingerprint);
        command.Parameters.AddWithValue("@title", comicBook.Title);
        command.Parameters.AddWithValue("@series_name", (object?)comicBook.SeriesName ?? DBNull.Value);
        command.Parameters.AddWithValue("@issue_number", (object?)comicBook.IssueNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@file_path", comicBook.FilePath);
        command.Parameters.AddWithValue("@format", (int)comicBook.Format);
        command.Parameters.AddWithValue("@page_count", comicBook.PageCount);
        command.Parameters.AddWithValue("@cover_path", (object?)comicBook.CoverPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@added_at", comicBook.AddedAt.ToString("O"));
        command.Parameters.AddWithValue("@updated_at", comicBook.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("@deleted_at", comicBook.DeletedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM comics WHERE id = @id";
        command.Parameters.AddWithValue("@id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static ComicBook ReadComicBook(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.GetString(6),
            (ComicFormat)reader.GetInt32(7),
            reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            DateTimeOffset.Parse(reader.GetString(10)),
            DateTimeOffset.Parse(reader.GetString(11)),
            reader.IsDBNull(12) ? null : DateTimeOffset.Parse(reader.GetString(12)));
}
