using System.IO.Compression;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.Reader;

public sealed class LocalPagePreviewLoader : IPagePreviewLoader
{
    public async Task<Bitmap?> LoadPageAsync(ComicBook comicBook, int pageIndex, CancellationToken cancellationToken = default)
    {
        try
        {
            return comicBook.Format switch
            {
                ComicFormat.Cbz or ComicFormat.Zip => await LoadArchivePageAsync(comicBook.FilePath, pageIndex, cancellationToken),
                ComicFormat.ImageFolder => await LoadImageFolderPageAsync(comicBook.FilePath, pageIndex, cancellationToken),
                _ => null
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Bitmap>> LoadPagesAsync(ComicBook comicBook, CancellationToken cancellationToken = default)
    {
        return comicBook.Format switch
        {
            ComicFormat.Cbz or ComicFormat.Zip => await LoadArchivePagesAsync(comicBook.FilePath, cancellationToken),
            ComicFormat.ImageFolder => await LoadImageFolderPagesAsync(comicBook.FilePath, cancellationToken),
            _ => []
        };
    }

    private static async Task<Bitmap?> LoadArchivePageAsync(string archivePath, int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                    && LocalComicLibraryScanner.IsImageFile(entry.FullName))
                .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                .ElementAtOrDefault(pageIndex);

            if (entry is null)
            {
                return null;
            }

            await using var entryStream = entry.Open();
            return await LoadBitmapAsync(entryStream, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<Bitmap>> LoadArchivePagesAsync(string archivePath, CancellationToken cancellationToken)
    {
        var pages = new List<Bitmap>();

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                    && LocalComicLibraryScanner.IsImageFile(entry.FullName))
                .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var entryStream = entry.Open();
                var page = await LoadBitmapAsync(entryStream, cancellationToken);
                if (page is not null)
                {
                    pages.Add(page);
                }
            }
        }
        catch (InvalidDataException)
        {
            return [];
        }

        return pages;
    }

    private static async Task<Bitmap?> LoadImageFolderPageAsync(string folderPath, int pageIndex, CancellationToken cancellationToken)
    {
        var imagePath = Directory.EnumerateFiles(folderPath)
            .Where(LocalComicLibraryScanner.IsImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ElementAtOrDefault(pageIndex);

        if (imagePath is null)
        {
            return null;
        }

        await using var stream = File.OpenRead(imagePath);
        return await LoadBitmapAsync(stream, cancellationToken);
    }

    private static async Task<IReadOnlyList<Bitmap>> LoadImageFolderPagesAsync(string folderPath, CancellationToken cancellationToken)
    {
        var pages = new List<Bitmap>();
        var imagePaths = Directory.EnumerateFiles(folderPath)
            .Where(LocalComicLibraryScanner.IsImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var imagePath in imagePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(imagePath);
            var page = await LoadBitmapAsync(stream, cancellationToken);
            if (page is not null)
            {
                pages.Add(page);
            }
        }

        return pages;
    }

    private static async Task<Bitmap?> LoadBitmapAsync(Stream source, CancellationToken cancellationToken)
    {
        try
        {
            using var memory = new MemoryStream();
            await source.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;
            return new Bitmap(memory);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
