using Comixa.Core.Models;

namespace Comixa.Reader.Rendering;

public interface IPageRenderer
{
    Task<PageRenderResult> RenderAsync(Stream pageStream, ComicPage page, CancellationToken cancellationToken = default);
}
