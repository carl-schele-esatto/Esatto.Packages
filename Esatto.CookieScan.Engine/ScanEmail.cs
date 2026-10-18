using System.Globalization;
using System.Net;
using System.Text;

using Esatto.CookieScan.Core;

namespace Esatto.CookieScan.Engine;

/// <summary>One file riding along with a scan email.</summary>
/// <remarks>
/// Bytes rather than a path, and that is the point of the type. The report directory holds exactly
/// one <c>cookie-scan-report.md</c>, overwritten by every run, so a mail that attached it by path
/// would send last night's report for a scan picked out of the history - and would have nothing at
/// all to attach when the report write had failed. Built from the <see cref="ScanResult"/> instead,
/// a scan mails the same two files whether it finished a second ago or last Tuesday.
/// </remarks>
public sealed record EmailAttachment(string FileName, string MediaType, byte[] Content);

/// <summary>A scan as an email: the subject, both body parts, and the paperclip.</summary>
/// <remarks>
/// Two bodies, not one. <see cref="Html"/> is what a mail client shows; <see cref="Text"/> is the
/// alternative part for a client that will not render it, and for the preview line a phone shows
/// under the subject. They say the same things in the same order.
/// </remarks>
public sealed record ScanEmailContent(
    string Subject,
    string Html,
    string Text,
    IReadOnlyList<EmailAttachment> Attachments);

/// <summary>
/// Turns a finished scan into the email that reports it. No SMTP, no network, no file system.
/// </summary>
/// <remarks>
/// In the engine rather than in the window, and pure, for the reason <see cref="ScanRunner"/>'s own
/// remarks give about there being one runner: a console tool that grew a <c>--email-to</c> flag must
/// send the same message the dashboard does, and two composers would be two things to keep in step
/// with nothing to compile against. It takes a <see cref="ScanResult"/> and nothing else, so the
/// scan that just finished and the scan loaded out of the history folder compose identically -
/// everything the message says is recorded in the result itself.
/// <para>
/// Nothing here knows what MailKit is. The engine ships as a package, and a mail transport dragged
/// into every consumer of it just to compose a string would be a dependency nobody who references
/// this for its scanner asked for. See <c>EmailSender</c> in the desktop project, which is not packed.
/// </para>
/// </remarks>
public static class ScanEmail
{
    /// <summary>The subject, both bodies and both attachments for one scan.</summary>
    public static ScanEmailContent Compose(ScanResult result)
    {
        string site = Host(result.Site);
        string stem = $"cookie-scan-{Slug(site)}-{result.CompletedAt.UtcDateTime:yyyyMMdd-HHmmss}";

        return new ScanEmailContent(
            Subject: $"Cookie scan - {site} - {Verdict(result)}",
            Html: BuildHtml(result, site),
            Text: BuildText(result, site),
            Attachments:
            [
                new EmailAttachment(
                    $"{stem}.md", "text/markdown", Encoding.UTF8.GetBytes(ScanReportWriter.Markdown(result))),
                new EmailAttachment(
                    $"{stem}.json", "application/json", Encoding.UTF8.GetBytes(ScanJson.Serialize(result))),
            ]);
    }

    /// <summary>
    /// The finding, in the two or three words a subject line has room for.
    /// </summary>
    /// <remarks>
    /// Findings only, and deliberately not the dry-run flag. The subject answers "is anything wrong
    /// with this site"; whether the policy page was written to is a different question, answered in
    /// the body. A subject carrying both would bury the first behind the second, and an inbox full of
    /// "Cookie scan - client.se - dry run" says nothing at a glance.
    /// <para>
    /// The order is the order that matters: a violation outranks a review, and the two are never
    /// summed. It mirrors <see cref="ScanResult.ExitCode"/>, which puts findings above plumbing for
    /// the same reason.
    /// </para>
    /// </remarks>
    public static string Verdict(ScanResult result)
        => result.Violations.Count > 0
            ? $"{result.Violations.Count} violation{(result.Violations.Count == 1 ? "" : "s")}"
            : result.NeedsReview.Count > 0
                ? $"{result.NeedsReview.Count} to review"
                : "clean";

    /// <summary>How many entries the policy page would end up declaring - observed plus catalogue.</summary>
    /// <remarks>
    /// The same two numbers <see cref="ScanReportWriter.SummaryLines"/> keeps apart: what the crawl
    /// saw, and what the page will say. The tile shows the second, because that is the one an
    /// operator is being asked to sign off on.
    /// </remarks>
    private static int Declared(ScanResult result)
        => result.Candidates.Count + (result.DeclaredFromCatalogue ?? []).Count;

    // The palette is spelled out here rather than shared with app.css, and has to be: a mail client
    // strips <style> and knows nothing about custom properties, so every colour has to sit inline on
    // the element that uses it. Ordinary hex, no variables, tables for layout - this is 2003 HTML on
    // purpose, and Outlook rendering Word's engine is why.
    private const string Ink = "#1c2024";
    private const string Muted = "#6b7280";
    private const string Line = "#e5e7eb";
    private const string Danger = "#b42318";
    private const string DangerBg = "#fef3f2";
    private const string Amber = "#b54708";
    private const string AmberBg = "#fffaeb";
    private const string Good = "#067647";
    private const string GoodBg = "#ecfdf3";

    private const string Sans =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    private const string Mono = "ui-monospace,SFMono-Regular,Menlo,Consolas,monospace";

    private static string BuildHtml(ScanResult result, string site)
    {
        (string ink, string bg) = Tone(result);

        StringBuilder html = new();

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html><head><meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        html.AppendLine($"<title>{E($"Cookie scan - {site}")}</title></head>");
        html.AppendLine($"<body style=\"margin:0;padding:0;background:#f5f6f7;color:{Ink};\">");
        html.AppendLine(
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" "
            + "style=\"background:#f5f6f7;padding:24px 12px;\"><tr><td align=\"center\">");
        html.AppendLine(
            "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"640\" "
            + $"style=\"width:100%;max-width:640px;background:#ffffff;border:1px solid {Line};"
            + $"border-radius:12px;font-family:{Sans};font-size:15px;line-height:1.5;\">");

        // Header: what was scanned, when, and the verdict as a pill - the three things a reader needs
        // before deciding whether to read the rest.
        html.AppendLine("<tr><td style=\"padding:24px 24px 8px 24px;\">");
        html.AppendLine(
            $"<div style=\"font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:{Muted};\">"
            + "Cookie declaration</div>");
        html.AppendLine($"<div style=\"font-size:22px;font-weight:600;padding-top:4px;\">{E(site)}</div>");
        html.AppendLine(
            $"<div style=\"font-size:13px;color:{Muted};padding-top:4px;\">{E(When(result))}</div>");
        html.AppendLine(
            $"<div style=\"display:inline-block;margin-top:12px;padding:5px 12px;border-radius:999px;"
            + $"background:{bg};color:{ink};font-size:13px;font-weight:600;\">{E(Verdict(result))}</div>");
        html.AppendLine("</td></tr>");

        // The four counts as one row of cells. A table rather than flexbox, for the reason the
        // palette comment above gives.
        html.AppendLine("<tr><td style=\"padding:16px 24px 4px 24px;\">");
        html.AppendLine(
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");

        Tile(html, Declared(result).ToString(CultureInfo.InvariantCulture), "declared", Ink);
        Tile(
            html,
            result.Violations.Count.ToString(CultureInfo.InvariantCulture),
            "violations",
            result.Violations.Count > 0 ? Danger : Ink);
        Tile(
            html,
            result.NeedsReview.Count.ToString(CultureInfo.InvariantCulture),
            "to review",
            result.NeedsReview.Count > 0 ? Amber : Ink);
        Tile(html, result.DryRun ? "dry" : "live", "write-back", Ink);

        html.AppendLine("</tr></table></td></tr>");

        if (result.Violations.Count > 0)
        {
            // First, and in its own tinted box. It is the finding that matters, and the report file
            // puts it first for the same reason - a compliance problem under a table of forty
            // ordinary cookies goes unread.
            Callout(
                html,
                "Consent violations",
                Danger,
                DangerBg,
                result.Violations.Select(candidate =>
                    $"<span style=\"font-family:{Mono};font-weight:600;\">{E(candidate.Name)}</span> - "
                    + $"categorised {E(candidate.Category)}, but was set during the "
                    + $"{E(candidate.FirstSeenPass.ToString())} pass, which did not grant it. "
                    + $"First seen at {E(candidate.FirstSeenUrl)}"));
        }

        if (result.NeedsReview.Count > 0)
        {
            Callout(
                html,
                "Needs review",
                Amber,
                AmberBg,
                result.NeedsReview.Select(candidate =>
                    $"<span style=\"font-family:{Mono};font-weight:600;\">{E(candidate.Name)}</span> - "
                    + $"written as {E(candidate.Category)}, which is a fallback. Only ever seen with "
                    + "everything granted."));
        }

        Block(html, "The policy page", WriteBackLines(result));

        if (result.ExpectedButNotObserved.Count > 0)
        {
            Block(
                html,
                "Expected but not observed",
                [E(string.Join(", ", result.ExpectedButNotObserved))]);
        }

        html.AppendLine(
            $"<tr><td style=\"padding:16px 24px 24px 24px;\"><div style=\"border-top:1px solid {Line};"
            + $"padding-top:12px;font-size:12px;color:{Muted};\">The full report is attached as markdown "
            + "and JSON. Sent by the Esatto cookie scanner; sending this changed nothing on the site."
            + "</div></td></tr>");

        html.AppendLine("</table></td></tr></table></body></html>");

        return html.ToString();
    }

    private static string BuildText(ScanResult result, string site)
    {
        StringBuilder text = new();

        text.AppendLine($"Cookie scan - {site}");
        text.AppendLine(When(result));
        text.AppendLine();
        text.AppendLine(
            $"{Declared(result)} declared, {result.Violations.Count} violation(s), "
            + $"{result.NeedsReview.Count} to review, write-back {(result.DryRun ? "dry run" : "live")}.");

        if (result.Violations.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("CONSENT VIOLATIONS");

            foreach (CookieDeclarationCandidate candidate in result.Violations)
            {
                text.AppendLine(
                    $"  {candidate.Name} ({candidate.Category}) was set during the "
                    + $"{candidate.FirstSeenPass} pass, which did not grant it. "
                    + $"First seen at {candidate.FirstSeenUrl}");
            }
        }

        if (result.NeedsReview.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("NEEDS REVIEW");

            foreach (CookieDeclarationCandidate candidate in result.NeedsReview)
            {
                text.AppendLine($"  {candidate.Name} - written as {candidate.Category}, which is a fallback.");
            }
        }

        text.AppendLine();
        text.AppendLine("THE POLICY PAGE");

        foreach (string line in WriteBackLines(result))
        {
            text.AppendLine($"  {WebUtility.HtmlDecode(line)}");
        }

        if (result.ExpectedButNotObserved.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"Expected but not observed: {string.Join(", ", result.ExpectedButNotObserved)}");
        }

        text.AppendLine();
        text.AppendLine("The full report is attached as markdown and JSON.");

        return text.ToString();
    }

    /// <summary>
    /// What happened to the policy page, in the words the console and the log already use.
    /// </summary>
    /// <remarks>
    /// Both sentences come from <see cref="ScanReportWriter"/> rather than being written again here.
    /// A mail telling an operator "2 added" while the dashboard's own log said "2 would be added" is
    /// exactly the drift the summary lines were fixed for once already.
    /// <para>
    /// Encoded here rather than by the callers, because both of them take these lines as HTML: the
    /// text body decodes them back. That is the cheaper direction - the alternative is carrying the
    /// same sentence twice in two encodings.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> WriteBackLines(ScanResult result)
    {
        if (result.Outcome is null)
        {
            return
            [
                E(result.CanReachApi is false
                    ? "Not compared. The scan ran report-only - it had no API credentials for this site, so "
                        + "nothing was checked against what the page already declares."
                    : result.Candidates.Count == 0
                        ? "Not attempted - the scan found nothing to write back."
                        : "The comparison was attempted and it failed. See the attached report."),
            ];
        }

        return
        [
            E(ScanReportWriter.WriteBackCounts(result.DryRun, result.Outcome)),
            E(ScanReportWriter.WriteBackSentence(result.DryRun, result.Outcome)),
        ];
    }

    /// <summary>The pill's colours: red for a violation, amber for a review, green for neither.</summary>
    private static (string Ink, string Background) Tone(ScanResult result)
        => result.Violations.Count > 0 ? (Danger, DangerBg)
            : result.NeedsReview.Count > 0 ? (Amber, AmberBg)
            : (Good, GoodBg);

    private static void Tile(StringBuilder html, string value, string label, string ink)
        => html.AppendLine(
            "<td width=\"25%\" style=\"padding:0 6px 0 0;vertical-align:top;\">"
            + $"<div style=\"border:1px solid {Line};border-radius:10px;padding:12px 10px;\">"
            + $"<div style=\"font-size:24px;font-weight:600;color:{ink};\">{E(value)}</div>"
            + $"<div style=\"font-size:12px;color:{Muted};padding-top:2px;\">{E(label)}</div>"
            + "</div></td>");

    /// <remarks>
    /// The items are already-encoded HTML rather than text: a violation carries a monospace span
    /// around the cookie name, and encoding it here would print the tag. Every caller encodes what it
    /// interpolates - see the calls in <see cref="BuildHtml"/>.
    /// </remarks>
    private static void Callout(
        StringBuilder html, string title, string ink, string background, IEnumerable<string> items)
    {
        html.AppendLine("<tr><td style=\"padding:12px 24px 0 24px;\">");
        html.AppendLine(
            $"<div style=\"background:{background};border:1px solid {ink};border-radius:10px;"
            + "padding:14px 16px;\">");
        html.AppendLine(
            $"<div style=\"font-size:13px;font-weight:700;color:{ink};text-transform:uppercase;"
            + $"letter-spacing:.06em;\">{E(title)}</div>");
        html.AppendLine("<ul style=\"margin:8px 0 0 0;padding-left:18px;\">");

        foreach (string item in items)
        {
            html.AppendLine($"<li style=\"padding:3px 0;\">{item}</li>");
        }

        html.AppendLine("</ul></div></td></tr>");
    }

    private static void Block(StringBuilder html, string title, IEnumerable<string> lines)
    {
        html.AppendLine("<tr><td style=\"padding:16px 24px 0 24px;\">");
        html.AppendLine(
            $"<div style=\"font-size:13px;font-weight:700;color:{Muted};text-transform:uppercase;"
            + $"letter-spacing:.06em;\">{E(title)}</div>");

        foreach (string line in lines)
        {
            html.AppendLine($"<div style=\"padding-top:6px;\">{line}</div>");
        }

        html.AppendLine("</td></tr>");
    }

    /// <summary>The scan's instant, with the offset it was recorded in rather than the reader's.</summary>
    /// <remarks>
    /// A mail is read on a machine that is not the one that scanned, so a local time with no offset
    /// on it is a time nobody can place. The offset is part of what the result already records;
    /// printing it costs seven characters and removes the ambiguity entirely.
    /// </remarks>
    private static string When(ScanResult result)
        => result.CompletedAt.ToString("yyyy-MM-dd HH:mm 'UTC'zzz", CultureInfo.InvariantCulture);

    /// <summary>The site as a host, or as it was recorded when that is not a URL.</summary>
    private static string Host(string? site)
        => Uri.TryCreate(site, UriKind.Absolute, out Uri? uri) ? uri.Host : (site ?? "").Trim();

    /// <summary>A host as a filename fragment: letters, digits and single hyphens, nothing else.</summary>
    /// <remarks>
    /// Attachment names reach a file system this program has never seen, so the safe set is the small
    /// one. A host that reduces to nothing - which takes a <see cref="ScanResult.Site"/> of pure
    /// punctuation - becomes "site" rather than leaving an empty gap in the middle of the name.
    /// </remarks>
    private static string Slug(string host)
    {
        string slug = new([.. host.Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')]);

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        slug = slug.Trim('-');

        return slug.Length == 0 ? "site" : slug;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
