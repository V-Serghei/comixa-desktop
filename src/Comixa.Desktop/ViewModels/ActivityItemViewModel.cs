namespace Comixa.Desktop.ViewModels;

public sealed class ActivityItemViewModel(
    ActivityKind kind,
    string title,
    string detail,
    DateTimeOffset createdAt)
{
    public ActivityKind Kind { get; } = kind;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public DateTimeOffset CreatedAt { get; } = createdAt;
    public string TimeLabel => CreatedAt.ToLocalTime().ToString("HH:mm");
    public string KindLabel => Kind.ToString();
    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);
}
