using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>Renders a Razor view to a string within the current request.</summary>
public sealed class RazorViewRenderer : IViewRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IModelMetadataProvider _metadataProvider;

    public RazorViewRenderer(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IModelMetadataProvider metadataProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _metadataProvider = metadataProvider;
    }

    public bool ViewExists(string path) => _viewEngine.GetView(executingFilePath: null, path, isMainPage: false).Success;

    public async Task<string> RenderAsync(string path, object model, ActionContext actionContext)
    {
        ViewEngineResult result = _viewEngine.GetView(executingFilePath: null, path, isMainPage: true);
        if (!result.Success)
        {
            throw new InvalidOperationException($"View '{path}' not found.");
        }

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary(_metadataProvider, new ModelStateDictionary()) { Model = model };
        var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(actionContext, result.View, viewData, tempData, writer, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
