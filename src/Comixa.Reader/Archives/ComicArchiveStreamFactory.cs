using System.IO.Compression;

namespace Comixa.Reader.Archives;

public static class ComicArchiveStreamFactory
{
    private static readonly byte[] RarSignature = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07];

    public static Stream? TryOpenArchiveStream(string archivePath)
    {
        try
        {
            if (!ComicArchiveLocator.TrySplitNestedArchivePath(archivePath, out var outerArchivePath, out var nestedEntryName))
            {
                return File.OpenRead(archivePath);
            }

            using var outerArchive = ZipFile.OpenRead(outerArchivePath);
            var nestedEntry = outerArchive.GetEntry(nestedEntryName);
            if (nestedEntry is null)
            {
                return null;
            }

            var memory = new MemoryStream((int)Math.Min(nestedEntry.Length, int.MaxValue));
            using (var nestedStream = nestedEntry.Open())
            {
                nestedStream.CopyTo(memory);
            }

            memory.Position = 0;
            return memory;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException)
        {
            return null;
        }
    }

    public static Stream? TryOpenRarArchiveStream(string archivePath)
    {
        var stream = TryOpenArchiveStream(archivePath);
        if (stream is null)
        {
            return null;
        }

        if (TryMoveToRarSignature(stream))
        {
            return stream;
        }

        stream.Dispose();
        return null;
    }

    private static bool TryMoveToRarSignature(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return true;
        }

        var start = stream.Position;
        Span<byte> header = stackalloc byte[RarSignature.Length];
        var read = stream.Read(header);
        stream.Position = start;
        if (read == RarSignature.Length && header.SequenceEqual(RarSignature))
        {
            return true;
        }

        var bufferLength = (int)Math.Min(stream.Length - start, 1024L * 1024L);
        var buffer = new byte[bufferLength];
        read = stream.Read(buffer, 0, buffer.Length);
        for (var index = 0; index <= read - RarSignature.Length; index++)
        {
            if (!buffer.AsSpan(index, RarSignature.Length).SequenceEqual(RarSignature))
            {
                continue;
            }

            stream.Position = start + index;
            return true;
        }

        stream.Position = start;
        return false;
    }
}
