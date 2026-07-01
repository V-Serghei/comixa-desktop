using Comixa.Reader.Scanning;

namespace Comixa.Reader.Tests;

public sealed class ComicTitleParserTests
{
    [Theory]
    [InlineData("Saga 001.cbz", "Saga #1", "Saga", 1)]
    [InlineData("Batman #23 (Digital).cbz", "Batman #23", "Batman", 23)]
    [InlineData("Monstress Vol. 2 Chapter 08.zip", "Monstress Vol. 2 #8", "Monstress", 8)]
    [InlineData("Akira (1988).cbz", "Akira (1988)", "Akira (1988)", null)]
    [InlineData("y-the-last-man v1 ch1 manga.zip", "Y The Last Man Vol. 1 #1", "Y The Last Man", 1)]
    [InlineData("y-the-last-man_v1_ch1_manga-chan.me.zip", "Y The Last Man Vol. 1 #1", "Y The Last Man", 1)]
    [InlineData("y-the-last-man_v1_ch1_manga-chan.me", "Y The Last Man Vol. 1 #1", "Y The Last Man", 1)]
    [InlineData("release-group-y-the-last-man-v1-ch001.cbz", "Y The Last Man Vol. 1 #1", "Y The Last Man", 1)]
    public void ParseExtractsSeriesAndIssueFromCommonNames(
        string fileName,
        string displayTitle,
        string seriesName,
        int? issueNumber)
    {
        var parsed = ComicTitleParser.Parse(fileName);

        Assert.Equal(displayTitle, parsed.DisplayTitle);
        Assert.Equal(seriesName, parsed.SeriesName);
        Assert.Equal(issueNumber, parsed.IssueNumber);
    }

    [Fact]
    public void FromMetadataPrefersComicInfoTitle()
    {
        var parsed = ComicTitleParser.FromMetadata(
            "Unmanned",
            "Y: The Last Man",
            "001",
            "1",
            "ugly_archive_name.cbz");

        Assert.Equal("Y: The Last Man Vol. 1 #1 - Unmanned", parsed.DisplayTitle);
        Assert.Equal("Y: The Last Man", parsed.SeriesName);
        Assert.Equal(1, parsed.IssueNumber);
        Assert.Equal(1, parsed.VolumeNumber);
    }

    [Theory]
    [InlineData("y-the-last-man", "Y The Last Man")]
    [InlineData("y_-_the_last_man", "Y The Last Man")]
    public void NormalizeDisplayNameCleansSlugStyleNames(string rawName, string displayName)
    {
        var normalized = ComicTitleParser.NormalizeDisplayName(rawName);

        Assert.Equal(displayName, normalized);
    }
}
