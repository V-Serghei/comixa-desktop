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
        foreach (var regex in new[] { IssueHashRegex(), IssueWordRegex(), LeadingBareNumberRegex(), TrailingBareNumberRegex() })
        {
            var match = regex.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var issue))
            {
                continue;
            }

            if (issue is >= 1800 and <= 2099)
            {
                continue;
            }

            return issue;
        }

        return null;
    }

    private static string BuildSeriesName(string text)
    {
        var seriesName = IssueHashRegex().Replace(text, string.Empty);
        seriesName = IssueWordRegex().Replace(seriesName, string.Empty);
        seriesName = LeadingBareNumberRegex().Replace(seriesName, string.Empty);
        seriesName = VolumeRegex().Replace(seriesName, string.Empty);
        seriesName = TrailingBareNumberRegex().Replace(seriesName, string.Empty);
        seriesName = TrailingSeparatorsRegex().Replace(seriesName, string.Empty);
        seriesName = LeadingSeparatorsRegex().Replace(seriesName, string.Empty);
        return CollapseWhitespaceRegex().Replace(seriesName, " ").Trim();
    }

    [GeneratedRegex("""\s*\((?!\d{4}\s*\))([^)]*)\)""")]
    private static partial Regex NoiseParenthesesRegex();

    [GeneratedRegex("""#\s*(\d+)""")]
    private static partial Regex IssueHashRegex();

    [GeneratedRegex("\\b(?:ch(?:apter)?|c|iss(?:ue)?|part|pt|episode|ep|\\u0433\\u043b\\u0430\\u0432\\u0430|\\u0447\\u0430\\u0441\\u0442\\u044c|\\u0442\\u043e\\u043c)[.\\s_-]*(\\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex IssueWordRegex();

    [GeneratedRegex("""\b(?:vol(?:ume)?\.?\s*(\d+)|v(\d+))\b""", RegexOptions.IgnoreCase)]
    private static partial Regex VolumeRegex();

    [GeneratedRegex("""^\s*(\d{1,4})(?:\s*[-.,_ ]\s*|$)""")]
    private static partial Regex LeadingBareNumberRegex();

    [GeneratedRegex("""(?:^|[\s_.-])(\d{1,4})\s*$""")]
    private static partial Regex TrailingBareNumberRegex();

    [GeneratedRegex("""[-\u2013\u2014,;\s]+$""")]
    private static partial Regex TrailingSeparatorsRegex();

    [GeneratedRegex("""^[-\u2013\u2014,;\s]+""")]
    private static partial Regex LeadingSeparatorsRegex();

    [GeneratedRegex("""\s+""")]
    private static partial Regex CollapseWhitespaceRegex();
}
