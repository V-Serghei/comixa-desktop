using Comixa.Core.Models;

namespace Comixa.Desktop.ViewModels;

public sealed class ShelfViewModel : ViewModelBase
{
    private bool _isActive;

    public ShelfViewModel(Shelf shelf, Action<Guid> onSelect, Action<Guid> onDelete)
    {
        Id = shelf.Id;
        Name = shelf.Name;
        SelectCommand = new RelayCommand(() => onSelect(Id));
        DeleteCommand = new RelayCommand(() => onDelete(Id));
    }

    public Guid Id { get; }
    public string Name { get; }
    public RelayCommand SelectCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            RaisePropertyChanged();
        }
    }
}
