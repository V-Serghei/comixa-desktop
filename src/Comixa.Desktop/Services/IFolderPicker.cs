namespace Comixa.Desktop.Services;

public interface IFolderPicker
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}
