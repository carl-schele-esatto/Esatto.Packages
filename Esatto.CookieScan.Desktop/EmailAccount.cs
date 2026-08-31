using System.Text.Json.Serialization;

namespace Esatto.CookieScan.Desktop;

/// <summary>How the connection to the SMTP server is secured.</summary>
/// <remarks>
/// Three, because there are three in the wild and picking wrongly fails in a way that is hard to
/// read: implicit TLS on 587 hangs, STARTTLS on 465 returns bytes that are not a greeting. The
/// window offers them as words rather than as a port number to guess from.
/// <para>
/// <see cref="StartTls"/> is first, so the enum's default is the one that goes with the default port
/// - a settings file missing the key reads as 587 + STARTTLS, which is what almost every server
/// wants.
/// </para>
/// </remarks>
public enum EmailSecurity
{
    /// <summary>Plain connect, then upgrade. Port 587, and what almost every server wants.</summary>
    StartTls,

    /// <summary>TLS from the first byte. Port 465.</summary>
    SslOnConnect,

    /// <summary>No TLS at all. An internal relay on 25, and nothing that crosses a network.</summary>
    None,
}

/// <summary>
/// The mailbox the window sends from: one per machine, not one per site.
/// </summary>
/// <remarks>
/// The split between this and the recipients on <see cref="SiteProfile"/> is the whole shape of the
/// feature. This is who the operator IS - typed once, on the Email page - and the recipients are a
/// fact about the client being scanned. Putting the server in the profile instead would mean typing
/// six fields again for every site added, to say the same thing every time.
/// <para>
/// <see cref="Username"/> and <see cref="Password"/> are plaintext HERE and ciphertext on disk, like
/// the four credentials on a profile and through the same <see cref="ProtectedText"/>. The host, the
/// port, the security mode and the from address are stored in clear: they are not credentials, and a
/// wrong one of them is a mistake somebody has to be able to spot by opening the file - the same
/// argument <see cref="SiteProfile.ConsentCookie"/> is stored in clear for.
/// </para>
/// <para>
/// Both credentials may be empty, and that is a supported configuration rather than an unfinished
/// one: an internal relay that accepts mail from the machine's own subnet takes no AUTH at all, and
/// offering it one is how such a relay refuses the connection. See <c>EmailSender</c>, which signs
/// in only when there is a username.
/// </para>
/// </remarks>
public sealed record EmailAccount(
    string Host = "",
    int Port = 587,
    EmailSecurity Security = EmailSecurity.StartTls,
    // Positional order is the order the keys are written in the settings file, and appending rather
    // than inserting keeps a hand-read file comparable across builds - the same rule SiteProfile's
    // own comment states.
    string Username = "",
    string Password = "",
    string FromAddress = "",
    string FromName = "")
{
    /// <summary>Whether there is enough here to attempt a send.</summary>
    /// <remarks>
    /// A host and a from address, and deliberately nothing else. The credentials are optional by
    /// design (see the class remark), and the port and security mode always have a usable default -
    /// so those two fields are the only ones whose absence means "not set up yet" rather than "left
    /// at the default".
    /// <para>
    /// Not serialised: it is a question about the record, not a fact to store and let drift.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public bool IsConfigured
        => string.IsNullOrWhiteSpace(Host) is false && string.IsNullOrWhiteSpace(FromAddress) is false;
}
