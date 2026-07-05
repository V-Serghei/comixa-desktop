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
            "SELECT id, sync_id, comic_book_id, comic_sync_id, page_index, note, created_at, updated_at, deleted_at FROM bookmarks WHERE comic_book_id = @id ORDER BY page_index";
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
            INSERT OR IGNORE INTO bookmarks (id, sync_id, comic_book_id, comic_sync_id, page_index, note, created_at, updated_at, deleted_at)
            VALUES (@id, @sync_id, @comic_id, @comic_sync_id, @page_index, @note, @created_at, @updated_at, @deleted_at)
            """;
        command.Parameters.AddWithValue("@id", bookmark.Id.ToString());
        command.Parameters.AddWithValue("@sync_id", bookmark.SyncId);
        command.Parameters.AddWithValue("@comic_id", bookmark.ComicBookId.ToString());
        command.Parameters.AddWithValue("@comic_sync_id", bookmark.ComicSyncId);
        command.Parameters.AddWithValue("@page_index", bookmark.PageIndex);
        command.Parameters.AddWithValue("@note", (object?)bookmark.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("@created_at", bookmark.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@updated_at", bookmark.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("@deleted_at", bookmark.DeletedAt?.ToString("O") ?? (object)DBNull.Value);
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

    public async Task DeleteForPageAsync(Guid comicBookId, int pageIndex, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bookmarks WHERE comic_book_id = @comic_id AND page_index = @page";
        command.Parameters.AddWithValue("@comic_id", comicBookId.ToString());
        command.Parameters.AddWithValue("@page", pageIndex);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Bookmark ReadBookmark(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Guid.Parse(reader.GetString(2)),
            reader.GetString(3),
            reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            DateTimeOffset.Parse(reader.GetString(6)),
            DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)));
}
