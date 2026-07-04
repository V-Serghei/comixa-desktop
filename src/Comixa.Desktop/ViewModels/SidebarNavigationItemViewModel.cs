using Avalonia.Media;

namespace Comixa.Desktop.ViewModels;

public sealed class SidebarNavigationItemViewModel : ViewModelBase
{
    private bool _isActive;
    private string _detail;

    public SidebarNavigationItemViewModel(string label, string detail, string iconData, RelayCommand command)
    {
        Label = label;
        _detail = detail;
        Icon = StreamGeometry.Parse(iconData);
        Command = command;
    }

    public string Label { get; }
    public Geometry Icon { get; }
    public RelayCommand Command { get; }
    public string ToolTip => HasDetail ? $"{Label} - {Detail}" : Label;

    public string Detail
    {
        get => _detail;
        private set
        {
            if (_detail == value) return;
            _detail = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasDetail));
            RaisePropertyChanged(nameof(ToolTip));
        }
    }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (_isActive == value) return;
            _isActive = value;
            RaisePropertyChanged();
        }
    }

    public void SetState(bool isActive, string detail)
    {
        IsActive = isActive;
        Detail = detail;
    }
}
