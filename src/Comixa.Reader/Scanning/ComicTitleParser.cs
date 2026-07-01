using System.Text.RegularExpressions;

namespace Comixa.Reader.Scanning;

public static partial class ComicTitleParser
{
    public static ParsedComicTitle Parse(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName);
        var raw = !IsKnownComicExtension(extension)
            ? fileName
            : fileName[..^extension.Length];
        raw = raw.Replace('_', ' ').Trim();

        raw = DistributionSuffixRegex().Replace(raw, string.Empty).Trim();
        raw = NormalizeSeparatorStyle(raw);
        raw = CollapseWhitespaceRegex().Replace(raw, " ");
        var cleaned = NoiseParenthesesRegex().Replace(raw, string.Empty).Trim();
        cleaned = NoiseBracketsRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = DistributionPrefixRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = FileNoiseTokenRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = TrailingSeparatorsRegex().Replace(cleaned, string.Empty);
        cleaned = LeadingSeparatorsRegex().Replace(cleaned, string.Empty);
        cleaned = CollapseWhitespaceRegex().Replace(cleaned, " ").Trim();

        var volumeNumber = ParseVolume(cleaned);
        var issueNumber = ParseIssue(cleaned);
        var seriesName = BuildSeriesName(cleaned);
        var displayTitle = BuildDisplayTitle(cleaned, seriesName, issueNumber, volumeNumber);

        return new ParsedComicTitle(
            displayTitle,
            string.IsNullOrWhiteSpace(seriesName) ? NormalizeDisplayName(cleaned) : NormalizeDisplayName(seriesName),
            issueNumber,
            volumeNumber);
    }

    public static ParsedComicTitle FromMetadata(
        string? title,
        string? series,
        string? number,
        string? volume,
        string fallbackFileName)
    {
        var fallback = Parse(fallbackFileName);
        var cleanSeries = CleanMetadataValue(series);
        var cleanTitle = CleanMetadataValue(title);
        var issueNumber = ParseMetadataNumber(number) ?? fallback.IssueNumber;
        var volumeNumber = ParseMetadataNumber(volume) ?? fallback.VolumeNumber;

        if (string.IsNullOrWhiteSpace(cleanSeries) && string.IsNullOrWhiteSpace(cleanTitle))
        {
            return fallback;
        }

        var seriesName = string.IsNullOrWhiteSpace(cleanSeries)
            ? fallback.SeriesName
            : NormalizeDisplayName(cleanSeries);

        var displayTitle = BuildMetadataDisplayTitle(cleanTitle, seriesName, issueNumber, volumeNumber);

        return new ParsedComicTitle(
            displayTitle,
            seriesName,
            issueNumber,
            volumeNumber);
    }

    public static string NormalizeDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value.Replace('_', ' ').Trim();

        cleaned = DistributionSuffixRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = NormalizeSeparatorStyle(cleaned);
        cleaned = NoiseParenthesesRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = NoiseBracketsRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = DistributionPrefixRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = FileNoiseTokenRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = TrailingSeparatorsRegex().Replace(cleaned, string.Empty);
        cleaned = LeadingSeparatorsRegex().Replace(cleaned, string.Empty);
        cleaned = CollapseWhitespaceRegex().Replace(cleaned, " ").Trim();

        return ToDisplayCase(cleaned);
    }

    public static string BuildSeriesDisplayTitle(string seriesName, int? issueNumber, int? volumeNumber)
    {
        var normalizedSeries = NormalizeDisplayName(seriesName);
        if (string.IsNullOrWhiteSpace(normalizedSeries))
        {
            return string.Empty;
        }

        if (issueNumber is null)
        {
            return normalizedSeries;
        }

        var volumeText = volumeNumber is null ? "" : $" Vol. {volumeNumber}";
        return $"{normalizedSeries}{volumeText} #{issueNumber}";
    }

    private static string NormalizeSeparatorStyle(string text)
    {
        var withoutDots = DotBetweenWordsRegex().Replace(text, " ");
        withoutDots = SpacedDashRegex().Replace(withoutDots, " ");

        var normalizedTokens = string.Join(
            ' ',
            withoutDots.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeSlugToken));
        var hyphenCount = normalizedTokens.Count(c => c == '-');
        var spaceCount = normalizedTokens.Count(char.IsWhiteSpace);

        return hyphenCount >= 2 && spaceCount <= 1
            ? normalizedTokens.Replace('-', ' ')
            : normalizedTokens;
    }

    private static string NormalizeSlugToken(string token)
    {
        return token.Count(c => c == '-') >= 2
            ? token.Replace('-', ' ')
            : token;
    }

    private static bool IsKnownComicExtension(string extension)
    {
        return extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cbr", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".7z", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cb7", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".epub", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDisplayTitle(string cleaned, string seriesName, int? issueNumber, int? volumeNumber)
    {
        if (issueNumber is null || string.IsNullOrWhiteSpace(seriesName))
        {
            return ToDisplayCase(cleaned);
        }

        var volumeText = volumeNumber is null ? "" : $" Vol. {volumeNumber}";
        return $"{ToDisplayCase(seriesName)}{volumeText} #{issueNumber}";
    }

    private static string BuildMetadataDisplayTitle(string? title, string seriesName, int? issueNumber, int? volumeNumber)
    {
        var baseTitle = NormalizeDisplayName(seriesName);
        if (volumeNumber is not null)
        {
            baseTitle += $" Vol. {volumeNumber}";
        }

        if (issueNumber is not null)
        {
            baseTitle += $" #{issueNumber}";
        }

        if (!string.IsNullOrWhiteSpace(title) &&
            !title.Equals(seriesName, StringComparison.OrdinalIgnoreCase) &&
            !baseTitle.Contains(title, StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseTitle} - {ToDisplayCase(title)}";
        }

        return baseTitle;
    }

    private static string? CleanMetadataValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return CollapseWhitespaceRegex().Replace(value.Trim(), " ");
    }

    private static int? ParseMetadataNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = MetadataNumberRegex().Match(value);
        return match.Success && int.TryParse(match.Value, out var number)
            ? number
            : null;
    }

    private static string ToDisplayCase(string text)
    {
        if (text.Any(char.IsUpper))
        {
            return text;
        }

        return string.Join(
            ' ',
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 1
                    ? word.ToUpperInvariant()
                    : char.ToUpperInvariant(word[0]) + word[1..]));
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

    [GeneratedRegex("""\s*\[[^\]]+\]""")]
    private static partial Regex NoiseBracketsRegex();

    [GeneratedRegex("""(?:[\s_-]+)(?:[a-z0-9-]+\.)+(?:com|net|org|me|ru|info|io|to|li)\b.*$""", RegexOptions.IgnoreCase)]
    private static partial Regex DistributionSuffixRegex();

    [GeneratedRegex("""^(?:release\s+group|scan\s+group|scanner\s+group)\s+""", RegexOptions.IgnoreCase)]
    private static partial Regex DistributionPrefixRegex();

    [GeneratedRegex("""\b(?:digital|scan|scans|scanner|c2c|web|hd|fixed|noads|manga|russian|rus|english|eng|translation|translated)\b""", RegexOptions.IgnoreCase)]
    private static partial Regex FileNoiseTokenRegex();

    [GeneratedRegex("""(?<=\p{L})\.(?=\p{L})""")]
    private static partial Regex DotBetweenWordsRegex();

    [GeneratedRegex("""\s+[-\u2013\u2014]\s+""")]
    private static partial Regex SpacedDashRegex();

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

    [GeneratedRegex("""\d+""")]
    private static partial Regex MetadataNumberRegex();
}
