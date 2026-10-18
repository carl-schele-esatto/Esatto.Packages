namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>Every handled outcome is a 200 with one of three statuses; the backoffice never sees an exception page.</summary>
public sealed record RenderOutcome(string Status, string? Html = null, string? Reason = null, string? Message = null)
{
    public static RenderOutcome Rendered(string html) => new("rendered", Html: html);

    /// <param name="reason"><c>noPartial</c>, <c>ignored</c> or <c>unsavedDocument</c>.</param>
    public static RenderOutcome NotPreviewable(string reason) => new("notPreviewable", Reason: reason);

    public static RenderOutcome Error(string message) => new("error", Message: message);
}
