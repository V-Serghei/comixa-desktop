using System.IO.Compression;
using Comixa.Core.Models;

namespace Comixa.Reader.Scanning;

public sealed class LocalComicLibraryScanner : IComicLibraryScanner
{
    private static readonly Dictionary<string, ComicFormat> SupportedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cbz"] = ComicFormat.Cbz,
        [".zip"] = ComicFormat.Zip,
        [".pdf"] = ComicFormat.Pdf
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".jpe",
        ".jfif",
        ".png",
        ".webp",
        ".avif",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff",
        ".jp2",
        ".j2k",
        ".jpf",
        ".heic",
        ".heif"
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
                var scannedFile = TryCreateScannedFile(filePath, cancellationToken);
                if (scannedFile is not null)
                {
                    files.Add(scannedFile);
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

    private static ScannedComicFile? TryCreateScannedFile(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(path);
        if (!SupportedFileExtensions.TryGetValue(extension, out var format))
        {
            return null;
        }

        var fileInfo = new FileInfo(path);
        if (format is ComicFormat.Cbz or ComicFormat.Zip)
        {
            var pageCount = InspectZipArchive(path);
            if (pageCount <= 0)
            {
                return null;
            }

            return new ScannedComicFile(
                fileInfo.FullName,
                fileInfo.Name,
                format,
                fileInfo.Length,
                pageCount,
                ComicArchiveMetadataReader.TryReadTitleMetadata(path, fileInfo.Name));
        }

        return new ScannedComicFile(
            fileInfo.FullName,
            fileInfo.Name,
            ComicFormat.Pdf,
            fileInfo.Length,
            0);
    }

    private static int InspectZipArchive(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            return archive.Entries.Count(entry =>
                !string.IsNullOrWhiteSpace(entry.Name) &&
                ImageExtensions.Contains(Path.GetExtension(entry.FullName)));
        }
        catch (InvalidDataException)
        {
            return 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
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
}
