using System.Text.Json;

using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Desktop;

/// <summary>
/// One message from the page to the host, already turned into the command it names.
/// </summary>
/// <remarks>
/// Both directions speak <see cref="ScanJson.Options"/> - the same camelCase, enums-as-names dialect
/// the report file is written in - so there is one place to change how the two sides talk, and a
/// <c>ScanResult</c> posted to the page is byte-for-byte the document the report holds.
/// </remarks>
public abstract record DashboardCommand
{
    /// <summary>
    /// Reads the <c>type</c> discriminator and deserialises the record it names, or returns null.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception, for anything unrecognised or unparseable. The page is inside
    /// the exe, so a message this method cannot read is a bug rather than an attack - but it arrives
    /// on the WebView2 message loop, where an exception takes the loop down and with it every later
    /// message. A dropped message is the smaller failure.
    /// </remarks>
    public static DashboardCommand? Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind is not JsonValueKind.Object
                || document.RootElement.TryGetProperty("type", out JsonElement type) is false
                || type.ValueKind is not JsonValueKind.String)
            {
                return null;
            }

            return type.GetString() switch
            {
                // The records carrying nothing are constructed rather than deserialised: there is
                // no payload to read, and a constructor cannot fail on a member that is not there.
                "cancel" => new CancelCommand(),
                "listHistory" => new ListHistoryCommand(),
                "ready" => new ReadyCommand(),
                "run" => JsonSerializer.Deserialize<RunCommand>(json, ScanJson.Options),
                "loadScan" => JsonSerializer.Deserialize<LoadScanCommand>(json, ScanJson.Options),
                "compare" => JsonSerializer.Deserialize<CompareCommand>(json, ScanJson.Options),
                "saveSite" => CompleteSave(JsonSerializer.Deserialize<SaveSiteCommand>(json, ScanJson.Options)),
                "deleteSite" => CompleteDelete(JsonSerializer.Deserialize<DeleteSiteCommand>(json, ScanJson.Options)),
                "deleteScan" => CompleteDeleteScan(JsonSerializer.Deserialize<DeleteScanCommand>(json, ScanJson.Options)),
                "clearScans" => new ClearScansCommand(),
                "saveEmail" => CompleteSaveEmail(JsonSerializer.Deserialize<SaveEmailCommand>(json, ScanJson.Options)),
                "testEmail" => CompleteTestEmail(JsonSerializer.Deserialize<TestEmailCommand>(json, ScanJson.Options)),
                "sendEmail" => CompleteSendEmail(JsonSerializer.Deserialize<SendEmailCommand>(json, ScanJson.Options)),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }

        // System.Text.Json fills a constructor parameter the message does not carry with default, so
        // a saveSite with no `profile` - or a deleteSite with no `url` - deserialises into a command
        // holding a null the record's own type says cannot be there. Checked here rather than at the
        // handlers because these are the commands that end in a write: every other command's
        // missing member costs a scan, and a deleteSite with no URL would match no profile, remove
        // nothing, drop the selection and rewrite the file to say so.
        //
        // deleteScan is here for the same reason and a sharper one: it ends in File.Delete. A null
        // path matches nothing in ScanHistory.Delete's own listing check, so both guards would have
        // to fail before anything happened - which is the point of having both.
        //
        // Separate names rather than one overloaded set, because local functions cannot be overloaded.
        static SaveSiteCommand? CompleteSave(SaveSiteCommand? command)
            => command is { Profile: not null } ? command : null;

        static DeleteSiteCommand? CompleteDelete(DeleteSiteCommand? command)
            => command is { Url: not null } ? command : null;

        static DeleteScanCommand? CompleteDeleteScan(DeleteScanCommand? command)
            => command is { Path: not null } ? command : null;

        // saveEmail ends in a write to settings.json, so it is guarded like saveSite: a message with
        // no `account` would otherwise store an account of nulls over a working one.
        static SaveEmailCommand? CompleteSaveEmail(SaveEmailCommand? command)
            => command is { Account: not null } ? command : null;

        // These two end in a message leaving the machine rather than in a write, which is the same
        // class of thing: unrecoverable once done. A send with no recipient is refused here rather
        // than at the SMTP conversation, so nothing connects to a server to find out.
        static TestEmailCommand? CompleteTestEmail(TestEmailCommand? command)
            => command is { To: not null } ? command : null;

        static SendEmailCommand? CompleteSendEmail(SendEmailCommand? command)
            => command is { To: not null } ? command : null;
    }
}

/// <summary>Start a scan with the options the page currently shows.</summary>
/// <remarks>
/// <c>Locale</c> is the enum's name as a string rather than the enum itself: this record is the wire
/// format, and a page sending a locale this build has never heard of should be one warning line
/// rather than a message the whole loop cannot parse.
/// <para>
/// A run carries the fields the form currently shows rather than the name of a saved profile, even
/// though the two are usually the same: what runs must be what is on screen, and a run that fetched
/// its own options from the settings would scan something other than what the operator was reading.
/// The profile is written FROM the run afterwards - see <see cref="ScanSession"/> - never the other
/// way round.
/// </para>
/// <para>
/// <c>ClientSecret</c> is nullable like the credentials beside it, and the distinction costs more
/// here than for any of them: <see cref="ScanSession.StartAsync"/> lets the machine's environment
/// variable fill in exactly when this is blank. "The page sent no field" and "the page sent an empty
/// secret" therefore have to mean the same thing - and they do, because both are what a run started
/// with an empty box looks like, and both are the case the fallback exists for.
/// </para>
/// </remarks>
public sealed record RunCommand(
    string Url, int MaxPages, string Locale, string? MemberEmail,
    string? MemberPassword, string? ClientId, string? ClientSecret, bool DryRun,
    // Defaulted, so a page from a build that predates the field still parses into a run rather than
    // being dropped by the loop. Null means "the cookie-consent default", which is what almost every
    // site is.
    string? ConsentCookie = null,
    // The email trio, defaulted for the same reason as the field above it. A run carries them
    // because a run WRITES the profile it ran with - see ScanSession.Remembered - so a form whose
    // recipients had been edited and not saved would otherwise have those edits discarded by the
    // scan that used them. EmailEnabled defaults to false: a page that does not know about email
    // must not start a run that mails anybody.
    bool EmailEnabled = false,
    string? EmailTo = null,
    string? EmailCc = null) : DashboardCommand;

/// <summary>Save the run card's current values as the profile for the URL they name.</summary>
/// <remarks>
/// The whole profile travels in one member rather than as eight loose fields, so the record the page
/// sends and the record the file stores are the same type - a field added to
/// <see cref="SiteProfile"/> later reaches the page's message without a second declaration to keep
/// in step. Its <c>Locale</c> is therefore the enum itself, unlike <see cref="RunCommand"/>'s: a
/// spelling this build cannot read is worth refusing at the parse when the next thing that happens
/// is a write to disk.
/// </remarks>
public sealed record SaveSiteCommand(SiteProfile Profile) : DashboardCommand;

/// <summary>Forget the profile saved for one URL.</summary>
public sealed record DeleteSiteCommand(string Url) : DashboardCommand;

/// <summary>Delete one kept scan, by the path a history answer gave the page.</summary>
/// <remarks>
/// The path is not trusted on arrival: <see cref="ScanHistory.Delete"/> matches it against the
/// folder's own listing first, so the only paths this can reach are ones the host itself reported.
/// </remarks>
public sealed record DeleteScanCommand(string Path) : DashboardCommand;

/// <summary>Delete every kept scan. The page asks the operator first; the host does not.</summary>
public sealed record ClearScansCommand : DashboardCommand;

/// <summary>Store the machine's one mail account, as the Email page currently shows it.</summary>
/// <remarks>
/// The whole account travels in one member for the reason <see cref="SaveSiteCommand"/> gives about
/// the profile: the record the page sends and the record the file stores are the same type, so a
/// field added to <see cref="EmailAccount"/> later needs no second declaration to keep in step. Its
/// <c>Security</c> is therefore the enum itself, and a spelling this build cannot read is refused at
/// the parse - which is right, because the next thing that happens is a write to disk.
/// </remarks>
public sealed record SaveEmailCommand(EmailAccount Account) : DashboardCommand;

/// <summary>Send the message that proves the account works.</summary>
/// <remarks>
/// Carries the account as well as the address, so the button tests what is ON SCREEN rather than
/// what was last saved. Testing the stored account would make "change the port, press Test" report
/// on the old port - and the whole point of the button is to try a setting before committing to it.
/// Null means "use what is saved", which is what the button sends when nothing has been edited.
/// </remarks>
public sealed record TestEmailCommand(string To, EmailAccount? Account = null) : DashboardCommand;

/// <summary>Mail one scan's report to the addresses named.</summary>
/// <remarks>
/// <c>Path</c> identifies the scan and is optional: a path names a kept scan out of the history
/// folder, and null means the run this session has most recently completed. The second case is not a
/// convenience - it is the one that has to work when the history write itself failed, which is
/// exactly when the operator most wants the findings out of the window and into a mailbox.
/// <para>
/// The recipients travel on the message rather than being looked up here from the profile. What is
/// sent must be what the operator was reading, for the same reason <see cref="RunCommand"/> carries
/// the form's own fields: a send that fetched its own recipient list could mail a different set of
/// people from the one shown next to the button that was pressed.
/// </para>
/// </remarks>
public sealed record SendEmailCommand(string To, string? Cc = null, string? Path = null) : DashboardCommand;

public sealed record CancelCommand : DashboardCommand;
public sealed record ListHistoryCommand : DashboardCommand;
public sealed record LoadScanCommand(string Path) : DashboardCommand;
public sealed record CompareCommand(string PathA, string PathB) : DashboardCommand;
public sealed record ReadyCommand : DashboardCommand;

/// <summary>
/// The envelopes the host sends back that more than one place has to build.
/// </summary>
/// <remarks>
/// Only <c>sites</c> qualifies today: the form answers <c>saveSite</c> and <c>deleteSite</c> with it,
/// and <see cref="ScanSession"/> posts it as a run STARTS - right after the upsert that records what
/// the run is about to do, and long before the run finishes. Two anonymous objects spelling the same
/// envelope in two files is exactly the drift the page cannot be told about - a renamed member would
/// leave the dropdown empty after a run and full after a save, with nothing to compile against. Every
/// other answer is built where it is posted, because every other answer has one caller.
/// </remarks>
public static class DashboardAnswer
{
    public static object Sites(DashboardSettings settings)
        => new { type = "sites", sites = settings.Sites, selectedUrl = settings.SelectedUrl };

    /// <summary>
    /// The mail account as stored, for the Email page's fields.
    /// </summary>
    /// <remarks>
    /// A second entry here for the same reason as the first: it is built by the <c>ready</c> answer
    /// and again by <c>saveEmail</c>, and two anonymous objects spelling one envelope is drift with
    /// nothing to compile against.
    /// <para>
    /// The password goes out decrypted, exactly as the profiles' do, and for the same reason: the
    /// page fills its own masked field from it and posts back what the field holds. The envelope
    /// never leaves the process - WebView2 hands it to a renderer inside this exe, over no socket and
    /// no origin anything else can reach.
    /// </para>
    /// </remarks>
    public static object Email(DashboardSettings settings)
        => new { type = "email", account = settings.Email };
}
