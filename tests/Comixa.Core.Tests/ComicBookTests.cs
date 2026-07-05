using Comixa.Core.Models;

namespace Comixa.Core.Tests;

public sealed class ComicBookTests
{
    [Fact]
    public void ComicBookStoresLibraryMetadata()
    {
        var id = Guid.NewGuid();
        var addedAt = DateTimeOffset.UtcNow;

        var comicBook = new ComicBook(
            id,
            "comic-sync-id",
            "sha256:abc123",
            "A Quiet Chapter",
            "A Quiet Chapter",
            3,
            Path.Join("library", "quiet-chapter.cbz"),
            ComicFormat.Cbz,
            42,
            null,
            addedAt,
            addedAt,
            null);

        Assert.Equal(id, comicBook.Id);
        Assert.Equal("comic-sync-id", comicBook.SyncId);
        Assert.Equal("sha256:abc123", comicBook.ContentFingerprint);
        Assert.Equal("A Quiet Chapter", comicBook.Title);
        Assert.Equal("A Quiet Chapter", comicBook.SeriesName);
        Assert.Equal(3, comicBook.IssueNumber);
        Assert.Equal(ComicFormat.Cbz, comicBook.Format);
        Assert.Equal(42, comicBook.PageCount);
        Assert.Null(comicBook.CoverPath);
        Assert.Equal(addedAt, comicBook.AddedAt);
        Assert.Equal(addedAt, comicBook.UpdatedAt);
        Assert.Null(comicBook.DeletedAt);
    }
}
