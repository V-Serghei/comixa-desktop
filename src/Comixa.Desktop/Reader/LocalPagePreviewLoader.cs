using System.Collections.Concurrent;
using System.IO.Compression;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;
using ImageMagick;

namespace Comixa.Desktop.Reader;

public sealed class LocalPagePreviewLoader : IPagePreviewLoader
{
    private static readonly ConcurrentDictionary<string, string[]> ArchiveImageEntryNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string[]> FolderImagePaths = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Bitmap> PageCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> PageLoads = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> PageCacheOrder = new();
    private const int MaxCachedPages = 32;

    public async Task<Bitmap?> LoadPageAsync(ComicBook comicBook, int pageIndex, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetPageCacheKey(comicBook, pageIndex);
        if (PageCache.TryGetValue(cacheKey, out var cachedPage))
        {
            return cachedPage;
        }

        var load = PageLoads.GetOrAdd(cacheKey, _ => new Lazy<Task<Bitmap?>>(() => LoadPageUncachedAsync(comicBook, pageIndex, cacheKey)));

        try
        {
            return await load.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (load.IsValueCreated && load.Value.IsCompleted)
            {
                PageLoads.TryRemove(cacheKey, out _);
            }
        }
    }

    private static async Task<Bitmap?> LoadPageUncachedAsync(ComicBook comicBook, int pageIndex, string cacheKey)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var page = comicBook.Format switch
                {
                    ComicFormat.Cbz or ComicFormat.Zip => await LoadArchivePageAsync(comicBook.FilePath, pageIndex, CancellationToken.None),
                    ComicFormat.ImageFolder => await LoadImageFolderPageAsync(comicBook.FilePath, pageIndex, CancellationToken.None),
                    _ => null
                };

                if (page is not null)
                {
                    CachePage(cacheKey, page);
                }

                return page;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        });
    }

    public async Task<IReadOnlyList<Bitmap>> LoadPagesAsync(ComicBook comicBook, CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            return comicBook.Format switch
            {
                ComicFormat.Cbz or ComicFormat.Zip => await LoadArchivePagesAsync(comicBook.FilePath, cancellationToken),
                ComicFormat.ImageFolder => await LoadImageFolderPagesAsync(comicBook.FilePath, cancellationToken),
                _ => []
            };
        }, cancellationToken);
    }

    private static async Task<Bitmap?> LoadArchivePageAsync(string archivePath, int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            var entryName = GetArchiveImageEntryNames(archivePath).ElementAtOrDefault(pageIndex);

            if (entryName is null)
            {
                return null;
            }

            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry(entryName);
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
            var entryNames = GetArchiveImageEntryNames(archivePath);
            using var archive = ZipFile.OpenRead(archivePath);

            foreach (var entryName in entryNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = archive.GetEntry(entryName);
                if (entry is null)
                {
                    continue;
                }

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
        var imagePath = GetFolderImagePaths(folderPath).ElementAtOrDefault(pageIndex);

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
        var imagePaths = GetFolderImagePaths(folderPath);

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

    private static string[] GetArchiveImageEntryNames(string archivePath)
    {
        return ArchiveImageEntryNames.GetOrAdd(archivePath, path =>
        {
            using var archive = ZipFile.OpenRead(path);
            return archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                    && LocalComicLibraryScanner.IsImageFile(entry.FullName))
                .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.FullName)
                .ToArray();
        });
    }

    private static string[] GetFolderImagePaths(string folderPath)
    {
        return FolderImagePaths.GetOrAdd(folderPath, path =>
            Directory.EnumerateFiles(path)
                .Where(LocalComicLibraryScanner.IsImageFile)
                .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static string GetPageCacheKey(ComicBook comicBook, int pageIndex)
    {
        var lastWriteTicks = File.GetLastWriteTimeUtc(comicBook.FilePath).Ticks;
        return $"{comicBook.FilePath}|{lastWriteTicks}|{pageIndex}";
    }

    private static void CachePage(string cacheKey, Bitmap page)
    {
        if (!PageCache.TryAdd(cacheKey, page))
        {
            return;
        }

        PageCacheOrder.Enqueue(cacheKey);
        while (PageCache.Count > MaxCachedPages && PageCacheOrder.TryDequeue(out var oldKey))
        {
            PageCache.TryRemove(oldKey, out _);
        }
    }

    private static async Task<Bitmap?> LoadBitmapAsync(Stream source, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();

        try
        {
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            try
            {
                using var image = new MagickImage(bytes);
                image.Format = MagickFormat.Png;
                return new Bitmap(new MemoryStream(image.ToByteArray()));
            }
            catch (MagickException)
            {
                return null;
            }
        }
    }
}
