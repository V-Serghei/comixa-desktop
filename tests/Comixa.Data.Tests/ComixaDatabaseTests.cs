using Comixa.Data;

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
}
