using Comixa.Core.Models;
using Comixa.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace Comixa.Data.Repositories;

public sealed class SqliteBookmarkRepository : IBookmarkRepository
{
    private readonly ComixaDatabase _database;

    public SqliteBookmarkRepository(ComixaDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<Bookmark>> GetForComicAsync(Guid comicBookId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, comic_book_id, page_number, note, created_at FROM bookmarks WHERE comic_book_id = @id ORDER BY page_number";
        command.Parameters.AddWithValue("@id", comicBookId.ToString());

        var result = new List<Bookmark>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadBookmark(reader));

        return result;
    }

    public async Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO bookmarks (id, comic_book_id, page_number, note, created_at)
            VALUES (@id, @comic_id, @page, @note, @created_at)
            """;
        command.Parameters.AddWithValue("@id", bookmark.Id.ToString());
        command.Parameters.AddWithValue("@comic_id", bookmark.ComicBookId.ToString());
        command.Parameters.AddWithValue("@page", bookmark.PageNumber);
        command.Parameters.AddWithValue("@note", (object?)bookmark.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", bookmark.CreatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bookmarks WHERE id = @id";
        command.Parameters.AddWithValue("@id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteForPageAsync(Guid comicBookId, int pageNumber, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bookmarks WHERE comic_book_id = @comic_id AND page_number = @page";
        command.Parameters.AddWithValue("@comic_id", comicBookId.ToString());
        command.Parameters.AddWithValue("@page", pageNumber);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Bookmark ReadBookmark(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4)));
}
