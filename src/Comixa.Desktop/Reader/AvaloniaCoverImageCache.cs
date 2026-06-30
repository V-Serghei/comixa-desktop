using System.Collections.Concurrent;
using System.IO.Compression;
using Avalonia;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;
using ImageMagick;

namespace Comixa.Desktop.Reader;

public sealed class AvaloniaCoverImageCache : ICoverImageCache, IDisposable
{
    private const int CoverMaxWidth = 220;
    private const int CoverMaxHeight = 330;

    private readonly IPagePreviewLoader _pagePreviewLoader;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, Lazy<Task<Bitmap?>>> _memoryCache = new(StringComparer.Ordinal);

    public AvaloniaCoverImageCache(IPagePreviewLoader pagePreviewLoader)
    {
        _pagePreviewLoader = pagePreviewLoader;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Comixa",
            "Desktop",
            "covers");
    }

    public async Task<Bitmap?> LoadCoverAsync(ComicBook comicBook, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCoverCacheKey(comicBook);
        var load = _memoryCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<Bitmap?>>(() => LoadCoverCoreAsync(comicBook, cacheKey, CancellationToken.None)));

        try
        {
            var cover = await load.Value.WaitAsync(cancellationToken);
            if (cover is null)
            {
                _memoryCache.TryRemove(cacheKey, out _);
            }

            return cover;
        }
        catch
        {
            if (load.IsValueCreated && load.Value.IsFaulted)
            {
                _memoryCache.TryRemove(cacheKey, out _);
            }

            throw;
        }
    }

    public void Dispose()
    {
        foreach (var load in _memoryCache.Values)
        {
            if (load.IsValueCreated &&
                load.Value.IsCompletedSuccessfully &&
                load.Value.Result is not null)
            {
                load.Value.Result.Dispose();
            }
        }

        _memoryCache.Clear();
    }

    private async Task<Bitmap?> LoadCoverCoreAsync(
        ComicBook comicBook,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);

        var cacheFile = Path.Combine(_cacheDirectory, $"{cacheKey}.png");
        if (File.Exists(cacheFile))
        {
            try
            {
                return await Task.Run(() => new Bitmap(cacheFile), cancellationToken);
            }
            catch
            {
                TryDelete(cacheFile);
            }
        }

        DeleteOldCoverFiles(comicBook.Id, cacheFile);

        var thumbnail = await TryCreateThumbnailFromSourceAsync(comicBook, cancellationToken);
        if (thumbnail is null)
        {
            thumbnail = await TryCreateThumbnailFromPageLoaderAsync(comicBook, cancellationToken);
        }

        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            await Task.Run(() => thumbnail.Save(cacheFile), cancellationToken);
        }
        catch
        {
        }

        return thumbnail;
    }

    private async Task<Bitmap?> TryCreateThumbnailFromSourceAsync(
        ComicBook comicBook,
        CancellationToken cancellationToken)
    {
        try
        {
            return comicBook.Format switch
            {
                ComicFormat.Cbz or ComicFormat.Zip => await TryCreateArchiveThumbnailAsync(comicBook.FilePath, cancellationToken),
                ComicFormat.ImageFolder => await TryCreateImageFolderThumbnailAsync(comicBook.FilePath, cancellationToken),
                _ => null
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or MagickException)
        {
            return null;
        }
    }

    private static async Task<Bitmap?> TryCreateArchiveThumbnailAsync(string archivePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var entry = archive.Entries
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && LocalComicLibraryScanner.IsImageFile(item.FullName))
            .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (entry is null)
        {
            return null;
        }

        await using var entryStream = entry.Open();
        return await CreateMagickThumbnailAsync(entryStream, cancellationToken);
    }

    private static async Task<Bitmap?> TryCreateImageFolderThumbnailAsync(string folderPath, CancellationToken cancellationToken)
    {
        var imagePath = Directory
            .EnumerateFiles(folderPath)
            .Where(LocalComicLibraryScanner.IsImageFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (imagePath is null)
        {
            return null;
        }

        await using var stream = File.OpenRead(imagePath);
        return await CreateMagickThumbnailAsync(stream, cancellationToken);
    }

    private static async Task<Bitmap?> CreateMagickThumbnailAsync(Stream source, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);

        using var image = new MagickImage(memory.ToArray());
        image.AutoOrient();
        image.Thumbnail(CoverMaxWidth, CoverMaxHeight);
        image.Format = MagickFormat.Png;
        return new Bitmap(new MemoryStream(image.ToByteArray()));
    }

    private async Task<Bitmap?> TryCreateThumbnailFromPageLoaderAsync(
        ComicBook comicBook,
        CancellationToken cancellationToken)
    {
        var fullPage = await _pagePreviewLoader.LoadPageAsync(comicBook, 0, cancellationToken);
        if (fullPage is null)
        {
            return null;
        }

        return await Task.Run(() => CreateAvaloniaThumbnail(fullPage), cancellationToken);
    }

    private static Bitmap CreateAvaloniaThumbnail(Bitmap source)
    {
        if (source.PixelSize.Width <= CoverMaxWidth && source.PixelSize.Height <= CoverMaxHeight)
        {
            return source.CreateScaledBitmap(source.PixelSize, BitmapInterpolationMode.LowQuality);
        }

        var scaleX = (double)CoverMaxWidth / source.PixelSize.Width;
        var scaleY = (double)CoverMaxHeight / source.PixelSize.Height;
        var scale = Math.Min(scaleX, scaleY);

        var newWidth = Math.Max(1, (int)(source.PixelSize.Width * scale));
        var newHeight = Math.Max(1, (int)(source.PixelSize.Height * scale));

        return source.CreateScaledBitmap(
            new PixelSize(newWidth, newHeight),
            BitmapInterpolationMode.LowQuality);
    }

    private string GetCoverCacheKey(ComicBook comicBook)
    {
        return $"{comicBook.Id:N}-{GetSourceStamp(comicBook.FilePath)}";
    }

    private static string GetSourceStamp(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var file = new FileInfo(path);
                return $"{file.LastWriteTimeUtc.Ticks:x}-{file.Length:x}";
            }

            if (Directory.Exists(path))
            {
                return Directory.GetLastWriteTimeUtc(path).Ticks.ToString("x");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return "unknown";
    }

    private void DeleteOldCoverFiles(Guid comicBookId, string keepFile)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDirectory, $"{comicBookId:N}-*.png"))
            {
                if (!file.Equals(keepFile, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(file);
                }
            }

            TryDelete(Path.Combine(_cacheDirectory, $"{comicBookId}.png"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
