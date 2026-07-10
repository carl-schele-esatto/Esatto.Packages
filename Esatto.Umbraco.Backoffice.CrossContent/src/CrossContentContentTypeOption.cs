namespace Esatto.Umbraco.Backoffice.CrossContent;

/// <summary>One pickable content type. Bound from "CrossContent:ContentTypes".</summary>
public sealed class CrossContentContentTypeOption
{
    /// <summary>Doc-type alias to list from the target's Delivery API, e.g. "casePage".</summary>
    public string Alias { get; set; } = "";

    /// <summary>Optional label for the picker's type dropdown. Falls back to <see cref="Alias"/>.</summary>
    public string? Label { get; set; }
}
