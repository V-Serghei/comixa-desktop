namespace Comixa.Sync.Contracts;

public interface ISyncContract
{
    SyncAnchor Anchor { get; }

    IReadOnlyList<ComicBookContract> ComicBooks { get; }

    IReadOnlyList<ReadingProgressContract> ReadingProgress { get; }

    IReadOnlyList<BookmarkContract> Bookmarks { get; }

    IReadOnlyList<LibraryFolderContract> LibraryFolders { get; }

    IReadOnlyList<WatchedFolderContract> WatchedFolders { get; }
}
