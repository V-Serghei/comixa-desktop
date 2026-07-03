namespace Comixa.Desktop.ViewModels;

public sealed class SidebarNavigationItemViewModel : ViewModelBase
{
    private bool _isActive;
    private string _detail;

    public SidebarNavigationItemViewModel(string label, string detail, RelayCommand command)
    {
        Label = label;
        _detail = detail;
        Command = command;
    }

    public string Label { get; }
    public RelayCommand Command { get; }

    public string Detail
    {
        get => _detail;
        private set
        {
            if (_detail == value) return;
            _detail = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasDetail));
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
