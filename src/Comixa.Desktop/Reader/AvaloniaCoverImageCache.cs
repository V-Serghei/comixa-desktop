using Avalonia;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;

namespace Comixa.Desktop.Reader;

public sealed class AvaloniaCoverImageCache : ICoverImageCache
{
    private readonly IPagePreviewLoader _pagePreviewLoader;
    private readonly string _cacheDirectory;

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
        Directory.CreateDirectory(_cacheDirectory);

        var cacheFile = Path.Combine(_cacheDirectory, $"{comicBook.Id}.png");
        if (File.Exists(cacheFile))
        {
            try
            {
                return await Task.Run(() => new Bitmap(cacheFile), cancellationToken);
            }
            catch
            {
                // Corrupt cache files are regenerated from the source page.
            }
        }

        var fullPage = await _pagePreviewLoader.LoadPageAsync(comicBook, 0, cancellationToken);
        if (fullPage is null)
        {
            return null;
        }

        var thumbnail = await Task.Run(() => CreateThumbnail(fullPage, 200, 300), cancellationToken);
        if (!ReferenceEquals(thumbnail, fullPage))
        {
            fullPage.Dispose();
        }

        try
        {
            await Task.Run(() => thumbnail.Save(cacheFile), cancellationToken);
        }
        catch
        {
            // Cover cache writes are best-effort; reading should still work.
        }

        return thumbnail;
    }

    private static Bitmap CreateThumbnail(Bitmap source, int maxWidth, int maxHeight)
    {
        if (source.PixelSize.Width <= maxWidth && source.PixelSize.Height <= maxHeight)
        {
            return source;
        }

        var scaleX = (double)maxWidth / source.PixelSize.Width;
        var scaleY = (double)maxHeight / source.PixelSize.Height;
        var scale = Math.Min(scaleX, scaleY);

        var newWidth = Math.Max(1, (int)(source.PixelSize.Width * scale));
        var newHeight = Math.Max(1, (int)(source.PixelSize.Height * scale));

        return source.CreateScaledBitmap(
            new PixelSize(newWidth, newHeight),
            BitmapInterpolationMode.LowQuality);
    }
}
