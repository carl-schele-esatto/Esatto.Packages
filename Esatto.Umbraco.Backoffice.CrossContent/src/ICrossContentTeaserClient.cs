namespace Esatto.Umbraco.Backoffice.CrossContent;

public interface ICrossContentTeaserClient
{
    /// <summary>Returns the teaser for a case, or null when the card should hide (gone / cold-miss-failed).</summary>
    Task<CrossContentTeaser?> GetTeaserAsync(Guid key, string? culture, CancellationToken ct);
}
