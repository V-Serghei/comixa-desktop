using Comixa.Reader.Scanning;

namespace Comixa.Reader.Tests;

public sealed class ComicTitleParserTests
{
    [Theory]
    [InlineData("Saga 001.cbz", "Saga 001", "Saga", 1)]
    [InlineData("Batman #23 (Digital).cbz", "Batman #23", "Batman", 23)]
    [InlineData("Monstress Vol. 2 Chapter 08.zip", "Monstress Vol. 2 Chapter 08", "Monstress", 8)]
    [InlineData("Akira (1988).cbz", "Akira (1988)", "Akira (1988)", null)]
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
}
