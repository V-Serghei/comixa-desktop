using Comixa.Core.Models;

namespace Comixa.Core.Tests;

public sealed class BookmarkTests
{
    [Fact]
    public void BookmarkAllowsOptionalNotes()
    {
        var now = DateTimeOffset.UtcNow;
        var bookmark = new Bookmark(
            Guid.NewGuid(),
            "bookmark-sync-id",
            Guid.NewGuid(),
            "comic-sync-id",
            7,
            null,
            now,
            now,
            null);

        Assert.Equal("bookmark-sync-id", bookmark.SyncId);
        Assert.Equal("comic-sync-id", bookmark.ComicSyncId);
        Assert.Equal(7, bookmark.PageIndex);
        Assert.Null(bookmark.Note);
        Assert.Equal(now, bookmark.UpdatedAt);
        Assert.Null(bookmark.DeletedAt);
    }
}
