using Comixa.Core.Models;

namespace Comixa.Core.Tests;

public sealed class ReadingProgressTests
{
    [Fact]
    public void ReadingProgressCanAdvanceUsingRecordCopy()
    {
        var comicBookId = Guid.NewGuid();
        var progress = new ReadingProgress(
            comicBookId,
            "comic-sync-id",
            1,
            20,
            ReadingStatus.InProgress,
            DateTimeOffset.UtcNow);

        var advanced = progress with
        {
            PageIndex = 12,
            UpdatedAt = progress.UpdatedAt.AddMinutes(5)
        };

        Assert.Equal(comicBookId, advanced.ComicBookId);
        Assert.Equal("comic-sync-id", advanced.ComicSyncId);
        Assert.Equal(12, advanced.PageIndex);
        Assert.Equal(20, advanced.TotalPages);
        Assert.Equal(ReadingStatus.InProgress, advanced.Status);
        Assert.True(advanced.UpdatedAt > progress.UpdatedAt);
    }
}
