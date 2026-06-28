using Comixa.Core.Models;
using Comixa.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace Comixa.Data.Repositories;

public sealed class SqliteShelfRepository : IShelfRepository
{
    private readonly ComixaDatabase _database;

    public SqliteShelfRepository(ComixaDatabase database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<Shelf>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created_at FROM shelves ORDER BY created_at";

        var result = new List<Shelf>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(ReadShelf(reader));

        return result;
    }

    public async Task<Shelf> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var shelf = new Shelf(Guid.NewGuid(), name, DateTimeOffset.UtcNow);

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO shelves (id, name, created_at) VALUES (@id, @name, @created_at)";
        command.Parameters.AddWithValue("@id", shelf.Id.ToString());
        command.Parameters.AddWithValue("@name", shelf.Name);
        command.Parameters.AddWithValue("@created_at", shelf.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
        return shelf;
    }

    public async Task DeleteAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        await pragma.ExecuteNonQueryAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM shelves WHERE id = @id";
        command.Parameters.AddWithValue("@id", shelfId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetBookIdsAsync(Guid shelfId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT comic_book_id FROM shelf_entries WHERE shelf_id = @shelf_id";
        command.Parameters.AddWithValue("@shelf_id", shelfId.ToString());

        var result = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Guid.Parse(reader.GetString(0)));

        return result;
    }

    public async Task AddBookAsync(Guid shelfId, Guid comicBookId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO shelf_entries (shelf_id, comic_book_id, added_at)
            VALUES (@shelf_id, @book_id, @added_at)
            """;
        command.Parameters.AddWithValue("@shelf_id", shelfId.ToString());
        command.Parameters.AddWithValue("@book_id", comicBookId.ToString());
        command.Parameters.AddWithValue("@added_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveBookAsync(Guid shelfId, Guid comicBookId, CancellationToken cancellationToken = default)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM shelf_entries WHERE shelf_id = @shelf_id AND comic_book_id = @book_id
            """;
        command.Parameters.AddWithValue("@shelf_id", shelfId.ToString());
        command.Parameters.AddWithValue("@book_id", comicBookId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Shelf ReadShelf(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)));
}
