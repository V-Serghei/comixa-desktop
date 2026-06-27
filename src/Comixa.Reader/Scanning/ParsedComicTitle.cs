namespace Comixa.Reader.Scanning;

public sealed record ParsedComicTitle(
    string DisplayTitle,
    string SeriesName,
    int? IssueNumber,
    int? VolumeNumber);
