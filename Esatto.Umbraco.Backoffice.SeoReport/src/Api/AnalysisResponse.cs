using System.Text.Json.Serialization;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Api;

// The answer of GET analysis. Written in camelCase by the Management API, like every response here (AnalysisJsonTests pins the names).
// The parts are the analysis classes' own records: no prose, only keys with their arguments (TextRef), numbers and months written
// "YYYY-MM". A null inside a statement's arguments is sent and means "nothing to say here", which is not the same as no argument.
// Every collection is an array: the Management API makes an interface-typed member polymorphic over the site's own classes that
// implement it, and a plain list then cannot be written (HostTypesJsonTests).

/// <summary>
/// The analysis of one month against the archive. This is the endpoint's one DECLARED 200 type (the OpenAPI document and the generated
/// client know this one); an archive with nothing in it gets <see cref="NoAnalysisResponse"/> at run time instead, which is why a
/// client reads <c>hasAnalysis</c> before anything else.
/// </summary>
/// <param name="Period">The month analysed, <c>YYYY-MM</c>: the one asked for, or the newest when none was.</param>
/// <param name="Summary">The month in statements, each a key and the figures it is written with (see <see cref="Narrative"/>). Empty when there is nothing to say.</param>
/// <param name="Note">The month's own note, or null.</param>
/// <param name="TargetHistory">The target queries' positions month by month across the archive; null without target queries or without a month that has search queries.</param>
/// <param name="Movement">The pages that gained and lost most, and against what; null when the month has no page list or nothing to compare with.</param>
/// <param name="Opportunities">The quick wins, cheapest first; NULL FOR A USER OUTSIDE THE FINDINGS GROUPS, who gets them from no part of the analysis.</param>
/// <param name="Targets">The target queries held up against the month; null without target queries.</param>
public sealed record AnalysisResponse(
    string Period,
    Statement[] Summary,
    string? Note,
    TargetGrid? TargetHistory,
    MovementResult? Movement,
    Opportunity[]? Opportunities,
    TargetAnalysisResponse? Targets)
{
    /// <summary>Always true: it is what tells this answer from <see cref="NoAnalysisResponse"/>.</summary>
    [JsonPropertyOrder(-1)]
    public bool HasAnalysis => true;
}

/// <summary>The answer when the archive holds nothing to analyse: <c>{ "hasAnalysis": false }</c> and nothing else.</summary>
public sealed record NoAnalysisResponse
{
    public bool HasAnalysis => false;
}

/// <summary>The editor's target queries held up against the month's search queries.</summary>
/// <param name="RowsFetched">How many search queries the month holds (not the code's limit): "not visible" means "not among these".</param>
/// <param name="Rows">One row per target query, in the editor's order.</param>
public sealed record TargetAnalysisResponse(int RowsFetched, TargetRow[] Rows);
