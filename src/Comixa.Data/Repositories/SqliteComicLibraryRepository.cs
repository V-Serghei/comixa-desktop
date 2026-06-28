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
            "SELECT id, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at " +
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
            "SELECT id, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at " +
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
            INSERT INTO comics (id, title, series_name, issue_number, file_path, format, page_count, cover_path, added_at)
            VALUES (@id, @title, @series_name, @issue_number, @file_path, @format, @page_count, @cover_path, @added_at)
            ON CONFLICT(file_path) DO UPDATE SET
                title        = excluded.title,
                series_name  = excluded.series_name,
                issue_number = excluded.issue_number,
                format       = excluded.format,
                page_count   = excluded.page_count,
                cover_path   = excluded.cover_path
            """;

        command.Parameters.AddWithValue("@id", comicBook.Id.ToString());
        command.Parameters.AddWithValue("@title", comicBook.Title);
        command.Parameters.AddWithValue("@series_name", (object?)comicBook.SeriesName ?? DBNull.Value);
        command.Parameters.AddWithValue("@issue_number", (object?)comicBook.IssueNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("@file_path", comicBook.FilePath);
        command.Parameters.AddWithValue("@format", (int)comicBook.Format);
        command.Parameters.AddWithValue("@page_count", comicBook.PageCount);
        command.Parameters.AddWithValue("@cover_path", (object?)comicBook.CoverPath ?? DBNull.Value);
        command.Parameters.AddWithValue("@added_at", comicBook.AddedAt.ToString("O"));

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
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.GetString(4),
            (ComicFormat)reader.GetInt32(5),
            reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            DateTimeOffset.Parse(reader.GetString(8)));
}
