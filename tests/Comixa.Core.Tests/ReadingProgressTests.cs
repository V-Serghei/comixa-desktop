using Comixa.Core.Models;

namespace Comixa.Core.Tests;

public sealed class ReadingProgressTests
{
    [Fact]
    public void ReadingProgressCanAdvanceUsingRecordCopy()
    {
        var comicBookId = Guid.NewGuid();
        var progress = new ReadingProgress(comicBookId, 1, DateTimeOffset.UtcNow);

        var advanced = progress with
        {
            PageNumber = 12,
            UpdatedAt = progress.UpdatedAt.AddMinutes(5)
        };

        Assert.Equal(comicBookId, advanced.ComicBookId);
        Assert.Equal(12, advanced.PageNumber);
        Assert.True(advanced.UpdatedAt > progress.UpdatedAt);
    }
}
