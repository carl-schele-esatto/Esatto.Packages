using System.Text;
using Esatto.CookieScan.Core;

namespace Esatto.CookieScan.Engine;

/// <summary>What the merge endpoint reported back, or null when it was never called.</summary>
public sealed record MergeOutcome(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> AlreadyDeclared,
    IReadOnlyList<string> DeclaredButNotFound,
    Guid PolicyPageKey,
    bool Saved);

/// <summary>
/// Writes the two report files and formats the console summary. Split into two independent jobs
/// so the window can take the same <see cref="ScanResult"/> and show its findings in a grid
/// without also getting a console-formatted duplicate of the summary text.
/// </summary>
/// <remarks>
/// The exit code itself is not decided here - it lives on <see cref="ScanResult.ExitCode"/>, so
/// both front ends see the same number without either one recomputing it.
/// </remarks>
public static class ScanReportWriter
{
    public const int ExitError = 2;

    /// <summary>
    /// Creates the report directory and writes <c>cookie-scan-report.md</c> and
    /// <c>cookie-scan-report.json</c>.
    /// </summary>
    /// <remarks>
    /// Sources every section from <paramref name="result"/> rather than from separate parameters,
    /// so a caller cannot pass mismatched pieces of two different scans. The three facts that are
    /// NOT on the result come from <paramref name="options"/>, which is authoritative for a run that
    /// has just happened - see <see cref="Markdown"/> for where they come from when it has not.
    /// </remarks>
    public static void WriteFiles(ScanOptions options, ScanResult result)
    {
        string markdown = BuildMarkdown(
            site: options.Url.ToString(),
            maxPages: options.MaxPages,
            memberScan: options.MemberScanEnabled,
            result: result);

        Directory.CreateDirectory(options.ReportDir);

        (string markdownPath, string jsonPath) = ReportPaths(options);

        File.WriteAllText(markdownPath, markdown);
        File.WriteAllText(jsonPath, ScanJson.Serialize(result));
    }

    /// <summary>
    /// The same report, for a result with no <see cref="ScanOptions"/> beside it.
    /// </summary>
    /// <remarks>
    /// This is what a scan mails and what the history browser could offer to save: both hold a
    /// <see cref="ScanResult"/> read back from a file, long after the options that produced it went
    /// out of scope. Everything the document says is recorded on the result - the site as its own
    /// URL, and the page count and member dimension on <see cref="ScanResult.Options"/> - so the two
    /// entry points produce the same bytes for the same run.
    /// <para>
    /// The exception is a history file written before <see cref="ScanOptionsSummary"/> existed, whose
    /// <c>Options</c> is null. Those two lines then say "not recorded", which is the truth about that
    /// file rather than a default nobody chose - the same rule the diff view applies to the same
    /// missing member.
    /// </para>
    /// </remarks>
    public static string Markdown(ScanResult result) => BuildMarkdown(
        site: result.Site,
        maxPages: result.Options?.MaxPages,
        memberScan: result.Options?.MemberScanEnabled,
        result: result);

    /// <remarks>
    /// One builder behind both entry points. It was inlined in <see cref="WriteFiles"/> until the
    /// mail needed the same document from a result alone; a second copy that rendered "the report"
    /// slightly differently is precisely the drift that makes an attachment and a file on disk
    /// disagree about what a scan found.
    /// </remarks>
    private static string BuildMarkdown(string site, int? maxPages, bool? memberScan, ScanResult result)
    {
        var markdown = new StringBuilder();

        markdown.AppendLine("# Cookie scan report");
        markdown.AppendLine();
        markdown.AppendLine($"- Site: {site}");
        markdown.AppendLine(
            $"- Pages per pass: {(maxPages is null ? "not recorded" : $"up to {maxPages}")}");
        markdown.AppendLine(
            $"- Member dimension: {(memberScan is null ? "not recorded" : memberScan.Value ? "yes" : "no")}");
        markdown.AppendLine(
            $"- Write-back: {Describe(result.CanReachApi, result.DryRun, result.Outcome, result.Candidates.Count)}");
        markdown.AppendLine();

        // Violations first, deliberately. It is the finding that matters, and burying it under a
        // table of forty ordinary cookies is how a compliance problem goes unread.
        Section(markdown, "Violations", result.Violations.Select(candidate =>
            $"**{candidate.Name}** — categorised `{candidate.Category}`, but was set during the "
            + $"`{candidate.FirstSeenPass}` pass, which did not grant it. First seen at {candidate.FirstSeenUrl}"));

        if (result.Outcome is not null)
        {
            // In a dry run nothing was actually added - Describe gets that right already, but this
            // heading used to claim otherwise regardless of DryRun.
            string addedHeading = result.DryRun ? "Would be added (dry run)" : "Added to the policy page (draft)";
            Section(markdown, addedHeading, result.Outcome.Added);
            Section(markdown, "Already declared", result.Outcome.AlreadyDeclared);
            Section(
                markdown,
                "Declared but not found — reported, never deleted",
                result.Outcome.DeclaredButNotFound);
        }
        else
        {
            markdown.AppendLine("## Comparison against the policy page");
            markdown.AppendLine();
            // The same report is written by the console tool and the dashboard, so it names the
            // credentials without saying where they come from - that differs between the two.
            markdown.AppendLine(
                "Not performed. Give the scan the site's API client id and secret - the profile's API "
                + $"credentials in the dashboard, `--client-id` and `{ScanOptions.SecretVariable}` for "
                + "the console tool - to compare the scan against what the page already declares. "
                + "Add `--dry-run` to compare without writing anything.");
            markdown.AppendLine();
        }

        Section(markdown, "Needs review — only ever seen with everything granted", result.NeedsReview.Select(
            candidate => $"{candidate.Name} — written as `{candidate.Category}`, which is a fallback"));

        Section(markdown, "Expected but not observed", result.ExpectedButNotObserved);

        // Said here rather than left to the section's title, which reads as a gap. It is not one:
        // these went to the endpoint with everything else, so the operator should expect to see them
        // in the draft and should not go looking for a sighting that cannot happen.
        if (result.Outcome is not null && result.ExpectedButNotObserved.Count > 0)
        {
            markdown.AppendLine(
                "Declared anyway. The catalogue flags these as this site's own, and the crawl issues "
                + "only GETs - so a cookie written by a booking or registration POST can never be "
                + "observed here, however often the scan runs. Not observed is a reason to declare "
                + "one of these, not a reason to leave it out.");
            markdown.AppendLine();
        }

        // Every declaration this scan proposes, in one table, because "what is going on the page"
        // is the question this section answers and splitting it in two made the answer look like the
        // observed count alone. The First seen in column carries the provenance instead: a pass name
        // for a sighting, "not observed" for a catalogue row.
        IReadOnlyList<CookieDeclaration> fromCatalogue = result.DeclaredFromCatalogue ?? [];

        markdown.AppendLine("## All entries declared");
        markdown.AppendLine();
        markdown.AppendLine("| Name | Storage | Category | First seen in | Duration |");
        markdown.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (CookieDeclarationCandidate candidate in result.Candidates)
        {
            markdown.AppendLine(
                $"| `{candidate.Name}` | {candidate.StorageType} | {candidate.Category} "
                + $"| {candidate.FirstSeenPass} | {candidate.Duration} |");
        }

        foreach (CookieDeclaration declaration in fromCatalogue)
        {
            markdown.AppendLine(
                $"| `{declaration.Name}` | {declaration.StorageType} | {declaration.Category} "
                + $"| not observed (catalogue) | {declaration.Duration} |");
        }

        markdown.AppendLine();

        if (fromCatalogue.Count > 0)
        {
            markdown.AppendLine(
                $"{result.Candidates.Count} observed, {fromCatalogue.Count} from the catalogue - "
                + $"{result.Candidates.Count + fromCatalogue.Count} declared in total.");
            markdown.AppendLine();
        }

        Section(markdown, "Third-party hosts contacted", result.HostsByPass
            .Where(pass => pass.Value.Count > 0)
            .Select(pass => $"{pass.Key}: {string.Join(", ", pass.Value.Order())}"));

        return markdown.ToString();
    }

    /// <summary>
    /// The lines the console summary prints, in order, blank lines included as empty strings so
    /// the caller's line-by-line printing reproduces the pre-refactor output exactly.
    /// </summary>
    /// <remarks>
    /// Takes <paramref name="options"/> as well as <paramref name="result"/> because its final two
    /// lines name the report paths, and those depend on <see cref="ScanOptions.ReportDir"/> - a
    /// <see cref="ScanResult"/> alone cannot produce them.
    /// </remarks>
    public static IReadOnlyList<string> SummaryLines(ScanOptions options, ScanResult result)
    {
        (string markdownPath, string jsonPath) = ReportPaths(options);

        int fromCatalogue = (result.DeclaredFromCatalogue ?? []).Count;

        List<string> lines =
        [
            "",
            // The two numbers are different questions - what the crawl saw, and what the page will
            // say - and printing only the first one made a scan that declares four cookies read as
            // one that declares three.
            fromCatalogue == 0
                ? $"{result.Candidates.Count} entr(ies) found."
                : $"{result.Candidates.Count} entr(ies) found, {result.Candidates.Count + fromCatalogue} declared "
                    + $"({fromCatalogue} from the catalogue, unreachable by a crawl).",
        ];

        if (result.Violations.Count > 0)
        {
            lines.Add("");
            lines.Add($"  {result.Violations.Count} CONSENT VIOLATION(S):");

            foreach (CookieDeclarationCandidate violation in result.Violations)
            {
                lines.Add(
                    $"    {violation.Name} ({violation.Category}) was set during the "
                    + $"{violation.FirstSeenPass} pass, which did not grant it.");
            }
        }

        if (result.Outcome is not null)
        {
            lines.Add("");
            lines.Add($"  {WriteBackCounts(options.DryRun, result.Outcome)}");
            lines.Add($"  {WriteBackSentence(options.DryRun, result.Outcome)}");
        }

        if (result.ExpectedButNotObserved.Count > 0)
        {
            lines.Add("");
            lines.Add(
                "  Expected but not observed"
                + (result.Outcome is null ? "" : ", and declared from the catalogue")
                + ": " + string.Join(", ", result.ExpectedButNotObserved));
        }

        lines.Add("");
        lines.Add($"Report written to {markdownPath}");
        lines.Add($"                  {jsonPath}");

        return lines;
    }

    // Both report files live beside each other, named from the same options - computed once so
    // WriteFiles and SummaryLines can never disagree about where the files went.
    private static (string MarkdownPath, string JsonPath) ReportPaths(ScanOptions options)
        => (Path.Combine(options.ReportDir, "cookie-scan-report.md"),
            Path.Combine(options.ReportDir, "cookie-scan-report.json"));

    /// <summary>
    /// What the merge endpoint reported, as counts.
    /// </summary>
    /// <remarks>
    /// "added" is only true when the page was saved. In a dry run the endpoint computes the merge
    /// and writes nothing, and a line that said "2 added" sent an operator to the backoffice to look
    /// for blocks that were never created. The markdown heading already made this distinction; the
    /// console, the dashboard's log and the scan email all read this one line instead.
    /// <para>
    /// Public, and one of two, because <see cref="ScanEmail"/> is now a third reader. A mail saying
    /// "2 added" beside a log saying "2 would be added" would be that same bug again, one front end
    /// further along - and this time with no console session left open to check it against.
    /// </para>
    /// </remarks>
    public static string WriteBackCounts(bool dryRun, MergeOutcome outcome)
        => $"{outcome.Added.Count} {(dryRun ? "would be added" : "added")}, "
            + $"{outcome.AlreadyDeclared.Count} already declared, "
            + $"{outcome.DeclaredButNotFound.Count} declared but not found.";

    /// <summary>What happened to the policy page itself, in one sentence.</summary>
    /// <remarks>See <see cref="WriteBackCounts"/> for why this is shared rather than written twice.</remarks>
    public static string WriteBackSentence(bool dryRun, MergeOutcome outcome)
        => outcome.Saved
            ? $"The policy page ({outcome.PolicyPageKey}) was saved as a DRAFT. Review the new blocks "
                + "in the backoffice and publish when you are happy with the wording."
            : dryRun
                ? "Dry run - the policy page was not changed. Run again without dry run to save the new "
                    + "blocks as a draft."
                : "Nothing new to write - the policy page already declares everything that was found.";

    /// <remarks>
    /// Takes the two flags rather than the whole <see cref="ScanOptions"/>, because
    /// <see cref="Markdown"/> renders this same line for a result that has no options beside it -
    /// and <see cref="ScanResult"/> records both of them itself.
    /// </remarks>
    private static string Describe(bool canReachApi, bool dryRun, MergeOutcome? outcome, int candidateCount)
        => outcome switch
        {
            null when canReachApi is false => "not configured (report only)",
            // The scan deliberately skips the merge call for an empty candidate list rather than
            // let the endpoint reject it - that is a legitimate outcome, not an attempt that failed.
            null when candidateCount == 0 => "not attempted - nothing found to write back",
            null => "attempted but failed - see the console output",
            { Saved: true } => "saved as a draft",
            _ => dryRun ? "dry run, nothing written" : "nothing new to write",
        };

    private static void Section(StringBuilder markdown, string title, IEnumerable<string> lines)
    {
        List<string> materialised = [.. lines];

        markdown.AppendLine($"## {title}");
        markdown.AppendLine();

        if (materialised.Count == 0)
        {
            markdown.AppendLine("_None._");
        }
        else
        {
            foreach (string line in materialised)
            {
                markdown.AppendLine($"- {line}");
            }
        }

        markdown.AppendLine();
    }
}
