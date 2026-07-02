using System.IO.Compression;
using Comixa.Core.Models;
using Comixa.Reader.Scanning;

namespace Comixa.Reader.Tests;

public sealed class LocalComicLibraryScannerTests
{
    [Fact]
    public async Task ScanAsyncFindsLocalComicItems()
    {
        var root = CreateTemporaryFolder();

        try
        {
            var cbzPath = Path.Join(root, "Saga 001.cbz");
            var zipPath = Path.Join(root, "nested", "Batman #23.zip");
            var pdfPath = Path.Join(root, "Akira.pdf");
            var imageFolderPath = Path.Join(root, "Loose Chapter 05");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
            Directory.CreateDirectory(imageFolderPath);

            CreateZip(cbzPath, "001.jpg", "002.png", "003.avif", "notes.txt");
            CreateZip(zipPath, "cover.webp");
            CreateZip(Path.Join(root, "backup.zip"), "notes.txt");
            File.WriteAllText(pdfPath, "%PDF /Type /Page /Type /Page");
            File.WriteAllText(Path.Join(imageFolderPath, "001.jpg"), "fake image");
            File.WriteAllText(Path.Join(imageFolderPath, "002.png"), "fake image");
            File.WriteAllText(Path.Join(root, "ignored.txt"), "not a comic");

            var scanner = new LocalComicLibraryScanner();

            var files = await scanner.ScanAsync(root);

            Assert.Equal(4, files.Count);
            Assert.Contains(files, file => file.Format == ComicFormat.Cbz && file.PageCount == 3);
            Assert.Contains(files, file => file.Format == ComicFormat.Zip && file.PageCount == 1);
            Assert.Contains(files, file => file.Format == ComicFormat.Pdf && file.PageCount == 0);
            Assert.Contains(files, file => file.Format == ComicFormat.ImageFolder && file.PageCount == 2);
            Assert.DoesNotContain(files, file => file.FileName == "backup.zip");
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

    [Fact]
    public async Task ScanAsyncReadsComicInfoMetadataFromArchive()
    {
        var root = CreateTemporaryFolder();

        try
        {
            var cbzPath = Path.Join(root, "release-group-y-the-last-man-v1-ch001.cbz");
            CreateZipWithComicInfo(
                cbzPath,
                """
                <ComicInfo>
                  <Series>Y: The Last Man</Series>
                  <Number>1</Number>
                  <Volume>1</Volume>
                  <Title>Unmanned</Title>
                </ComicInfo>
                """,
                "001.jpg");

            var scanner = new LocalComicLibraryScanner();

            var file = Assert.Single(await scanner.ScanAsync(root));
            var book = file.ToComicBook(DateTimeOffset.UtcNow);

            Assert.Equal("Y: The Last Man Vol. 1 #1 - Unmanned", book.Title);
            Assert.Equal("Y: The Last Man", book.SeriesName);
            Assert.Equal(1, book.IssueNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsyncUsesParentFolderForGenericArchiveNames()
    {
        var root = CreateTemporaryFolder();

        try
        {
            var seriesFolder = Path.Join(root, "y-the-last-man");
            Directory.CreateDirectory(seriesFolder);
            CreateZip(Path.Join(seriesFolder, "001.cbz"), "001.jpg", "002.jpg");

            var scanner = new LocalComicLibraryScanner();

            var file = Assert.Single(await scanner.ScanAsync(root));
            var book = file.ToComicBook(DateTimeOffset.UtcNow);

            Assert.Equal("Y The Last Man #1", book.Title);
            Assert.Equal("Y The Last Man", book.SeriesName);
            Assert.Equal(1, book.IssueNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsyncInfersTitleFromArchivePageNamesWhenArchiveNameIsGeneric()
    {
        var root = CreateTemporaryFolder();

        try
        {
            CreateZip(
                Path.Join(root, "001.cbz"),
                "y_-_the_last_man_01_p01.avif",
                "y_-_the_last_man_01_p02.avif");

            var scanner = new LocalComicLibraryScanner();

            var file = Assert.Single(await scanner.ScanAsync(root));
            var book = file.ToComicBook(DateTimeOffset.UtcNow);

            Assert.Equal("Y The Last Man #1", book.Title);
            Assert.Equal("Y The Last Man", book.SeriesName);
            Assert.Equal(1, book.IssueNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsyncKeepsZipBundlesWithNestedComicArchivesVisible()
    {
        var root = CreateTemporaryFolder();

        try
        {
            var bundlePath = Path.Join(root, "Brightest Day Aftermath - The Search 01-03 (2011).zip");
            CreateZip(bundlePath, "Brightest Day Aftermath - The Search 01 (of 03).cbr");

            var scanner = new LocalComicLibraryScanner();

            var file = Assert.Single(await scanner.ScanAsync(root));
            var book = file.ToComicBook(DateTimeOffset.UtcNow);

            Assert.Equal(ComicFormat.Zip, file.Format);
            Assert.Equal(0, file.PageCount);
            Assert.Equal("Brightest Day Aftermath The Search #1-3", book.Title);
            Assert.Equal("Brightest Day Aftermath The Search", book.SeriesName);
            Assert.Equal(1, book.IssueNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsyncUsesOuterBundleSeriesForGenericNestedChapterNames()
    {
        var root = CreateTemporaryFolder();

        try
        {
            var bundlePath = Path.Join(root, "Brightest Day Aftermath - The Search 01-03 (2011).zip");
            CreateZip(bundlePath, "001.cbr");

            var scanner = new LocalComicLibraryScanner();

            var file = Assert.Single(await scanner.ScanAsync(root));
            var book = file.ToComicBook(DateTimeOffset.UtcNow);

            Assert.Equal("Brightest Day Aftermath The Search #1", book.Title);
            Assert.Equal("Brightest Day Aftermath The Search", book.SeriesName);
            Assert.Equal(1, book.IssueNumber);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private static void CreateZipWithComicInfo(string path, string comicInfoXml, params string[] imageEntries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        var metadataEntry = archive.CreateEntry("ComicInfo.xml");
        using (var stream = metadataEntry.Open())
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(comicInfoXml);
        }

        foreach (var entryName in imageEntries)
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write("content");
        }
    }
}
