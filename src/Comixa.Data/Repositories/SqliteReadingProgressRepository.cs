using Comixa.Core.Models;
using Comixa.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace Comixa.Data.Repositories;

public sealed class SqliteReadingProgressRepository : IReadingProgressRepository
{
    private readonly ComixaDatabase _database;

    public SqliteReadingProgressRepository(ComixaDatabase database)
    {
        _database = database;
    }

    public async Task<ReadingProgress?> GetAsync(Guid comicBookId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT comic_book_id, page_number, updated_at FROM reading_progress WHERE comic_book_id = @id";
        command.Parameters.AddWithValue("@id", comicBookId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            return ReadProgress(reader);

        return null;
    }

    public async Task SaveAsync(ReadingProgress progress, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO reading_progress (comic_book_id, page_number, updated_at)
            VALUES (@id, @page, @updated_at)
            ON CONFLICT(comic_book_id) DO UPDATE SET
                page_number = excluded.page_number,
                updated_at  = excluded.updated_at
            """;
        command.Parameters.AddWithValue("@id", progress.ComicBookId.ToString());
        command.Parameters.AddWithValue("@page", progress.PageNumber);
        command.Parameters.AddWithValue("@updated_at", progress.UpdatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReadingProgress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT comic_book_id, page_number, updated_at FROM reading_progress";

        var result = new List<ReadingProgress>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadProgress(reader));

        return result;
    }

    private static ReadingProgress ReadProgress(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetInt32(1),
            DateTimeOffset.Parse(reader.GetString(2)));
}
