using System.IO.Compression;
using Comixa.Core.Models;

namespace Comixa.Reader.Scanning;

public sealed class LocalComicLibraryScanner : IComicLibraryScanner
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbz",
        ".zip"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp"
    };

    public Task<IReadOnlyList<ScannedComicFile>> ScanAsync(string rootFolder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);

        if (!Directory.Exists(rootFolder))
        {
            return Task.FromResult<IReadOnlyList<ScannedComicFile>>(Array.Empty<ScannedComicFile>());
        }

        var files = Directory.EnumerateFiles(rootFolder, "*", SearchOption.AllDirectories)
            .Where(IsSupportedArchive)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => CreateScannedFile(path, cancellationToken))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ScannedComicFile>>(files);
    }

    private static bool IsSupportedArchive(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    private static ScannedComicFile CreateScannedFile(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(path);
        var format = Path.GetExtension(path).Equals(".cbz", StringComparison.OrdinalIgnoreCase)
            ? ComicFormat.Cbz
            : ComicFormat.Zip;

        return new ScannedComicFile(
            fileInfo.FullName,
            fileInfo.Name,
            format,
            fileInfo.Length,
            CountImageEntries(path));
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
}
