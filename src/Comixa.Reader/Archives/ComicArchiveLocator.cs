namespace Comixa.Reader.Archives;

public static class ComicArchiveLocator
{
    public static string GetPhysicalArchivePath(string path)
    {
        return path;
    }

    public static string GetDisplayFileName(string path)
    {
        return Path.GetFileName(path);
    }
}
