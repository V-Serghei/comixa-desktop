using Avalonia.Media.Imaging;

namespace Comixa.Desktop.ViewModels;

public sealed class ReaderPageViewModel
{
    public ReaderPageViewModel(int pageNumber, Bitmap image)
    {
        PageNumber = pageNumber;
        Image = image;
    }

    public int PageNumber { get; }

    public Bitmap Image { get; }

    public string Label => PageNumber.ToString();
}
