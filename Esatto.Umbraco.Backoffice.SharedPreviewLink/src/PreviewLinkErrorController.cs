using Microsoft.AspNetCore.Mvc;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;

[Route("preview-link-error")]
public class PreviewLinkErrorController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
