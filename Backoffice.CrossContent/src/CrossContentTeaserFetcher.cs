using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Backoffice.CrossContent;

public sealed class CrossContentTeaserFetcher(
    IHttpClientFactory httpClientFactory,
    IOptions<CrossContentOptions> options) : ICrossContentTeaserFetcher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _teaserPath = options.Value.TeaserPath.Trim('/');

    public async Task<TeaserFetchResult> FetchAsync(Guid key, string? culture, CancellationToken ct)
    {
        var url = $"{_teaserPath}/{key}";
        if (!string.IsNullOrWhiteSpace(culture)) url += $"?culture={Uri.EscapeDataString(culture)}";
        try
        {
            var http = httpClientFactory.CreateClient("crosscontent");
            using var resp = await http.GetAsync(url, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return TeaserFetchResult.Gone;
            if (!resp.IsSuccessStatusCode) return TeaserFetchResult.Failed;

            var dto = await resp.Content.ReadFromJsonAsync<CrossContentTeaser>(Json, ct);
            return dto is null ? TeaserFetchResult.Failed : TeaserFetchResult.Ok(dto);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return TeaserFetchResult.Failed;   // timeout (HttpClient.Timeout)
        }
        catch (Exception)
        {
            return TeaserFetchResult.Failed;   // network/parse — never throw to caller
        }
    }
}
