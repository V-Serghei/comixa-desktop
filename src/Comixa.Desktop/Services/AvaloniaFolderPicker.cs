using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Comixa.Desktop.Services;

public sealed class AvaloniaFolderPicker : IFolderPicker
{
    private readonly Window _owner;

    public AvaloniaFolderPicker(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Choose comic folder"
            });

        cancellationToken.ThrowIfCancellationRequested();

        return folders.Count == 0
            ? null
            : folders[0].TryGetLocalPath();
    }
}
