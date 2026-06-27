using Comixa.Reader.Rendering;

namespace Comixa.Reader.Tests;

public sealed class PageRenderResultTests
{
    [Fact]
    public void PageRenderResultStoresRenderedPageMetadata()
    {
        var result = new PageRenderResult(1920, 2880, "image/jpeg");

        Assert.Equal(1920, result.Width);
        Assert.Equal(2880, result.Height);
        Assert.Equal("image/jpeg", result.ContentType);
    }
}
