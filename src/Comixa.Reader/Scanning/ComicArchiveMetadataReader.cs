using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Comixa.Reader.Scanning;

internal static partial class ComicArchiveMetadataReader
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".avif",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff"
    };

    public static ParsedComicTitle? TryReadTitleMetadata(string archivePath, string fallbackFileName)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var comicInfoTitle = TryReadComicInfo(archive, fallbackFileName);
            if (comicInfoTitle is not null)
            {
                return comicInfoTitle;
            }

            var fallbackTitle = ComicTitleParser.Parse(fallbackFileName);
            return ShouldInferFromEntries(fallbackTitle)
                ? TryInferFromEntries(archive)
                : null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException)
        {
            return null;
        }
    }

    private static ParsedComicTitle? TryReadComicInfo(ZipArchive archive, string fallbackFileName)
    {
        try
        {
            var entry = archive.Entries.FirstOrDefault(entry =>
                entry.Name.Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return null;
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);

            return ComicTitleParser.FromMetadata(
                ReadElement(document, "Title"),
                ReadElement(document, "Series"),
                ReadElement(document, "Number"),
                ReadElement(document, "Volume"),
                fallbackFileName);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static ParsedComicTitle? TryInferFromEntries(ZipArchive archive)
    {
        return archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                && ImageExtensions.Contains(Path.GetExtension(entry.FullName)))
            .Take(64)
            .Select(entry => ComicTitleParser.Parse(RemovePageSuffix(entry.Name)))
            .Where(IsUsefulEntryTitle)
            .OrderByDescending(ScoreEntryTitle)
            .FirstOrDefault();
    }

    private static string RemovePageSuffix(string entryName)
    {
        var stem = Path.GetFileNameWithoutExtension(entryName)
            .Replace('_', ' ')
            .Trim();

        return PageSuffixRegex().Replace(stem, string.Empty).Trim();
    }

    private static bool ShouldInferFromEntries(ParsedComicTitle fallbackTitle)
    {
        return IsWeakTitle(fallbackTitle)
            || fallbackTitle.IssueNumber is null;
    }

    private static bool IsUsefulEntryTitle(ParsedComicTitle title)
    {
        return title.IssueNumber is not null
            && !IsWeakTitle(title)
            && title.SeriesName.Length > 2;
    }

    private static bool IsWeakTitle(ParsedComicTitle title)
    {
        return title.SeriesName.Equals(title.DisplayTitle, StringComparison.OrdinalIgnoreCase)
            || int.TryParse(title.SeriesName, out _)
            || title.SeriesName.StartsWith("chapter", StringComparison.OrdinalIgnoreCase)
            || title.SeriesName.StartsWith("ch ", StringComparison.OrdinalIgnoreCase)
            || title.SeriesName.StartsWith("part", StringComparison.OrdinalIgnoreCase)
            || title.SeriesName.StartsWith("pt ", StringComparison.OrdinalIgnoreCase)
            || title.SeriesName.StartsWith("page", StringComparison.OrdinalIgnoreCase)
            || title.SeriesName.StartsWith("pg ", StringComparison.OrdinalIgnoreCase)
            || title.SeriesName.StartsWith("p ", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreEntryTitle(ParsedComicTitle title)
    {
        var score = title.SeriesName.Length;
        if (title.VolumeNumber is not null)
        {
            score += 20;
        }

        return score;
    }

    private static string? ReadElement(XDocument document, string name)
    {
        return document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    [GeneratedRegex("""[\s_.-]*(?:p|pg|page|page-)\s*\d{1,4}$""", RegexOptions.IgnoreCase)]
    private static partial Regex PageSuffixRegex();
}
