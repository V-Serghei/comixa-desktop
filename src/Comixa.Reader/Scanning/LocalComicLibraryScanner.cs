using System.IO.Compression;
using Comixa.Core.Models;
using PDFtoImage;
using PDFtoImage.Exceptions;

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
                var scannedFile = TryCreateScannedFile(filePath, cancellationToken);
                if (scannedFile is not null)
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

    private static ScannedComicFile? TryCreateScannedFile(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(path);
        var fileInfo = new FileInfo(path);
        if (ArchiveExtensions.Contains(extension))
        {
            var pageCount = CountImageEntries(path);
            if (pageCount == 0)
            {
                return null;
            }

            return new ScannedComicFile(
                fileInfo.FullName,
                fileInfo.Name,
                extension.Equals(".cbz", StringComparison.OrdinalIgnoreCase) ? ComicFormat.Cbz : ComicFormat.Zip,
                fileInfo.Length,
                pageCount,
                ComicArchiveMetadataReader.TryReadTitleMetadata(path, fileInfo.Name));
        }

        if (DetectedOnlyExtensions.TryGetValue(extension, out var detectedFormat))
        {
            return new ScannedComicFile(
                fileInfo.FullName,
                fileInfo.Name,
                detectedFormat,
                fileInfo.Length,
                detectedFormat == ComicFormat.Pdf ? CountPdfPages(path) : 0);
        }

        return null;
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

    private static int CountImageEntries(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);

            return archive.Entries.Count(entry =>
                !string.IsNullOrWhiteSpace(entry.Name)
                && ImageExtensions.Contains(Path.GetExtension(entry.FullName)));
        }
        catch (InvalidDataException)
        {
            return 0;
        }
    }

    private static int CountPdfPages(string pdfPath)
    {
        try
        {
            using var stream = File.OpenRead(pdfPath);
            return Math.Max(0, Conversion.GetPageCount(stream));
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
        catch (ArgumentException)
        {
            return 0;
        }
        catch (PdfException)
        {
            return 0;
        }
        catch (DllNotFoundException)
        {
            return 0;
        }
        catch (BadImageFormatException)
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
