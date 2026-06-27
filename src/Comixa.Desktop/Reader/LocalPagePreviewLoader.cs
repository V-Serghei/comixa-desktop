using System.IO.Compression;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;

namespace Comixa.Desktop.Reader;

public sealed class LocalPagePreviewLoader : IPagePreviewLoader
{
    public Task<Bitmap?> LoadPageAsync(ComicBook comicBook, int pageIndex, CancellationToken cancellationToken = default)
    {
        return comicBook.Format switch
        {
            ComicFormat.Cbz or ComicFormat.Zip => LoadArchivePageAsync(comicBook.FilePath, pageIndex, cancellationToken),
            ComicFormat.ImageFolder => LoadImageFolderPageAsync(comicBook.FilePath, pageIndex, cancellationToken),
            ComicFormat.Pdf => Task.FromResult<Bitmap?>(null),
            _ => Task.FromResult<Bitmap?>(null)
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

    private static async Task<Bitmap> LoadBitmapAsync(Stream source, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return new Bitmap(memory);
    }
}
