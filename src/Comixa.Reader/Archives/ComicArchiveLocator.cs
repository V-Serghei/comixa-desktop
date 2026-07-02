namespace Comixa.Reader.Archives;

public static class ComicArchiveLocator
{
    private const string NestedArchiveSeparator = "::";

    public static string CreateNestedArchivePath(string outerArchivePath, string nestedEntryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outerArchivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nestedEntryName);

        return $"{outerArchivePath}{NestedArchiveSeparator}{nestedEntryName.Replace('\\', '/')}";
    }

    public static bool TrySplitNestedArchivePath(string path, out string outerArchivePath, out string nestedEntryName)
    {
        var separatorIndex = path.IndexOf(NestedArchiveSeparator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            outerArchivePath = path;
            nestedEntryName = "";
            return false;
        }

        outerArchivePath = path[..separatorIndex];
        nestedEntryName = path[(separatorIndex + NestedArchiveSeparator.Length)..];
        return !string.IsNullOrWhiteSpace(outerArchivePath) && !string.IsNullOrWhiteSpace(nestedEntryName);
    }

    public static string GetPhysicalArchivePath(string path)
    {
        return TrySplitNestedArchivePath(path, out var outerArchivePath, out _)
            ? outerArchivePath
            : path;
    }

    public static string GetDisplayFileName(string path)
    {
        if (TrySplitNestedArchivePath(path, out _, out var nestedEntryName))
        {
            return Path.GetFileName(nestedEntryName);
        }

        return Path.GetFileName(path);
    }
}
