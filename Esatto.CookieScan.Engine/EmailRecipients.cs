namespace Esatto.CookieScan.Engine;

/// <summary>
/// One typed recipient field, split into the addresses it names.
/// </summary>
/// <remarks>
/// Recipients are stored as the one string the operator typed rather than as a list, so the settings
/// file stays hand-editable and the form stays one text box. This is the only place that string is
/// turned into addresses, so the auto-send, the manual send and the test message cannot disagree
/// about what "a, b; c" means.
/// <para>
/// It deliberately does NOT validate. There is exactly one rule for whether a string is a mailbox -
/// the one MailKit applies when it builds the message - and inventing a second grammar here would
/// mean an address this class rejected and the mail server would have accepted, silently dropped
/// with nothing to show for it. Splitting is this class's whole job; see <c>EmailSender</c> for what
/// happens to an entry that will not parse.
/// </para>
/// </remarks>
public static class EmailRecipients
{
    /// <summary>Every address a field names, trimmed, in the order typed, without duplicates.</summary>
    /// <remarks>
    /// Newlines are separators alongside the comma and the semicolon: the field is a single-line
    /// input in the window, but the settings file is a file people paste into, and a list pasted out
    /// of a mail client arrives one address per line.
    /// <para>
    /// Case-insensitive de-duplication, because a domain is case-insensitive and a list holding
    /// <c>Legal@client.se</c> beside <c>legal@client.se</c> would otherwise mail the same person
    /// twice. The first spelling is the one kept - it is the one the operator typed first.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Parse(string? typed)
        => string.IsNullOrWhiteSpace(typed)
            ? []
            : [.. typed
                .Split(
                    [',', ';', '\n', '\r'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
}
