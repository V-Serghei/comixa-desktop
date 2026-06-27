namespace Comixa.Data;

public sealed class ComixaDatabase
{
    public ComixaDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }
}
