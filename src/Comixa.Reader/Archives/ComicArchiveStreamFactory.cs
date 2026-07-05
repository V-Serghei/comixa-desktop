namespace Comixa.Reader.Archives;

public static class ComicArchiveStreamFactory
{
    public static Stream? TryOpenArchiveStream(string archivePath)
    {
        try
        {
            return File.OpenRead(archivePath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            return null;
        }
    }
}
