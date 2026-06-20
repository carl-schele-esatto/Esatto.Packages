namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;

public class PreviewLinkRequest
{
    public Guid ContentKey { get; set; }

    /// <summary>
    /// The culture (ISO code, e.g. "sv-SE") the editor is previewing — the backoffice's
    /// active language. Used to build the preview URL for the correct variant. Optional:
    /// when omitted (invariant content, or an older client) the server falls back to the
    /// content's domain-derived culture.
    /// </summary>
    public string? Culture { get; set; }
}
