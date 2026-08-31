using System.Text;

using Esatto.CookieScan.Core;
using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Tests;

public class ScanEmailTests
{
    private static readonly Guid PolicyPageKey = new("11111111-2222-3333-4444-555555555555");

    private static readonly DateTimeOffset Completed =
        new(2026, 8, 30, 15, 57, 0, TimeSpan.FromHours(2));

    private static CookieDeclarationCandidate Candidate(
        string name, CandidateFlag flag, ConsentPass pass = ConsentPass.AcceptAll)
        => new(name, "Google", "marketing", "Mäter.", "24 månader", "Cookie", flag, pass,
            "https://example.com/kontakt");

    private static ScanResult Result(
        IReadOnlyList<CookieDeclarationCandidate>? candidates = null,
        IReadOnlyList<CookieDeclarationCandidate>? violations = null,
        MergeOutcome? outcome = null,
        bool dryRun = true,
        bool canReachApi = true,
        ScanOptionsSummary? options = null)
        => new(
            Candidates: candidates ?? [Candidate("_ga", CandidateFlag.None)],
            Violations: violations ?? [],
            ExpectedButNotObserved: [],
            HostsByPass: new Dictionary<ConsentPass, IReadOnlyList<string>>(),
            Outcome: outcome,
            CanReachApi: canReachApi,
            DryRun: dryRun,
            CompletedAt: Completed,
            Site: "https://example.com/",
            Options: options ?? new ScanOptionsSummary(25, Locale.Sv, false, dryRun));

    // The subject is the whole message for anyone reading a phone notification, so it has to answer
    // "is anything wrong with this site" without being opened.
    [Fact]
    public void A_clean_scan_says_so_in_the_subject()
    {
        ScanEmailContent content = ScanEmail.Compose(Result());

        Assert.Equal("Cookie scan - example.com - clean", content.Subject);
    }

    [Fact]
    public void One_violation_is_singular_and_two_are_not()
    {
        Assert.Equal("1 violation", ScanEmail.Verdict(
            Result(violations: [Candidate("_ga", CandidateFlag.Violation)])));

        Assert.Equal("2 violations", ScanEmail.Verdict(Result(violations:
        [
            Candidate("_ga", CandidateFlag.Violation),
            Candidate("_fbp", CandidateFlag.Violation),
        ])));
    }

    // A violation outranks a review and the two are never summed - the same order ExitCode applies.
    // A subject reading "3 to review" on a run that also found a violation would bury the finding
    // that actually matters.
    [Fact]
    public void A_violation_outranks_a_review_in_the_verdict()
    {
        ScanResult result = Result(
            candidates: [Candidate("_ga", CandidateFlag.NeedsReview), Candidate("_fbp", CandidateFlag.NeedsReview)],
            violations: [Candidate("_ga", CandidateFlag.Violation)]);

        Assert.Equal("1 violation", ScanEmail.Verdict(result));
    }

    [Fact]
    public void Reviews_reach_the_verdict_when_nothing_was_violated()
    {
        ScanResult result = Result(candidates: [Candidate("_hjid", CandidateFlag.NeedsReview)]);

        Assert.Equal("1 to review", ScanEmail.Verdict(result));
    }

    // The dry-run flag is deliberately not in the subject: it answers a different question, and an
    // inbox full of "- dry run" would say nothing at a glance. It still has to be IN the message.
    [Fact]
    public void The_subject_carries_the_finding_and_not_the_dry_run_flag()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(dryRun: true));

        Assert.DoesNotContain("dry", content.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dry", content.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_violation_is_spelled_out_in_both_bodies()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(
            violations: [Candidate("_fbp", CandidateFlag.Violation, ConsentPass.RejectAll)]));

        Assert.Contains("_fbp", content.Html);
        Assert.Contains("RejectAll", content.Html);
        Assert.Contains("https://example.com/kontakt", content.Html);

        Assert.Contains("_fbp", content.Text);
        Assert.Contains("RejectAll", content.Text);
    }

    // A cookie name is a string a site chose, not one this program did. It reaches an HTML body, so
    // it goes through the encoder - a name carrying a tag must render as text rather than as markup.
    [Fact]
    public void A_cookie_name_that_looks_like_markup_is_encoded()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(
            violations: [Candidate("<script>x</script>", CandidateFlag.Violation)]));

        Assert.DoesNotContain("<script>", content.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", content.Html, StringComparison.Ordinal);
    }

    // The text part exists for clients that will not render HTML and for the preview line under a
    // subject. A tag surviving into it would be read as literal text by exactly those readers.
    [Fact]
    public void The_text_body_carries_no_markup()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(
            outcome: new MergeOutcome(["_ga"], [], [], PolicyPageKey, false)));

        Assert.DoesNotContain("<", content.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", content.Text, StringComparison.Ordinal);
    }

    // The same bug the summary lines were fixed for once already, one front end further along: a
    // mail that said "1 added" for a dry run would send someone to the backoffice to look for a
    // block that was never created.
    [Fact]
    public void A_dry_run_says_what_would_be_added_and_that_nothing_changed()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(
            dryRun: true,
            outcome: new MergeOutcome(["_ga"], [], [], PolicyPageKey, false)));

        Assert.Contains("1 would be added", content.Text);
        Assert.Contains("Dry run - the policy page was not changed", content.Text);
        Assert.DoesNotContain("1 added,", content.Text);
    }

    [Fact]
    public void A_saved_page_says_it_is_a_draft()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(
            dryRun: false,
            outcome: new MergeOutcome(["_ga"], [], [], PolicyPageKey, true)));

        Assert.Contains("1 added,", content.Text);
        Assert.Contains("saved as a DRAFT", content.Text);
    }

    // Report-only is a supported mode, not a fault, and the mail has to say which of the three
    // "no outcome" cases it was - the operator's next action differs for each.
    [Fact]
    public void A_report_only_scan_says_why_nothing_was_compared()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(canReachApi: false));

        Assert.Contains("report-only", content.Text);
    }

    [Fact]
    public void An_attempted_comparison_that_failed_is_not_reported_as_report_only()
    {
        ScanEmailContent content = ScanEmail.Compose(Result(canReachApi: true, outcome: null));

        Assert.Contains("attempted and it failed", content.Text);
        Assert.DoesNotContain("report-only", content.Text);
    }

    [Fact]
    public void Both_report_files_are_attached()
    {
        ScanEmailContent content = ScanEmail.Compose(Result());

        Assert.Equal(2, content.Attachments.Count);
        Assert.Contains(content.Attachments, file => file.FileName.EndsWith(".md", StringComparison.Ordinal));
        Assert.Contains(content.Attachments, file => file.FileName.EndsWith(".json", StringComparison.Ordinal));
    }

    // Named from the site and the instant rather than the fixed cookie-scan-report.md, because a
    // mailbox holding four of these needs to be able to tell them apart - and because two scans of
    // two sites must not arrive as two files with one name.
    [Fact]
    public void The_attachments_are_named_after_the_site_and_the_scan()
    {
        ScanEmailContent content = ScanEmail.Compose(Result());

        Assert.Contains(
            content.Attachments,
            file => file.FileName == "cookie-scan-example-com-20260830-135700.md");
    }

    // The markdown attached is byte-for-byte the report file, not a second rendering of it. A
    // recipient comparing the attachment against the file on the operator's machine has to see the
    // same document.
    [Fact]
    public void The_markdown_attachment_is_the_report_itself()
    {
        ScanResult result = Result();

        EmailAttachment markdown = ScanEmail.Compose(result).Attachments
            .Single(file => file.FileName.EndsWith(".md", StringComparison.Ordinal));

        Assert.Equal(ScanReportWriter.Markdown(result), Encoding.UTF8.GetString(markdown.Content));
    }

    [Fact]
    public void The_json_attachment_is_the_scan_itself()
    {
        ScanResult result = Result();

        EmailAttachment json = ScanEmail.Compose(result).Attachments
            .Single(file => file.FileName.EndsWith(".json", StringComparison.Ordinal));

        Assert.Equal(ScanJson.Serialize(result), Encoding.UTF8.GetString(json.Content));
    }

    // A history file written before ScanOptionsSummary existed loads with a null Options. Mailing
    // one must say "not recorded" rather than claim a page count nobody chose.
    [Fact]
    public void A_scan_with_no_recorded_options_says_so_rather_than_inventing_them()
    {
        ScanResult result = Result() with { Options = null };

        string markdown = ScanReportWriter.Markdown(result);

        Assert.Contains("- Pages per pass: not recorded", markdown);
        Assert.Contains("- Member dimension: not recorded", markdown);
    }

    [Fact]
    public void A_scan_with_recorded_options_renders_them()
    {
        string markdown = ScanReportWriter.Markdown(
            Result(options: new ScanOptionsSummary(40, Locale.En, true, true)));

        Assert.Contains("- Pages per pass: up to 40", markdown);
        Assert.Contains("- Member dimension: yes", markdown);
        Assert.Contains("- Site: https://example.com/", markdown);
    }

    // The whole reason WriteFiles and Markdown share one builder. A scan mailed from the History
    // page and the same scan's report on disk must not be two different documents.
    [Fact]
    public void The_written_report_and_the_composed_one_are_the_same_document()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"cookiescan-email-{Guid.NewGuid():N}");

        try
        {
            ScanOptions options = new(
                Url: new Uri("https://example.com/"),
                Target: new Uri("https://example.com/"),
                MaxPages: 25,
                Locale: Locale.Sv,
                MemberEmail: null,
                MemberPassword: null,
                ClientId: "cookie-scanner",
                ClientSecret: "secret-for-this-test-only",
                DryRun: true,
                ReportDir: folder,
                Headed: false);

            ScanResult result = Result();

            ScanReportWriter.WriteFiles(options, result);

            Assert.Equal(
                File.ReadAllText(Path.Combine(folder, "cookie-scan-report.md")),
                ScanReportWriter.Markdown(result));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
