using System.IO.Compression;
using Comixa.Core.Models;
using Comixa.Reader.Archives;

namespace Comixa.Reader.Scanning;

public sealed class LocalComicLibraryScanner : IComicLibraryScanner
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz",
        ".zip"
    };

    private static readonly Dictionary<string, ComicFormat> DetectedOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ComicFormat.Pdf,
        [".cbr"] = ComicFormat.Cbr,
        [".rar"] = ComicFormat.Rar,
        [".7z"] = ComicFormat.SevenZip,
        [".cb7"] = ComicFormat.SevenZip,
        [".epub"] = ComicFormat.Epub
    };

    private static readonly HashSet<string> NestedComicExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz",
        ".zip",
        ".pdf",
        ".cbr",
        ".rar",
        ".7z",
        ".cb7",
        ".epub"
    };

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

    public async Task<IReadOnlyList<ScannedComicFile>> ScanAsync(string rootFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);

        if (!Directory.Exists(rootFolder))
        {
            return Array.Empty<ScannedComicFile>();
        }

        return await Task.Run(() =>
        {
            var files = new List<ScannedComicFile>();

            foreach (var filePath in EnumerateFiles(rootFolder, cancellationToken))
            {
                foreach (var scannedFile in TryCreateScannedFiles(filePath, cancellationToken))
                {
                    files.Add(scannedFile);
                }
            }

            foreach (var directoryPath in EnumerateDirectories(rootFolder, cancellationToken))
            {
                var imageFolder = TryCreateImageFolder(directoryPath, cancellationToken);
                if (imageFolder is not null)
                {
                    files.Add(imageFolder);
                }
            }

            files.Sort((left, right) => string.Compare(left.FilePath, right.FilePath, StringComparison.OrdinalIgnoreCase));

            return (IReadOnlyList<ScannedComicFile>)files;
        }, cancellationToken);
    }

    public static bool IsImageFile(string path)
    {
        return ImageExtensions.Contains(Path.GetExtension(path));
    }

    private static IEnumerable<ScannedComicFile> TryCreateScannedFiles(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(path);
        var fileInfo = new FileInfo(path);
        if (ArchiveExtensions.Contains(extension))
        {
            var archive = InspectArchive(path);
            if (archive.PageCount > 0)
            {
                yield return new ScannedComicFile(
                    fileInfo.FullName,
                    fileInfo.Name,
                    extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase) ? ComicFormat.Cbz : ComicFormat.Zip,
                    fileInfo.Length,
                    archive.PageCount,
                    ComicArchiveMetadataReader.TryReadTitleMetadata(path, fileInfo.Name));
            }

            foreach (var nestedArchive in archive.NestedComicArchives)
            {
                yield return nestedArchive;
            }

            yield break;
        }

        if (DetectedOnlyExtensions.TryGetValue(extension, out var detectedFormat))
        {
            yield return new ScannedComicFile(
                fileInfo.FullName,
                fileInfo.Name,
                detectedFormat,
                fileInfo.Length,
                0);
        }
    }

    private static ScannedComicFile? TryCreateImageFolder(string directoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var imageFiles = EnumerateDirectFiles(directoryPath)
            .Where(IsImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (imageFiles.Length == 0)
        {
            return null;
        }

        var directoryInfo = new DirectoryInfo(directoryPath);
        var sizeBytes = imageFiles.Sum(path => new FileInfo(path).Length);

        return new ScannedComicFile(
            directoryInfo.FullName,
            directoryInfo.Name,
            ComicFormat.ImageFolder,
            sizeBytes,
            imageFiles.Length);
    }

    private static ArchiveInspection InspectArchive(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var pageCount = 0;
            var nestedComicArchives = new List<ScannedComicFile>();

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                var extension = Path.GetExtension(entry.FullName);
                if (ImageExtensions.Contains(extension))
                {
                    pageCount++;
                }
                else if (NestedComicExtensions.Contains(extension))
                {
                    var nestedFile = TryCreateNestedArchiveFile(archivePath, entry);
                    if (nestedFile is not null)
                    {
                        nestedComicArchives.Add(nestedFile);
                    }
                }
            }

            return new ArchiveInspection(pageCount, nestedComicArchives);
        }
        catch (InvalidDataException)
        {
            return ArchiveInspection.Empty;
        }
        catch (IOException)
        {
            return ArchiveInspection.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return ArchiveInspection.Empty;
        }
    }

    private static ScannedComicFile? TryCreateNestedArchiveFile(string outerArchivePath, ZipArchiveEntry entry)
    {
        var nestedFormat = GetComicFormat(Path.GetExtension(entry.FullName));
        if (nestedFormat is null)
        {
            return null;
        }

        var nestedPath = ComicArchiveLocator.CreateNestedArchivePath(outerArchivePath, entry.FullName);
        return new ScannedComicFile(
            nestedPath,
            Path.GetFileName(entry.FullName),
            nestedFormat.Value,
            entry.Length,
            0,
            ResolveNestedTitleMetadata(outerArchivePath, entry.FullName));
    }

    private static ParsedComicTitle? ResolveNestedTitleMetadata(string outerArchivePath, string nestedEntryName)
    {
        var nestedFileName = Path.GetFileName(nestedEntryName);
        var nestedTitle = ComicTitleParser.Parse(nestedFileName);
        if (!IsWeakNestedSeries(nestedTitle))
        {
            return null;
        }

        var outerTitle = ComicTitleParser.Parse(Path.GetFileName(outerArchivePath));
        if (string.IsNullOrWhiteSpace(outerTitle.SeriesName))
        {
            return null;
        }

        var displayTitle = ComicTitleParser.BuildSeriesDisplayTitle(
            outerTitle.SeriesName,
            nestedTitle.IssueNumber,
            nestedTitle.VolumeNumber);

        return nestedTitle with
        {
            DisplayTitle = string.IsNullOrWhiteSpace(displayTitle) ? nestedTitle.DisplayTitle : displayTitle,
            SeriesName = outerTitle.SeriesName
        };
    }

    private static bool IsWeakNestedSeries(ParsedComicTitle title)
    {
        return title.IssueNumber is not null &&
            (title.SeriesName.Equals(title.DisplayTitle, StringComparison.OrdinalIgnoreCase)
                || int.TryParse(title.SeriesName, out _)
                || title.SeriesName.StartsWith("chapter", StringComparison.OrdinalIgnoreCase)
                || title.SeriesName.StartsWith("ch ", StringComparison.OrdinalIgnoreCase)
                || title.SeriesName.StartsWith("part", StringComparison.OrdinalIgnoreCase)
                || title.SeriesName.StartsWith("pt ", StringComparison.OrdinalIgnoreCase));
    }

    private static ComicFormat? GetComicFormat(string extension)
    {
        if (extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase))
        {
            return ComicFormat.Cbz;
        }

        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ComicFormat.Zip;
        }

        return DetectedOnlyExtensions.TryGetValue(extension, out var format)
            ? format
            : null;
    }

    private static IEnumerable<string> EnumerateFiles(string rootFolder, CancellationToken cancellationToken)
    {
        foreach (var directory in EnumerateDirectories(rootFolder, cancellationToken).Prepend(rootFolder))
        {
            foreach (var file in EnumerateDirectFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string rootFolder, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootFolder);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = pending.Pop();
            foreach (var child in EnumerateDirectDirectories(current))
            {
                if (ShouldSkipDirectory(child))
                {
                    continue;
                }

                yield return child;
                pending.Push(child);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateDirectDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool ShouldSkipDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        return name.StartsWith(".", StringComparison.Ordinal)
            || name.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ArchiveInspection(int PageCount, IReadOnlyList<ScannedComicFile> NestedComicArchives)
    {
        public static ArchiveInspection Empty { get; } = new(0, []);
    }
}
