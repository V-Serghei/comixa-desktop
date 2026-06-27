namespace Comixa.Reader.Scanning;

public interface IComicLibraryScanner
{
    Task<IReadOnlyList<ScannedComicFile>> ScanAsync(string rootFolder, CancellationToken cancellationToken = default);
}
