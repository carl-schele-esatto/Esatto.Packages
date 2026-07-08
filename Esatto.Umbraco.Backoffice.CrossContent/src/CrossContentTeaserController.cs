using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Web;

namespace Esatto.Umbraco.Backoffice.CrossContent;

// Cross-site teaser endpoint. AllowAnonymous + a shared-key check: the consumer sends the "Api-Key"
// header (the SAME key as this site's Umbraco:CMS:DeliveryApi:ApiKey), read server-to-server.
// Attribute-routed → auto-discovered. Read-only, published only.
[ApiController]
[Route("api/crosscontent/teaser")]
[AllowAnonymous]
public sealed class CrossContentTeaserController : Controller
{
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly DeliveryApiSettings _deliveryApi;
    private readonly ICrossContentTeaserMapper _mapper;

    public CrossContentTeaserController(
        IUmbracoContextAccessor umbracoContextAccessor,
        IOptions<DeliveryApiSettings> deliveryApiOptions,
        ICrossContentTeaserMapper mapper)
    {
        _umbracoContextAccessor = umbracoContextAccessor;
        _deliveryApi = deliveryApiOptions.Value;
        _mapper = mapper;
    }

    [HttpGet("{key:guid}")]
    [ProducesResponseType(typeof(CrossContentTeaser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(Guid key, [FromQuery] string? culture = null)
    {
        if (!IsAuthorized()) return Unauthorized();
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext)) return NotFound();

        var content = umbracoContext.Content?.GetById(key);
        if (content is null) return NotFound();

        var etag = $"\"{content.UpdateDate.Ticks:x}\"";
        if (Request.Headers.IfNoneMatch.ToString() == etag) return StatusCode(StatusCodes.Status304NotModified);

        var teaser = _mapper.Map(content, culture);
        if (teaser is null) return NotFound();

        Response.Headers.ETag = etag;
        return Ok(teaser);
    }

    private bool IsAuthorized()
    {
        var configuredKey = _deliveryApi.ApiKey;
        if (string.IsNullOrEmpty(configuredKey)) return false; // fail closed
        var providedKey = Request.Headers["Api-Key"].ToString();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedKey),
            Encoding.UTF8.GetBytes(configuredKey));
    }
}
