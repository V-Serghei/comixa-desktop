namespace Comixa.Desktop.ViewModels;

public sealed class LibrarySummaryViewModel : ViewModelBase
{
    private int _comicCount;
    private int _seriesCount;
    private int _folderCount;
    private int _startedCount;
    private int _readCount;
    private int _unreadCount;

    public int ComicCount => _comicCount;
    public int SeriesCount => _seriesCount;
    public int FolderCount => _folderCount;
    public int StartedCount => _startedCount;
    public int ReadCount => _readCount;
    public int UnreadCount => _unreadCount;
    public string StatsLabel => $"{ComicCount} comics - {SeriesCount} series - {FolderCount} folders";
    public string FolderStatsLabel => FolderCount == 1 ? "1 watched folder" : $"{FolderCount} watched folders";

    public void Update(
        int comicCount,
        int seriesCount,
        int folderCount,
        int startedCount,
        int readCount,
        int unreadCount)
    {
        if (_comicCount == comicCount &&
            _seriesCount == seriesCount &&
            _folderCount == folderCount &&
            _startedCount == startedCount &&
            _readCount == readCount &&
            _unreadCount == unreadCount)
        {
            return;
        }

        _comicCount = comicCount;
        _seriesCount = seriesCount;
        _folderCount = folderCount;
        _startedCount = startedCount;
        _readCount = readCount;
        _unreadCount = unreadCount;

        RaisePropertyChanged(nameof(ComicCount));
        RaisePropertyChanged(nameof(SeriesCount));
        RaisePropertyChanged(nameof(FolderCount));
        RaisePropertyChanged(nameof(StartedCount));
        RaisePropertyChanged(nameof(ReadCount));
        RaisePropertyChanged(nameof(UnreadCount));
        RaisePropertyChanged(nameof(StatsLabel));
        RaisePropertyChanged(nameof(FolderStatsLabel));
    }
}
