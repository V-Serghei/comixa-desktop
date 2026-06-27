using System.IO.Compression;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;

namespace Comixa.Reader.Tests;

public sealed class LocalComicLibraryScannerTests
{
    [Fact]
    public async Task ScanAsyncFindsCbzAndZipArchives()
    {
        var root = CreateTemporaryFolder();

        try
        {
            var cbzPath = Path.Join(root, "Saga 001.cbz");
            var zipPath = Path.Join(root, "nested", "Batman #23.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

            CreateZip(cbzPath, "001.jpg", "002.png", "notes.txt");
            CreateZip(zipPath, "cover.webp");
            File.WriteAllText(Path.Join(root, "ignored.txt"), "not a comic");

            var scanner = new LocalComicLibraryScanner();

            var files = await scanner.ScanAsync(root);

            Assert.Equal(2, files.Count);
            Assert.Contains(files, file => file.Format == ComicFormat.Cbz && file.PageCount == 2);
            Assert.Contains(files, file => file.Format == ComicFormat.Zip && file.PageCount == 1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsyncReturnsEmptyListForMissingFolder()
    {
        var scanner = new LocalComicLibraryScanner();

        var files = await scanner.ScanAsync(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Empty(files);
    }

    private static string CreateTemporaryFolder()
    {
        var path = Path.Join(Path.GetTempPath(), "comixa-reader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateZip(string path, params string[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var entryName in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write("content");
        }
    }
}
