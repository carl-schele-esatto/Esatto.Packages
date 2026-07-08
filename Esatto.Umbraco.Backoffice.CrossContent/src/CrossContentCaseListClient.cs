using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CrossContent;

public sealed class CrossContentCaseListClient(HttpClient http, IOptions<CrossContentOptions> options)
    : ICrossContentCaseListClient
{
    private readonly string _path = options.Value.CaseListPath;

    public async Task<IReadOnlyList<CrossContentCaseListItem>> ListCasesAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(_path, ct);
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
                    result.Add(new CrossContentCaseListItem(key, name));
                }
            }
            return result;
        }
        catch (Exception) { return []; }   // picker degrades to empty list, never 500s
    }
}
