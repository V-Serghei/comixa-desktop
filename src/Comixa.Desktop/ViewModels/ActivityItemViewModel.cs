namespace Comixa.Desktop.ViewModels;

public sealed class ActivityItemViewModel(
    string title,
    string detail,
    DateTimeOffset createdAt)
{
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public string TimeLabel => CreatedAt.ToLocalTime().ToString("HH:mm");
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}
