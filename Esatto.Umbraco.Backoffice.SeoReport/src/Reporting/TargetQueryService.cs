using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// The editor's list of TARGET QUERIES: the searches the site wants to be found for.
///
/// Kept in the <c>targets</c> document, not with the settings and not in the reports. It is configuration and not a measurement:
/// the analysis is laid over the archive when it is read, so changing the list changes the view of the whole history at once.
/// </summary>
public interface ITargetQueryService
{
    /// <summary>The most terms a list holds. A table longer than this is not read, and the cost of the analysis grows with it.</summary>
    const int MaxTerms = 50;

    /// <summary>
    /// The longest term. A whole sentence is not a target query, and a very long string would match no query at all and so be
    /// reported as "missing" for ever.
    /// </summary>
    const int MaxTermLength = 100;

    /// <summary>The list in the editor's order. Empty when nothing is saved, or when what is saved cannot be read.</summary>
    Task<IReadOnlyList<string>> GetAsync();

    /// <summary>
    /// Saves the list and returns it as saved: trimmed, white space collapsed, lower case, without blanks and without
    /// duplicates, cut to <see cref="MaxTerms"/>. A term longer than <see cref="MaxTermLength"/> is dropped, not shortened.
    /// </summary>
    Task<IReadOnlyList<string>> SaveAsync(IEnumerable<string>? terms);
}

/// <inheritdoc />
public sealed class TargetQueryService(ISeoReportStore store, ILogger<TargetQueryService>? logger = null) : ITargetQueryService
{
    public async Task<IReadOnlyList<string>> GetAsync()
    {
        var json = await store.GetDocumentAsync(DocumentKeys.Targets);
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            // Normalized on the way OUT as well. Not because the store is dirty (only SaveAsync writes to it), but so that a list
            // saved under an older version's rules follows today's without being saved again, and so a blank term can never reach
            // the analysis, however the document got there.
            return Normalize(JsonSerializer.Deserialize<List<string?>>(json));
        }
        catch (JsonException)
        {
            // A damaged list must not take the dashboard down. An empty list is the same as "nothing saved yet". Nothing of the
            // document is logged.
            logger?.LogWarning("The saved target queries cannot be read and are treated as empty.");
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> SaveAsync(IEnumerable<string>? terms)
    {
        var clean = Normalize(terms);
        await store.SaveDocumentAsync(DocumentKeys.Targets, JsonSerializer.Serialize(clean));
        return clean;
    }

    /// <summary>
    /// Cleans a list. A pure function, apart from the store: it carries the whole rulebook and needs no database to be tested.
    ///
    /// Each term is trimmed, any run of white space inside it becomes one space (Search Console's queries are single-spaced, so
    /// a term typed with a double space or a tab could never match one) and it is lower-cased with the invariant culture. Blank
    /// terms are dropped: an empty term would match EVERY query, which is a row claiming that the site performs on everything.
    /// Duplicates are dropped after that, the first kept. A term longer than <see cref="ITargetQueryService.MaxTermLength"/> is
    /// dropped and NOT cut: a cut term is a search the editor never typed and would quietly match queries nobody asked about;
    /// the loss shows in the list at once, as <see cref="SaveAsync"/> returns it. The list stops at
    /// <see cref="ITargetQueryService.MaxTerms"/>, so it is the last terms that go, not the first. The order is kept: the editor
    /// puts what matters most first, and that is information.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string?>? terms)
    {
        if (terms is null) return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var clean = new List<string>();

        foreach (var raw in terms)
        {
            var term = Tidy(raw);

            if (term.Length == 0 || term.Length > ITargetQueryService.MaxTermLength || !seen.Add(term)) continue;

            clean.Add(term);
            if (clean.Count == ITargetQueryService.MaxTerms) break;
        }

        return clean;
    }

    /// <summary>Trimmed, inner white space as single spaces, lower case; empty for null or white space.</summary>
    private static string Tidy(string? term)
        => term is null
            ? ""
            : string.Join(' ', term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
