using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CrossContent;

public sealed class CrossContentCaseListClient(HttpClient http, IOptions<CrossContentOptions> options)
    : ICrossContentCaseListClient
{
    private readonly CrossContentOptions _options = options.Value;

    // Delivery-API list query per configured type. {0} = URL-escaped doc-type alias.
    private const string TypeQueryTemplate =
        "umbraco/delivery/api/v2/content?filter=contentType:{0}&sort=name:asc&take=100";

    public async Task<IReadOnlyList<CrossContentCaseListItem>> ListCasesAsync(CancellationToken ct)
    {
        var types = _options.ContentTypes;
        if (types is { Count: > 0 })
        {
            var merged = new List<CrossContentCaseListItem>();
            foreach (var t in types)
            {
                if (string.IsNullOrWhiteSpace(t.Alias)) continue;
                var path = string.Format(TypeQueryTemplate, Uri.EscapeDataString(t.Alias));
                merged.AddRange(await FetchAsync(path, t.Alias, ct));
            }
            return merged;
        }

        // Legacy single-path mode (pre-1.1): one query, items tagged "casePage".
        return await FetchAsync(_options.CaseListPath, "casePage", ct);
    }

    // One Delivery-API list request. Isolated try/catch per call so a single failing type
    // (or the legacy path) degrades to an empty list rather than 500-ing the picker.
    private async Task<IReadOnlyList<CrossContentCaseListItem>> FetchAsync(string path, string type, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(path, ct);
            if (!resp.IsSuccessStatusCode) return [];
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<CrossContentCaseListItem>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var key))
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    result.Add(new CrossContentCaseListItem(key, name, type));
                }
            }
            return result;
        }
        catch (Exception) { return []; }
    }
}
