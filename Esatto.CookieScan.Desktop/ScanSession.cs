using Esatto.CookieScan.Core;
using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Desktop;

/// <summary>
/// One scan: the options it runs with, the task it runs on, and the files it leaves behind.
/// </summary>
/// <remarks>
/// Owns a run and nothing else. It knows the page only through <see cref="DashboardBridge"/>, so the
/// same session drives the log, the result and the running state without ever touching a control.
/// <para>
/// The one thing it owns beyond the run is a share of <paramref name="settings"/> - the same
/// instance <see cref="DashboardForm"/> holds, not a copy. A run saves the profile it ran with, and
/// two instances would be two lists overwriting each other's file. Everything this class does to it
/// happens on the UI thread, before the scan reaches <c>Task.Run</c>; see the remark on
/// <see cref="DashboardSettings"/> for what that buys and what would break it.
/// </para>
/// </remarks>
public sealed class ScanSession(DashboardBridge bridge, DashboardSettings settings)
{
    private readonly WebViewScanLog log = new(bridge);

    private CancellationTokenSource? cancellation;

    /// <summary>
    /// The last scan this session finished, for the result card's own Send email button.
    /// </summary>
    /// <remarks>
    /// Held here rather than looked up out of the history folder by path, and the difference matters
    /// in exactly the case the button is most wanted: a scan whose history entry could not be written
    /// has no path to be found by, and it is the scan whose findings most need a way out of this
    /// window. Set from the result the page was shown, so the two cannot be different scans.
    /// <para>
    /// Not cleared when a new run starts. A run that fails leaves the previous result on screen and
    /// mailable, which is the honest reading - the button sends what the card is showing.
    /// </para>
    /// </remarks>
    public ScanResult? LastResult { get; private set; }

    /// <summary>
    /// Where the window writes its report files.
    /// </summary>
    /// <remarks>
    /// Not the current directory, which is what the console tool defaults to. A window's current
    /// directory is wherever it happened to be launched from - a desktop shortcut leaves it at the
    /// system directory - so reports would scatter or fail to write. The last two lines of
    /// <see cref="ScanReportWriter.SummaryLines"/> name both files and reach the page on the result
    /// envelope, so the operator is still told where they went.
    /// </remarks>
    private static string ReportDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Esatto.CookieScan",
        "reports");

    /// <summary>Runs one scan, reporting everything it does through the bridge.</summary>
    /// <remarks>
    /// Nothing is thrown out of here. The caller is a message handler with no user to apologise to,
    /// so a failure is a warning line in the page's log and a running state the page can trust.
    /// </remarks>
    public async Task StartAsync(RunCommand command)
    {
        if (cancellation is not null)
        {
            log.Warning("A scan is already running.");

            return;
        }

        ScanOptions options;

        try
        {
            options = BuildOptions(command);
        }
        catch (ArgumentException error)
        {
            // Nothing started, so the page is put back to idle rather than left waiting for a run
            // that never began.
            log.Warning(error.Message);
            bridge.Post(new { type = "state", running = false });

            return;
        }

        // Remembered as soon as the options are known good, rather than only when the scan works. A
        // scan that fails is exactly when the operator has typed something worth not losing - a URL
        // that turned out to resolve to nothing, a client id being tried for the first time - and
        // that was the run that used to discard it. Not before the check above, though: a URL this
        // window has just refused is not one to hand back at every later launch, and is certainly
        // not one to add to the dropdown.
        //
        // This is the same upsert the Save site button performs, from the same values, so running a
        // scan against a URL with no profile yet creates one: "remember what was typed" and "save
        // this site" were always the same act, and now they are the same code. The client secret is
        // among the values now, but only ever the typed one - see Remembered for why the
        // environment's must not be written here.
        settings.Upsert(Remembered(command));
        settings.SelectedUrl = command.Url.Trim();
        settings.Save();

        // Answered as well as written, so the dropdown is never a relaunch behind the file. Without
        // this, a scan of a new URL would save a profile the operator cannot see, select, or delete
        // until the window is restarted.
        bridge.Post(DashboardAnswer.Sites(settings));

        bridge.Post(new { type = "state", running = true });

        cancellation = new CancellationTokenSource();

        // Copied out of the field: the field is nullable and reassigned per run, and the lambda
        // below outlives the statement that assigned it.
        CancellationToken token = cancellation.Token;

        try
        {
            // Task.Run so Playwright's synchronous startup cannot block the UI thread.
            ScanResult? result = await Task.Run(
                () => new ScanRunner(options, () => CatalogueSource.Load(log, options.ConsentCookie), log).RunAsync(token),
                token);

            if (result is null)
            {
                log.Warning("The scan found no pages, so there is nothing to report.");

                return;
            }

            // Set before the result is posted, so the page cannot show a Send email button for a scan
            // this session could not then produce. See the property's own remark for why the result is
            // kept here rather than found again by path.
            LastResult = result;

            // Posted before anything is written, and carrying the summary lines with it. A report
            // file left open in an editor, or a full disk, used to throw straight past this and cost
            // the operator the findings of a scan that had actually succeeded. The summary lines name
            // the paths the writes below are about to create, so the counts reach the page even when
            // one of those writes then fails.
            bridge.Post(new
            {
                type = "result",
                scan = result,
                summary = ScanReportWriter.SummaryLines(options, result),
            });

            // Two blocks, not one: a locked report file must not cost the history entry - history is
            // written "in addition to" the report directory rather than after it - and neither may
            // cost the result the page already has. Narrow on purpose, unlike the settings write:
            // these files are the point of the exercise, so anything other than the disk refusing
            // them should still reach the handler below.
            try
            {
                ScanReportWriter.WriteFiles(options, result);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                log.Warning($"The scan finished, but its report could not be written: {error.Message}");
            }

            try
            {
                ScanHistory.Save(result);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                log.Warning($"The scan finished, but its history entry could not be written: {error.Message}");
            }

            // The third of the three things a finished scan leaves behind, in the same shape as the
            // two above it: a mail that will not send is a warning line and nothing else, never a
            // scan reported as failed. Last of the three because a report is worth recording before
            // it is announced - though the message itself is composed from the result rather than
            // from either file, so neither write failing costs the mail its attachments.
            //
            // Its own method rather than a fourth try block, because unlike the two writes above it
            // has decisions to make before it does anything: whether this site asked for a mail,
            // whether anyone is named, and whether the machine can send one at all.
            await MailAsync(command, result, token);
        }
        catch (OperationCanceledException)
        {
            // Before the general handler, which would otherwise report a cancel as a failure. A
            // cancelled scan writes no report and produces no result: a partial scan presented as a
            // complete one would be worse than no scan at all.
            log.Warning("Cancelled. No report was written.");
        }
        catch (Exception error)
        {
            log.Warning($"The scan failed: {error.Message}");
        }
        finally
        {
            bridge.Post(new { type = "state", running = false });

            // Disposed here and nowhere else: everything outside the awaited task runs on the UI
            // thread, so no cancel message can interleave and reach a disposed source. Nulled so the
            // next run cannot be handed the spent one.
            cancellation.Dispose();
            cancellation = null;
        }
    }

    /// <summary>
    /// Mails a finished scan's report, if the site asked for one and the machine can send it.
    /// </summary>
    /// <remarks>
    /// Every reason not to send is a line in the log rather than silence. A site whose profile says
    /// "email this report" and then does not is the failure mode worth spending three warnings on:
    /// the operator has ticked a box, walked away, and would otherwise learn that nothing was sent
    /// only from the recipient never mentioning it.
    /// <para>
    /// Under the run's own cancellation, so closing the window during a send does not leave an SMTP
    /// conversation holding the process open. That cannot be mistaken for a cancelled scan:
    /// <see cref="EmailSender"/> catches the cancel itself and answers with an outcome, so nothing
    /// reaches the handler that reports a run as abandoned.
    /// </para>
    /// </remarks>
    private async Task MailAsync(RunCommand command, ScanResult result, CancellationToken token)
    {
        if (command.EmailEnabled is false)
        {
            return;
        }

        IReadOnlyList<string> to = EmailRecipients.Parse(command.EmailTo);

        if (to.Count == 0)
        {
            log.Warning(
                "This site is set to email its report, but no recipient is filled in. Nothing was sent.");

            return;
        }

        if (settings.Email is not { IsConfigured: true } account)
        {
            log.Warning(
                "This site is set to email its report, but this machine has no mail server set up. "
                + "Fill in the Email page. Nothing was sent.");

            return;
        }

        // The outcome is dropped here on purpose: an automatic send has already said everything it
        // has to say in the log, and the page it would answer is the one with the log on it.
        _ = await SendAsync(account, to, EmailRecipients.Parse(command.EmailCc), result, token);
    }

    /// <summary>Mails one scan on demand - the result card's button, or a row on the History page.</summary>
    /// <remarks>
    /// The same send the automatic path takes, from the same account and through the same sender, so
    /// a report that arrives by hand is the report that would have arrived by itself.
    /// <para>
    /// Answered on an envelope as well as logged, which the automatic path is not. The History page
    /// has no log panel to print into - a send from a row there would otherwise report itself two
    /// pages away, on a screen the operator is not looking at. The envelope echoes
    /// <see cref="SendEmailCommand.Path"/> so the page can tell a send from a row apart from a send
    /// from the result card, which does have a log.
    /// </para>
    /// </remarks>
    public async Task SendAsync(SendEmailCommand command, ScanResult result)
    {
        IReadOnlyList<string> to = EmailRecipients.Parse(command.To);

        if (to.Count == 0)
        {
            Answer(command, new EmailOutcome(
                false, "There is nobody to send this to. Fill in a recipient and try again."));

            return;
        }

        if (settings.Email is not { IsConfigured: true } account)
        {
            Answer(command, new EmailOutcome(
                false, "This machine has no mail server set up, so nothing was sent. Fill in the Email page."));

            return;
        }

        EmailOutcome outcome = await SendAsync(
            account, to, EmailRecipients.Parse(command.Cc), result, CancellationToken.None);

        bridge.Post(new { type = "emailSent", path = command.Path, sent = outcome.Sent, message = outcome.Message });
    }

    /// <summary>One refusal, said in the log and beside the button that asked, before anything connects.</summary>
    private void Answer(SendEmailCommand command, EmailOutcome outcome)
    {
        log.Warning(outcome.Message);

        bridge.Post(new { type = "emailSent", path = command.Path, sent = outcome.Sent, message = outcome.Message });
    }

    /// <remarks>
    /// The one place a scan actually becomes a message. Both callers reach it with an account they
    /// have already checked and a recipient list they have already found to be non-empty, so the
    /// three states an operator sees - about to send, sent, would not send - are worded once.
    /// </remarks>
    private async Task<EmailOutcome> SendAsync(
        EmailAccount account,
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        ScanResult result,
        CancellationToken token)
    {
        log.Info($"Emailing the report to {string.Join(", ", to)}...");

        EmailOutcome outcome = await EmailSender.SendAsync(
            account, to, cc, ScanEmail.Compose(result), token);

        if (outcome.Sent)
        {
            log.Info(outcome.Message);
        }
        else
        {
            log.Warning(outcome.Message);
        }

        return outcome;
    }

    /// <remarks>
    /// Says so in the log, because the engine only observes a cancel between passes: without a line
    /// here the window looks like it ignored the click for the rest of the current pass.
    /// </remarks>
    public void Cancel()
    {
        if (cancellation is null)
        {
            return;
        }

        log.Info("Cancelling - the scan stops at the end of the pass it is running.");

        cancellation.Cancel();
    }

    /// <summary>
    /// Turns one message from the page into the same <see cref="ScanOptions"/> the command line
    /// would have built.
    /// </summary>
    /// <remarks>
    /// The URL is checked with <see cref="Uri.TryCreate"/> against <see cref="UriKind.Absolute"/>,
    /// which is the rule <see cref="ScanOptions.Parse"/> applies, and the message names the same
    /// likely cause - a URL pasted without its scheme. Only the wording differs, because a window
    /// telling someone to fix a flag it does not have would be nonsense. Two front ends accepting
    /// different URLs is a bug nobody could reproduce from the other one.
    /// </remarks>
    /// <exception cref="ArgumentException">The site URL is not absolute.</exception>
    private static ScanOptions BuildOptions(RunCommand command)
    {
        string url = command.Url.Trim();

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? root) is false)
        {
            throw new ArgumentException(
                $"'{url}' is not an absolute URL. It needs a scheme, for example https://example.com");
        }

        return new ScanOptions(
            Url: root,
            // The policy page lives on the site being scanned. --target exists for the case where it
            // does not, which is a console-tool concern: nothing in the window offers it, so the root
            // here is the deliberate answer rather than an omission.
            Target: root,
            MaxPages: Pages(command.MaxPages),
            Locale: ParseLocale(command.Locale),
            MemberEmail: Supplied(command.MemberEmail),
            MemberPassword: Supplied(command.MemberPassword),
            ClientId: Supplied(command.ClientId),
            // The profile's own secret wins; the environment fills in when the box is empty. That
            // order is the whole point of the change: each site registers its own API user, so the
            // secret that belongs to the site being scanned must beat the one that happens to be set
            // on this machine. The variable is still read - a machine that scans one site and has it
            // set keeps working with an empty box - but it is now the fallback rather than the
            // source.
            //
            // Blank counts as absent, exactly as it does for the three above: an operator who
            // cleared the box is asking for whatever the machine has, not for "no secret".
            ClientSecret: string.IsNullOrWhiteSpace(command.ClientSecret)
                ? Environment.GetEnvironmentVariable(ScanOptions.SecretVariable)
                : command.ClientSecret.Trim(),
            DryRun: command.DryRun,
            ReportDir: ReportDirectory,
            // Headless, always. --headed exists to debug the engine; a window that opened a second
            // visible browser on every run would be answering a question nobody asked.
            Headed: false,
            ConsentCookie: Supplied(command.ConsentCookie));

        // Blank means absent, matching the console tool, where a flag that was not passed is null.
        static string? Supplied(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The console tool's rule for a page count it cannot use: anything not positive means the
    /// default rather than a crawl of nothing.
    /// </summary>
    /// <remarks>
    /// One rule, read by both the options and the settings, because they must agree. A blank field
    /// reaches the host as zero by design; remembering that zero rather than the 25 that actually
    /// ran would put a 0 in the spinner at every later launch - a value the form's own min="1"
    /// cannot correct, since the settings are assigned rather than typed.
    /// <para>
    /// Public for the third caller: Save site writes a profile without running anything, from the
    /// same form and so with the same zero. Reached across from <see cref="DashboardForm"/> rather
    /// than copied there, because a second 25 would be a second rule the moment either moved.
    /// </para>
    /// </remarks>
    public static int Pages(int requested) => requested > 0 ? requested : 25;

    /// <summary>The run, as the profile for the site it ran against.</summary>
    /// <remarks>
    /// The page count is put through <see cref="Pages"/> here, so the profile holds what actually
    /// ran rather than what was typed: a blank max-pages field reaches the host as zero and the scan
    /// runs 25, and a profile that stored the zero would put it back in the spinner at every later
    /// launch, which <see cref="Pages"/>'s own remark is about.
    /// <para>
    /// The URL and the four credentials are handed over untrimmed, because
    /// <see cref="DashboardSettings.Upsert"/> trims every stored string itself. That is deliberate
    /// and not an omission: trimming on this path and not on the Save site button's was how the same
    /// form came to produce two different files depending on which one was pressed.
    /// <see cref="BuildOptions"/> trims what it signs in with independently, so what is stored is
    /// still exactly what the scan used.
    /// </para>
    /// <para>
    /// The client secret stored is <see cref="RunCommand.ClientSecret"/> - what the operator typed -
    /// and NEVER the effective secret <see cref="BuildOptions"/> computed. The two differ precisely
    /// when the box was empty and the machine's ESATTO_COOKIESCAN_CLIENT_SECRET filled in, and
    /// writing that value here would have the profile quietly absorb the machine's secret on the
    /// first run: the box would refill with dots at the next launch, the operator would believe the
    /// site had its own secret, and copying the file or moving to a machine with a different
    /// variable would then fail with a credential nobody remembers typing. A blank box stays blank
    /// on disk, and the fallback stays a fallback.
    /// </para>
    /// </remarks>
    private static SiteProfile Remembered(RunCommand command) => new(
        Url: command.Url,
        MaxPages: Pages(command.MaxPages),
        Locale: ParseLocale(command.Locale),
        DryRun: command.DryRun,
        MemberEmail: command.MemberEmail ?? "",
        MemberPassword: command.MemberPassword ?? "",
        ClientId: command.ClientId ?? "",
        ClientSecret: command.ClientSecret ?? "",
        ConsentCookie: command.ConsentCookie ?? "",
        EmailEnabled: command.EmailEnabled,
        EmailTo: command.EmailTo ?? "",
        EmailCc: command.EmailCc ?? "");

    /// <remarks>
    /// Swedish for anything unrecognised, which is the rule the console tool applies to --locale.
    /// The page sends the enum's name, and a name this build does not know is a page out of step
    /// with its host rather than a reason to refuse the run.
    /// </remarks>
    private static Locale ParseLocale(string locale)
        => Enum.TryParse(locale, ignoreCase: true, out Locale parsed) ? parsed : Locale.Sv;
}
