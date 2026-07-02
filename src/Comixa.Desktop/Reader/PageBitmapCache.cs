using Avalonia.Media.Imaging;

namespace Comixa.Desktop.Reader;

internal sealed class PageBitmapCache : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pinnedKeys = new(StringComparer.Ordinal);
    private readonly List<RetiredBitmap> _retiredBitmaps = [];
    private readonly Timer _cleanupTimer;
    private readonly int _maxEntries;
    private readonly long _maxEstimatedBytes;
    private readonly TimeSpan _idleLifetime;
    private long _estimatedBytes;
    private bool _disposed;
    private static readonly TimeSpan RetiredBitmapLifetime = TimeSpan.FromSeconds(30);

    public PageBitmapCache(int maxEntries, long maxEstimatedBytes, TimeSpan idleLifetime, TimeSpan cleanupInterval)
    {
        _maxEntries = maxEntries;
        _maxEstimatedBytes = maxEstimatedBytes;
        _idleLifetime = idleLifetime;
        _cleanupTimer = new Timer(_ => TrimExpired(), null, cleanupInterval, cleanupInterval);
    }

    public bool TryGet(string key, out Bitmap? bitmap)
    {
        lock (_gate)
        {
            if (!_disposed && _entries.TryGetValue(key, out var entry))
            {
                entry.LastAccessed = DateTimeOffset.UtcNow;
                bitmap = entry.Bitmap;
                return true;
            }
        }

        bitmap = null;
        return false;
    }

    public Bitmap? AddOrGet(string key, Bitmap bitmap)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                bitmap.Dispose();
                return null;
            }

            if (_entries.TryGetValue(key, out var existing))
            {
                existing.LastAccessed = DateTimeOffset.UtcNow;
                if (!ReferenceEquals(existing.Bitmap, bitmap))
                {
                    bitmap.Dispose();
                }

                return existing.Bitmap;
            }

            var entry = new CacheEntry(bitmap, EstimateBytes(bitmap));
            _entries[key] = entry;
            _estimatedBytes += entry.EstimatedBytes;
            TrimLocked(DateTimeOffset.UtcNow, key);
            return bitmap;
        }
    }

    public void PinOnly(IEnumerable<string> keys)
    {
        lock (_gate)
        {
            _pinnedKeys.Clear();
            foreach (var key in keys)
            {
                _pinnedKeys.Add(key);
                if (_entries.TryGetValue(key, out var entry))
                {
                    entry.LastAccessed = DateTimeOffset.UtcNow;
                }
            }

            TrimLocked(DateTimeOffset.UtcNow);
        }
    }

    public void TrimExpired()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            TrimLocked(DateTimeOffset.UtcNow);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
            {
                entry.Bitmap.Dispose();
            }

            foreach (var retired in _retiredBitmaps)
            {
                retired.Bitmap.Dispose();
            }

            _entries.Clear();
            _pinnedKeys.Clear();
            _retiredBitmaps.Clear();
            _estimatedBytes = 0;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _cleanupTimer.Dispose();
        Clear();
    }

    private void TrimLocked(DateTimeOffset now, string? protectedKey = null)
    {
        DisposeRetiredLocked(now);

        var expiredKeys = _entries
            .Where(pair => pair.Key != protectedKey
                && !_pinnedKeys.Contains(pair.Key)
                && now - pair.Value.LastAccessed > _idleLifetime)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var key in expiredKeys)
        {
            RemoveLocked(key);
        }

        if (_entries.Count <= _maxEntries && _estimatedBytes <= _maxEstimatedBytes)
        {
            return;
        }

        var removableKeys = _entries
            .Where(pair => pair.Key != protectedKey && !_pinnedKeys.Contains(pair.Key))
            .OrderBy(pair => pair.Value.LastAccessed)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var key in removableKeys)
        {
            if (_entries.Count <= _maxEntries && _estimatedBytes <= _maxEstimatedBytes)
            {
                break;
            }

            RemoveLocked(key);
        }
    }

    private void RemoveLocked(string key)
    {
        if (!_entries.Remove(key, out var entry))
        {
            return;
        }

        _estimatedBytes -= entry.EstimatedBytes;
        _retiredBitmaps.Add(new RetiredBitmap(entry.Bitmap, DateTimeOffset.UtcNow + RetiredBitmapLifetime));
    }

    private void DisposeRetiredLocked(DateTimeOffset now)
    {
        for (var index = _retiredBitmaps.Count - 1; index >= 0; index--)
        {
            if (_retiredBitmaps[index].DisposeAfter > now)
            {
                continue;
            }

            _retiredBitmaps[index].Bitmap.Dispose();
            _retiredBitmaps.RemoveAt(index);
        }
    }

    private static long EstimateBytes(Bitmap bitmap)
    {
        return Math.Max(1L, bitmap.PixelSize.Width * (long)bitmap.PixelSize.Height * 4L);
    }

    private sealed class CacheEntry(Bitmap bitmap, long estimatedBytes)
    {
        public Bitmap Bitmap { get; } = bitmap;

        public long EstimatedBytes { get; } = estimatedBytes;

        public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed record RetiredBitmap(Bitmap Bitmap, DateTimeOffset DisposeAfter);
}
