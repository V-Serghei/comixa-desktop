using Avalonia;
using Avalonia.Media.Imaging;
using Comixa.Core.Models;
using Comixa.Desktop.Reader;

namespace Comixa.Desktop.ViewModels;

public sealed class ReaderPageViewModel : ViewModelBase
{
    private static readonly PixelSize DefaultRenderSize = new(1400, 2400);

    private readonly ComicBook _comicBook;
    private readonly IPagePreviewLoader _pagePreviewLoader;
    private CancellationTokenSource? _loadCts;
    private Task? _loadTask;
    private Bitmap? _image;
    private bool _isAttached;
    private bool _isLoading;
    private bool _hasFailed;

    public ReaderPageViewModel(ComicBook comicBook, int pageIndex, IPagePreviewLoader pagePreviewLoader)
    {
        _comicBook = comicBook;
        PageIndex = pageIndex;
        _pagePreviewLoader = pagePreviewLoader;
    }

    public int PageIndex { get; }

    public string LoadingLabel => $"Page {PageIndex + 1}";

    public Bitmap? Image
    {
        get => _image;
        private set
        {
            if (_image == value) return;
            _image = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasImage));
        }
    }

    public bool HasImage => Image is not null;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            RaisePropertyChanged();
        }
    }

    public bool HasFailed
    {
        get => _hasFailed;
        private set
        {
            if (_hasFailed == value) return;
            _hasFailed = value;
            RaisePropertyChanged();
        }
    }

    public void AttachAndLoad()
    {
        _isAttached = true;
        if (Image is not null || _loadTask is { IsCompleted: false })
        {
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        _loadTask = LoadAsync(_loadCts.Token);
    }

    public void Detach()
    {
        _isAttached = false;
        _loadCts?.Cancel();
        _loadTask = null;
        Image = null;
        HasFailed = false;
        IsLoading = false;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        HasFailed = false;

        try
        {
            var page = _pagePreviewLoader is IRenderedPageLoader renderedPageLoader
                ? await renderedPageLoader.LoadRenderedPageAsync(_comicBook, PageIndex, DefaultRenderSize, cancellationToken)
                : await _pagePreviewLoader.LoadPageAsync(_comicBook, PageIndex, cancellationToken);

            if (cancellationToken.IsCancellationRequested || !_isAttached)
            {
                return;
            }

            Image = page;
            HasFailed = page is null;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!cancellationToken.IsCancellationRequested && _isAttached)
            {
                IsLoading = false;
            }
        }
    }
}
