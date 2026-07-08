namespace Esatto.Umbraco.Backoffice.CrossContent;

public interface ICrossContentTeaserFetcher
{
    Task<TeaserFetchResult> FetchAsync(Guid key, string? culture, CancellationToken ct);
}
