using Microsoft.AspNetCore.Mvc;

namespace Backoffice.PreviewLink;

[Route("preview-link-error")]
public class PreviewLinkErrorController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
