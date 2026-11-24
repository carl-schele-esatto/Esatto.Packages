namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// Decides who may see the findings, and removes them from the report for everyone else.
///
/// WHY THE SERVER AND NOT THE INTERFACE. The dashboard's tab strip is already data driven: the "To fix" tab only exists when the
/// report carries at least one annotation with severity "warning". If the server leaves them out the tab disappears by itself
/// and the findings never reach the browser. A condition in the interface would have hidden the tab but still sent the texts over
/// the wire, where anyone with Content access could read them in the network tab.
///
/// GROUP ALIAS AND NOT GUID OR NAME. An alias is the same string in every environment, while a group's GUID differs between
/// environments when the group is created by hand in each, and a person's name stops applying the day someone changes their name
/// or leaves. With an alias, "who sees the findings" is a checkbox on a user group, not a code change.
/// </summary>
public static class FindingsFilter
{
    /// <summary>
    /// The severity that makes an annotation a FINDING. The same string both views share; everything else ("info") is
    /// information about the data and is never hidden.
    /// </summary>
    private const string FindingSeverity = "warning";

    /// <summary>
    /// True when the user may see the findings.
    ///
    /// AN EMPTY CONFIGURATION MEANS NO RESTRICTION, that is, the behaviour from before the feature existed. Chosen deliberately
    /// in that direction: a misspelt or forgotten setting must not silently hide the list of fixes from the people who should
    /// have it. Whoever wants to restrict it does so actively.
    /// </summary>
    /// <param name="userGroupAliases">The signed-in backoffice user's group aliases.</param>
    /// <param name="allowedGroups">The configured aliases of the groups that may see the findings.</param>
    public static bool IsAllowed(IEnumerable<string>? userGroupAliases, IEnumerable<string>? allowedGroups)
    {
        var allowed = Aliases(allowedGroups);

        if (allowed.Count == 0) return true;

        return Aliases(userGroupAliases).Any(mine => allowed.Contains(mine, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The report without its findings. Returns the same instance when there is nothing to remove: reports are records, and a
    /// needless copy of a whole tree per call would be waste without gain.
    ///
    /// Only <c>traffic.search.annotations</c> is touched. It is the list both views read their findings from; the charts' own
    /// annotations describe curves and are not findings, and insights interpret the data and carry no severity.
    /// </summary>
    public static Report? StripFindings(Report? report)
    {
        if (report?.Traffic?.Search is not { } search) return report;
        if (search.Annotations is not { Count: > 0 } notes) return report;

        var kept = notes.Where(x => x?.Severity != FindingSeverity).ToList();

        if (kept.Count == notes.Count) return report;

        return report with
        {
            Traffic = report.Traffic with { Search = search with { Annotations = kept } },
        };
    }

    /// <summary>The aliases of a list without the blank entries, trimmed.</summary>
    private static List<string> Aliases(IEnumerable<string>? aliases) =>
        [.. (aliases ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())];
}
