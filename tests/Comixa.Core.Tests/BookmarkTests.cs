using Comixa.Core.Models;

namespace Comixa.Core.Tests;

public sealed class BookmarkTests
{
    [Fact]
    public void BookmarkAllowsOptionalNotes()
    {
        var bookmark = new Bookmark(
            Guid.NewGuid(),
            Guid.NewGuid(),
            7,
            null,
            DateTimeOffset.UtcNow);

        Assert.Equal(7, bookmark.PageNumber);
        Assert.Null(bookmark.Note);
    }
}
