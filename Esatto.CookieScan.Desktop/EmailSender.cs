using Esatto.CookieScan.Engine;

using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

namespace Esatto.CookieScan.Desktop;

/// <summary>What one attempt to send came to, in a sentence the log panel can print.</summary>
/// <remarks>
/// A result rather than an exception, because every caller is a message handler with no user to
/// apologise to - the same reason <see cref="ScanSession.StartAsync"/> throws nothing. A mail that
/// did not go is a warning line beside a scan that did work, never a scan reported as failed.
/// </remarks>
public sealed record EmailOutcome(bool Sent, string Message);

/// <summary>
/// The transport: one SMTP conversation, and nothing about what a scan means.
/// </summary>
/// <remarks>
/// The only file in the solution that knows what MailKit is, and the reason the reference sits on
/// this project rather than on the engine: the desktop exe is the one half of the scanner that is
/// not packed, so a dependency added here reaches nobody who references
/// <c>Esatto.CookieScan.Engine</c> for its scanner. The message itself is composed by
/// <see cref="ScanEmail"/>, over there, with no idea how it will travel.
/// <para>
/// MailKit rather than <c>System.Net.Mail.SmtpClient</c>, which Microsoft's own documentation says
/// not to use for new work: it cannot do implicit TLS on 465 at all, and its failures arrive as
/// "Failure sending mail" with the reason on an inner exception nobody sees.
/// </para>
/// </remarks>
public static class EmailSender
{
    /// <summary>
    /// How long a hung server is allowed to hold the send, in milliseconds.
    /// </summary>
    /// <remarks>
    /// MailKit's own default is two minutes, which is two minutes of a log panel saying nothing
    /// after a mistyped host. A minute is long enough for a real server on a slow link with two
    /// attachments on the wire, and short enough that a wrong port reports itself while the operator
    /// is still looking at the window.
    /// </remarks>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>Sends one scan's report.</summary>
    public static Task<EmailOutcome> SendAsync(
        EmailAccount account,
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        ScanEmailContent content,
        CancellationToken token = default)
        => DeliverAsync(
            account, to, cc, content.Subject, content.Html, content.Text, content.Attachments, token);

    /// <summary>
    /// Sends the message that proves the account works, and nothing else.
    /// </summary>
    /// <remarks>
    /// Its own entry point rather than a fabricated <see cref="ScanResult"/> put through
    /// <see cref="SendAsync"/>: a test message that looked like a scan report is one that can be
    /// mistaken for a finding about a real site, and there is no site involved here at all.
    /// </remarks>
    public static Task<EmailOutcome> SendTestAsync(
        EmailAccount account, IReadOnlyList<string> to, CancellationToken token = default)
        => DeliverAsync(
            account,
            to,
            [],
            "Esatto cookie scanner - test message",
            "<!doctype html><html><body style=\"font-family:-apple-system,'Segoe UI',Arial,sans-serif;\">"
            + "<p>This is a test message from the Esatto cookie scanner.</p>"
            + "<p>If you are reading it, the SMTP settings on this machine work and scan reports will "
            + "reach this address.</p></body></html>",
            "This is a test message from the Esatto cookie scanner.\r\n\r\n"
            + "If you are reading it, the SMTP settings on this machine work and scan reports will "
            + "reach this address.\r\n",
            [],
            token);

    private static async Task<EmailOutcome> DeliverAsync(
        EmailAccount account,
        IReadOnlyList<string> to,
        IReadOnlyList<string> cc,
        string subject,
        string html,
        string text,
        IReadOnlyList<EmailAttachment> attachments,
        CancellationToken token)
    {
        if (account.IsConfigured is false)
        {
            return new EmailOutcome(
                false, "No SMTP server is set up. Fill in the host and the from address on the Email page.");
        }

        if (MailboxAddress.TryParse(account.FromAddress.Trim(), out MailboxAddress? from) is false)
        {
            return new EmailOutcome(
                false, $"'{account.FromAddress.Trim()}' is not an address the message can be sent from.");
        }

        from.Name = account.FromName.Trim();

        // Every address is parsed before anything connects, so a typo costs a sentence rather than a
        // round trip to a server that then refuses it with a numeric code.
        (List<MailboxAddress> toBoxes, List<string> badTo) = Boxes(to);
        (List<MailboxAddress> ccBoxes, List<string> badCc) = Boxes(cc);

        if (toBoxes.Count == 0)
        {
            return new EmailOutcome(
                false,
                badTo.Count == 0
                    ? "There is nobody to send this to. Add a recipient to the site's profile."
                    : $"No valid recipient address among: {string.Join(", ", badTo)}");
        }

        MimeMessage message = new();

        message.From.Add(from);
        message.To.AddRange(toBoxes);
        message.Cc.AddRange(ccBoxes);
        message.Subject = subject;

        BodyBuilder body = new() { HtmlBody = html, TextBody = text };

        foreach (EmailAttachment attachment in attachments)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.MediaType));
        }

        message.Body = body.ToMessageBody();

        try
        {
            using SmtpClient client = new() { Timeout = TimeoutMilliseconds };

            await client.ConnectAsync(account.Host.Trim(), account.Port, Options(account.Security), token);

            // Only when there is one. An anonymous internal relay refuses a client that offers
            // credentials it never asked for, so signing in unconditionally would break exactly the
            // setup that needs no password.
            if (string.IsNullOrWhiteSpace(account.Username) is false)
            {
                await client.AuthenticateAsync(account.Username.Trim(), account.Password, token);
            }

            await client.SendAsync(message, token);
            await client.DisconnectAsync(true, token);
        }
        catch (OperationCanceledException)
        {
            return new EmailOutcome(false, "Sending was cancelled.");
        }
        catch (AuthenticationException error)
        {
            // Called out rather than folded into the general case: it is the failure with a different
            // fix, and "the server rejected the sign-in" sends someone to the password box instead of
            // to the host field.
            return new EmailOutcome(
                false, $"The mail server rejected the sign-in for {account.Username.Trim()}: {error.Message}");
        }
        catch (Exception error)
        {
            // Broad on purpose. MailKit reports a wrong port, a wrong security mode, a certificate it
            // will not accept, a refused recipient and a dead socket as five unrelated exception
            // types, and the operator's next move is the same for all of them: read the sentence and
            // change a field. Nothing here may escape - the caller is a message handler, and an
            // unhandled throw takes the WebView2 loop down and the running scan's log with it.
            return new EmailOutcome(false, $"The mail could not be sent: {error.Message}");
        }

        string sent = $"Sent to {string.Join(", ", toBoxes.Select(box => box.Address))}"
            + (ccBoxes.Count == 0 ? "" : $" (cc {string.Join(", ", ccBoxes.Select(box => box.Address))})")
            + ".";

        // Reported as a success WITH a complaint rather than as a failure: the mail did go, and the
        // operator still needs to know that one of the names they typed did not travel with it.
        List<string> skipped = [.. badTo, .. badCc];

        return new EmailOutcome(
            true,
            skipped.Count == 0 ? sent : $"{sent} Skipped, not an address: {string.Join(", ", skipped)}");
    }

    /// <summary>The addresses that parse, and the strings that did not.</summary>
    private static (List<MailboxAddress> Boxes, List<string> Rejected) Boxes(IReadOnlyList<string> addresses)
    {
        List<MailboxAddress> boxes = [];
        List<string> rejected = [];

        foreach (string address in addresses)
        {
            if (MailboxAddress.TryParse(address, out MailboxAddress? box))
            {
                boxes.Add(box);
            }
            else
            {
                rejected.Add(address);
            }
        }

        return (boxes, rejected);
    }

    private static SecureSocketOptions Options(EmailSecurity security) => security switch
    {
        EmailSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        EmailSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.StartTls,
    };
}
