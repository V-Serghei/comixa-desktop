using Comixa.Core.Models;

namespace Comixa.Reader.Scanning;

public sealed record ScannedComicFile(
    string FilePath,
    string FileName,
    ComicFormat Format,
    long SizeBytes,
    int PageCount)
{
    public ComicBook ToComicBook(DateTimeOffset addedAt)
    {
        var parsed = ComicTitleParser.Parse(FileName);
        var seriesName = ResolveSeriesName(parsed, FilePath);

        return new ComicBook(
            Guid.NewGuid(),
            parsed.DisplayTitle,
            seriesName,
            parsed.IssueNumber,
            FilePath,
            Format,
            PageCount,
            null,
            addedAt);
    }

    private static string? ResolveSeriesName(ParsedComicTitle parsed, string filePath)
    {
        if (!ShouldPreferParentFolder(parsed))
        {
            return parsed.SeriesName;
        }

        var parent = Directory.GetParent(filePath);
        if (parent is null)
        {
            return parsed.SeriesName;
        }

        return string.IsNullOrWhiteSpace(parent.Name)
            ? parsed.SeriesName
            : parent.Name;
    }

    private static bool ShouldPreferParentFolder(ParsedComicTitle parsed)
    {
        if (parsed.IssueNumber is null)
        {
            return false;
        }

        return parsed.SeriesName.Equals(parsed.DisplayTitle, StringComparison.OrdinalIgnoreCase)
            || StartsWithIssueNumber(parsed.DisplayTitle, parsed.IssueNumber.Value)
            || int.TryParse(parsed.SeriesName, out _)
            || parsed.SeriesName.StartsWith("chapter", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("ch ", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("part", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("pt ", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("episode", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("ep ", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("\u0433\u043b\u0430\u0432\u0430", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("\u0447\u0430\u0441\u0442\u044c", StringComparison.OrdinalIgnoreCase)
            || parsed.SeriesName.StartsWith("\u0442\u043e\u043c", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithIssueNumber(string title, int issueNumber)
    {
        var trimmed = title.TrimStart();
        for (var width = 1; width <= 4; width++)
        {
            var value = issueNumber.ToString($"D{width}");
            if (!trimmed.StartsWith(value, StringComparison.Ordinal))
            {
                continue;
            }

            return trimmed.Length == value.Length || IsIssueSeparator(trimmed[value.Length]);
        }

        return false;
    }

    private static bool IsIssueSeparator(char value)
    {
        return char.IsWhiteSpace(value) || value is '-' or '_' or '.' or ',';
    }
}
