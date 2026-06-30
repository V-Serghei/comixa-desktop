using System.Collections.Concurrent;
using System.IO.Compression;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;
using ImageMagick;

namespace Comixa.Desktop.Reader;

public sealed class LocalPagePreviewLoader : IPagePreviewLoader, IPageCacheMaintenance, IDisposable
{
    private const int MaxCachedPages = 256;
    private const int HotPreloadForwardPages = 16;
    private const int HotPreloadBackwardPages = 4;
    private const int HotPreloadParallelism = 4;
    private const long MaxCachedPageBytes = 768L * 1024L * 1024L;
    private static readonly TimeSpan PageIdleLifetime = TimeSpan.FromMinutes(7);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(45);

    private static readonly ConcurrentDictionary<string, string[]> ArchiveImageEntryNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string[]> FolderImagePaths = new(StringComparer.OrdinalIgnoreCase);

    private readonly PageBitmapCache _pageCache = new(MaxCachedPages, MaxCachedPageBytes, PageIdleLifetime, CleanupInterval);
    private readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> _pageLoads = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _decodeGate = new(Math.Clamp(Environment.ProcessorCount / 2, 2, 6));
    private readonly SemaphoreSlim _preloadGate = new(1, 1);
    private volatile bool _isDisposed;

    public async Task<Bitmap?> LoadPageAsync(ComicBook comicBook, int pageIndex, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return null;
        }

        if (pageIndex < 0 || pageIndex >= comicBook.PageCount)
        {
            return null;
        }

        var cacheKey = GetPageCacheKey(comicBook, pageIndex);
        if (_pageCache.TryGet(cacheKey, out var cachedPage))
        {
            return cachedPage;
        }

        var load = _pageLoads.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<Bitmap?>>(() => LoadPageUncachedAsync(comicBook, pageIndex, cacheKey, CancellationToken.None)));

        try
        {
            return await load.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (load.IsValueCreated && load.Value.IsCompleted)
            {
                _pageLoads.TryRemove(cacheKey, out _);
            }
        }
    }

    public async Task<IReadOnlyList<Bitmap>> LoadPagesAsync(ComicBook comicBook, CancellationToken cancellationToken = default)
    {
        var pages = new List<Bitmap>(comicBook.PageCount);
        for (var index = 0; index < comicBook.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await LoadPageAsync(comicBook, index, cancellationToken);
            if (page is not null)
            {
                pages.Add(page);
            }
        }

        return pages;
    }

    public async Task PreloadPagesAsync(ComicBook comicBook, int startPageIndex, CancellationToken cancellationToken = default)
    {
        if (comicBook.PageCount <= 0)
        {
            return;
        }

        await _preloadGate.WaitAsync(cancellationToken);
        try
        {
            var hotPages = BuildHotPreloadOrder(comicBook.PageCount, startPageIndex).ToArray();
            await PreloadPageBatchAsync(comicBook, hotPages, cancellationToken);

            if (comicBook.Format is ComicFormat.Cbz or ComicFormat.Zip)
            {
                await PreloadArchivePagesAsync(comicBook, startPageIndex, cancellationToken);
                return;
            }

            foreach (var pageIndex in BuildPreloadOrder(comicBook.PageCount, startPageIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadPageAsync(comicBook, pageIndex, cancellationToken);
            }
        }
        finally
        {
            _preloadGate.Release();
        }
    }

    public void SetActivePageWindow(ComicBook comicBook, int pageIndex, int pageCount)
    {
        if (comicBook.PageCount <= 0)
        {
            _pageCache.PinOnly([]);
            return;
        }

        var start = Math.Max(0, pageIndex - 2);
        var end = Math.Min(comicBook.PageCount - 1, pageIndex + Math.Max(1, pageCount) + HotPreloadForwardPages);
        var cacheKeyPrefix = GetPageCacheKeyPrefix(comicBook);
        var keys = Enumerable
            .Range(start, end - start + 1)
            .Select(index => GetPageCacheKey(cacheKeyPrefix, index));

        _pageCache.PinOnly(keys);
    }

    public void ClearInactivePages()
    {
        _pageCache.TrimExpired();
    }

    public void ClearAllPages()
    {
        _pageLoads.Clear();
        _pageCache.Clear();
        ArchiveImageEntryNames.Clear();
        FolderImagePaths.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _pageLoads.Clear();
        ArchiveImageEntryNames.Clear();
        FolderImagePaths.Clear();
        _pageCache.Dispose();
    }

    private async Task<Bitmap?> LoadPageUncachedAsync(
        ComicBook comicBook,
        int pageIndex,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            await _decodeGate.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = comicBook.Format switch
                {
                    ComicFormat.Cbz or ComicFormat.Zip => await LoadArchivePageAsync(comicBook.FilePath, pageIndex, cancellationToken),
                    ComicFormat.ImageFolder => await LoadImageFolderPageAsync(comicBook.FilePath, pageIndex, cancellationToken),
                    _ => null
                };

                if (page is not null)
                {
                    if (_isDisposed)
                    {
                        page.Dispose();
                        return null;
                    }

                    page = _pageCache.AddOrGet(cacheKey, page);
                }

                return page;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {
                return null;
            }
            finally
            {
                _decodeGate.Release();
            }
        }, cancellationToken);
    }

    private static async Task PreloadPageBatchAsync(
        IPagePreviewLoader loader,
        ComicBook comicBook,
        IReadOnlyCollection<int> pageIndexes,
        CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            pageIndexes,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = HotPreloadParallelism,
                CancellationToken = cancellationToken
            },
            async (pageIndex, token) => await loader.LoadPageAsync(comicBook, pageIndex, token));
    }

    private Task PreloadPageBatchAsync(
        ComicBook comicBook,
        IReadOnlyCollection<int> pageIndexes,
        CancellationToken cancellationToken)
    {
        return PreloadPageBatchAsync(this, comicBook, pageIndexes, cancellationToken);
    }

    private async Task PreloadArchivePagesAsync(ComicBook comicBook, int startPageIndex, CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            try
            {
                var entryNames = GetArchiveImageEntryNames(comicBook.FilePath);
                var cacheKeyPrefix = GetPageCacheKeyPrefix(comicBook);
                using var archive = ZipFile.OpenRead(comicBook.FilePath);

                foreach (var pageIndex in BuildPreloadOrder(comicBook.PageCount, startPageIndex))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cacheKey = GetPageCacheKey(cacheKeyPrefix, pageIndex);
                    if (_pageCache.TryGet(cacheKey, out _))
                    {
                        continue;
                    }

                    var entryName = entryNames.ElementAtOrDefault(pageIndex);
                    if (entryName is null)
                    {
                        continue;
                    }

                    var entry = archive.GetEntry(entryName);
                    if (entry is null)
                    {
                        continue;
                    }

                    await _decodeGate.WaitAsync(cancellationToken);
                    try
                    {
                        if (_pageCache.TryGet(cacheKey, out _))
                        {
                            continue;
                        }

                        await using var entryStream = entry.Open();
                        var page = await LoadBitmapAsync(entryStream, cancellationToken);
                        if (page is null)
                        {
                            continue;
                        }

                        if (_isDisposed)
                        {
                            page.Dispose();
                            return;
                        }

                        _pageCache.AddOrGet(cacheKey, page);
                    }
                    finally
                    {
                        _decodeGate.Release();
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
            {
            }
        }, cancellationToken);
    }

    private static async Task<Bitmap?> LoadArchivePageAsync(string archivePath, int pageIndex, CancellationToken cancellationToken)
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

    private static IEnumerable<int> BuildPreloadOrder(int pageCount, int startPageIndex)
    {
        var start = Math.Clamp(startPageIndex, 0, pageCount - 1);
        yield return start;

        for (var index = start + 1; index < pageCount; index++)
        {
            yield return index;
        }

        for (var index = start - 1; index >= 0; index--)
        {
            yield return index;
        }
    }

    private static IEnumerable<int> BuildHotPreloadOrder(int pageCount, int startPageIndex)
    {
        var start = Math.Clamp(startPageIndex, 0, pageCount - 1);
        yield return start;

        for (var offset = 1; offset <= HotPreloadForwardPages; offset++)
        {
            var pageIndex = start + offset;
            if (pageIndex < pageCount)
            {
                yield return pageIndex;
            }
        }

        for (var offset = 1; offset <= HotPreloadBackwardPages; offset++)
        {
            var pageIndex = start - offset;
            if (pageIndex >= 0)
            {
                yield return pageIndex;
            }
        }
    }

    private static string GetPageCacheKey(ComicBook comicBook, int pageIndex)
    {
        return GetPageCacheKey(GetPageCacheKeyPrefix(comicBook), pageIndex);
    }

    private static string GetPageCacheKeyPrefix(ComicBook comicBook)
    {
        var lastWriteTicks = File.GetLastWriteTimeUtc(comicBook.FilePath).Ticks;
        return $"{comicBook.FilePath}|{lastWriteTicks}";
    }

    private static string GetPageCacheKey(string cacheKeyPrefix, int pageIndex)
    {
        return $"{cacheKeyPrefix}|{pageIndex}";
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
