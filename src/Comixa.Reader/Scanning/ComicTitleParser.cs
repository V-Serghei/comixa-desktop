using System.Text.RegularExpressions;

namespace Comixa.Reader.Scanning;

public static partial class ComicTitleParser
{
    public static ParsedComicTitle Parse(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var raw = Path.GetFileNameWithoutExtension(fileName)
            .Replace('_', ' ')
            .Trim();

        raw = CollapseWhitespaceRegex().Replace(raw, " ");
        var cleaned = NoiseParenthesesRegex().Replace(raw, string.Empty).Trim();

        var volumeNumber = ParseVolume(cleaned);
        var issueNumber = ParseIssue(cleaned);
        var seriesName = BuildSeriesName(cleaned);

        return new ParsedComicTitle(
            cleaned,
            string.IsNullOrWhiteSpace(seriesName) ? cleaned : seriesName,
            issueNumber,
            volumeNumber);
    }

    private static int? ParseVolume(string text)
    {
        var match = VolumeRegex().Match(text);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var volume)
            || int.TryParse(match.Groups[2].Value, out volume)
                ? volume
                : null;
    }

    private static int? ParseIssue(string text)
    {
        var hashMatch = IssueHashRegex().Match(text);
        if (hashMatch.Success && int.TryParse(hashMatch.Groups[1].Value, out var hashIssue))
        {
            return hashIssue;
        }

        var chapterMatch = IssueChapterRegex().Match(text);
        if (chapterMatch.Success && int.TryParse(chapterMatch.Groups[1].Value, out var chapterIssue))
        {
            return chapterIssue;
        }

        var trailingMatch = TrailingBareNumberRegex().Match(text);
        if (trailingMatch.Success && int.TryParse(trailingMatch.Groups[1].Value, out var trailingIssue))
        {
            return trailingIssue is >= 1800 and <= 2099 ? null : trailingIssue;
        }

        return null;
    }

    private static string BuildSeriesName(string text)
    {
        var seriesName = IssueHashRegex().Replace(text, string.Empty);
        seriesName = IssueChapterRegex().Replace(seriesName, string.Empty);
        seriesName = VolumeRegex().Replace(seriesName, string.Empty);
        seriesName = TrailingBareNumberRegex().Replace(seriesName, string.Empty);
        seriesName = TrailingSeparatorsRegex().Replace(seriesName, string.Empty);
        return CollapseWhitespaceRegex().Replace(seriesName, " ").Trim();
    }

    [GeneratedRegex("""\s*\((?!\d{4}\s*\))([^)]*)\)""")]
    private static partial Regex NoiseParenthesesRegex();

    [GeneratedRegex("""#(\d+)""")]
    private static partial Regex IssueHashRegex();

    [GeneratedRegex("""\b(?:ch(?:apter)?|iss(?:ue)?)[.\s]+(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex IssueChapterRegex();

    [GeneratedRegex("""\b(?:vol(?:ume)?\.?\s*(\d+)|v(\d+))\b""", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeRegex();

    [GeneratedRegex("""(?:^|[\s_])(\d{1,4})\s*$""")]
    private static partial Regex TrailingBareNumberRegex();

    [GeneratedRegex("""[-–—,;\s]+$""")]
    private static partial Regex TrailingSeparatorsRegex();

    [GeneratedRegex("""\s+""")]
    private static partial Regex CollapseWhitespaceRegex();
}
