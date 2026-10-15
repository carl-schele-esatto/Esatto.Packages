using Microsoft.AspNetCore.Mvc;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

public interface IViewRenderer
{
    bool ViewExists(string path);
    Task<string> RenderAsync(string path, object model, ActionContext actionContext);
}
