using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// The verdict on a pasted service-account key. It carries the outcome, a fixed error code and the key's public identity
/// (its client email), and nothing else: this is a record, so its generated <c>ToString()</c> prints every property, and a
/// property holding any of the key would put the key in a log line.
/// </summary>
/// <param name="IsValid">Whether the text is a usable service-account key.</param>
/// <param name="ErrorCode"><see langword="null"/> when valid, otherwise <see cref="ServiceAccountKey.InvalidCode"/>.</param>
/// <param name="ClientEmail">The service account's email when valid (the address to grant access to), otherwise <see langword="null"/>.</param>
public sealed record KeyCheck(bool IsValid, string? ErrorCode, string? ClientEmail);

/// <summary>
/// Checks and tidies a Google service-account key (the JSON file from the Google Cloud console) before it is stored or
/// used. The key is a secret, so nothing here ever puts any of the input into a return value, an exception or a log: every
/// failure is the single code <see cref="InvalidCode"/>, and a parse problem is swallowed rather than rethrown because the
/// parser's message can quote the text it choked on.
/// </summary>
public static class ServiceAccountKey
{
    /// <summary>The largest key accepted, in UTF-8 bytes. A real key is about 2.4 KB; the limit keeps a wrong paste cheap to refuse.</summary>
    public const int MaxBytes = 16 * 1024;

    /// <summary>The error code of every refusal.</summary>
    public const string InvalidCode = "keyInvalid";

    // Trailing commas are tolerated because hand-edited JSON often has them. A repeated property is not: with duplicates,
    // the copy this class validates could differ from the copy Google's loader ends up using.
    private static readonly JsonDocumentOptions ParseOptions = new() { AllowTrailingCommas = true, AllowDuplicateProperties = false };

    // Relaxed escaping keeps the re-emitted text readable (the PEM's '+' stays a '+'); it is still well-formed JSON.
    private static readonly JsonWriterOptions WriteOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Google's token endpoint: the only <c>token_uri</c> a key may name.</summary>
    public const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";

    /// <summary>The token endpoint in keys Google issued before 2020, which Google still honours: also Google's own, so also accepted.</summary>
    public const string LegacyGoogleTokenEndpoint = "https://accounts.google.com/o/oauth2/token";

    private const string TokenUriProperty = "token_uri";

    // What Google's loader needs from a service-account key, as Google writes the names. Everything else (token_uri, auth_uri,
    // universe_domain, the certificate URLs, anything added by hand) is dropped, so the loader uses Google's own defaults and a key
    // cannot send the signed assertion, or anything else, to an address of its choosing.
    private static readonly HashSet<string> KeptProperties = new(StringComparer.Ordinal)
    {
        "type", "project_id", "private_key_id", "private_key", "client_email", "client_id",
    };

    /// <summary>
    /// Whether <paramref name="json"/> is a service-account key: an object with <c>type</c> <c>service_account</c>, a
    /// <c>client_email</c> and a PEM <c>private_key</c>, at most <see cref="MaxBytes"/> bytes, and no <c>token_uri</c> other than
    /// <see cref="GoogleTokenEndpoint"/> or <see cref="LegacyGoogleTokenEndpoint"/> (an unrecognised one is refused although
    /// <see cref="TryNormalize(string?, out string)"/> drops it, so a key that tries to name another address is not accepted
    /// quietly). A BOM, surrounding whitespace,
    /// CRLF and a double-escaped private key are repaired first (see <see cref="TryNormalize"/>). The private key is not
    /// parsed as RSA here; that happens when the credential is built.
    /// </summary>
    public static KeyCheck Validate(string? json)
    {
        // Every UTF-16 char is at least one UTF-8 byte, so a longer string is over the limit without counting.
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxBytes || Encoding.UTF8.GetByteCount(json) > MaxBytes)
        {
            return Invalid;
        }

        if (!TryNormalize(json, out var normalized, out var tokenEndpointIsGoogles) || !tokenEndpointIsGoogles
            || !TryParse(normalized, out var document))
        {
            return Invalid;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!TryGetString(root, "type", out var type) || type != "service_account"
                || !TryGetString(root, "client_email", out var clientEmail) || string.IsNullOrWhiteSpace(clientEmail)
                || !TryGetString(root, "private_key", out var privateKey) || !LooksLikePemPrivateKey(privateKey))
            {
                return Invalid;
            }

            return new KeyCheck(true, null, clientEmail.Trim());
        }
    }

    /// <summary>
    /// Turns what a person pasted into the JSON Google's loader expects. Strips a leading BOM and surrounding whitespace,
    /// and repairs the <c>private_key</c> string: CRLF becomes LF, and when the key has no real newline but carries the two
    /// characters backslash and <c>n</c> (a key that was escaped twice on its way here), those become real newlines. Only the
    /// properties the loader needs are kept, in their order (<c>type</c>, <c>project_id</c>, <c>private_key_id</c>,
    /// <c>private_key</c>, <c>client_email</c>, <c>client_id</c>); every other one is dropped, so the loader falls back to
    /// Google's own endpoints whatever the text named.
    /// <para>
    /// This neither enforces <see cref="MaxBytes"/> nor requires any field: <see cref="Validate"/> does that. It returns
    /// <see langword="false"/> with <c>""</c> for anything that is not a JSON object, never throws, and never quotes the input.
    /// On success <paramref name="normalized"/> is the key itself, so treat it as a secret.
    /// </para>
    /// </summary>
    public static bool TryNormalize(string? json, out string normalized) => TryNormalize(json, out normalized, out _);

    /// <param name="json">The text, as for <see cref="TryNormalize(string?, out string)"/>.</param>
    /// <param name="normalized">As for <see cref="TryNormalize(string?, out string)"/>.</param>
    /// <param name="tokenEndpointIsGoogles">
    /// Whether the text names no token endpoint other than Google's: no <c>token_uri</c> at all (in any letter case: Google's
    /// loader matches names without regard to case), or exactly <see cref="GoogleTokenEndpoint"/> or <see cref="LegacyGoogleTokenEndpoint"/>.
    /// </param>
    private static bool TryNormalize(string? json, out string normalized, out bool tokenEndpointIsGoogles)
    {
        normalized = "";
        tokenEndpointIsGoogles = false;
        if (json is null) return false;

        var text = Trim(json);
        if (text.Length == 0 || !TryParse(text, out var document)) return false;

        using (document)
        {
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && TryWriteNormalized(root, out normalized, out tokenEndpointIsGoogles);
        }
    }

    private static bool TryWriteNormalized(JsonElement root, out string normalized, out bool tokenEndpointIsGoogles)
    {
        normalized = "";
        tokenEndpointIsGoogles = true;
        try
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, WriteOptions))
            using (var dropped = new Utf8JsonWriter(Stream.Null))
            {
                writer.WriteStartObject();
                dropped.WriteStartObject();
                foreach (var property in root.EnumerateObject())
                {
                    if (string.Equals(property.Name, TokenUriProperty, StringComparison.OrdinalIgnoreCase)
                        && !IsGoogleTokenEndpoint(property.Value))
                    {
                        tokenEndpointIsGoogles = false;
                    }

                    if (!KeptProperties.Contains(property.Name))
                    {
                        // Not kept, but still read in full, so text that is not valid UTF-16 is refused wherever it sits.
                        property.WriteTo(dropped);
                    }
                    else if (property.NameEquals("private_key") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteString(property.Name, RepairPem(property.Value.GetString()!));
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            normalized = Encoding.UTF8.GetString(buffer.WrittenSpan);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The parser reads strings lazily: a lone surrogate written as an escape (\ud800) parses, and throws only when
            // the string is read or copied, here. Its message names no text, but it is dropped all the same.
            normalized = "";
            tokenEndpointIsGoogles = false;
            return false;
        }
    }

    private static bool IsGoogleTokenEndpoint(JsonElement value)
        => value.ValueKind == JsonValueKind.String
           && (value.ValueEquals(GoogleTokenEndpoint) || value.ValueEquals(LegacyGoogleTokenEndpoint));

    private static KeyCheck Invalid => new(false, InvalidCode, null);

    private static bool LooksLikePemPrivateKey(string value)
        => value.Contains("-----BEGIN", StringComparison.Ordinal) && value.Contains("PRIVATE KEY-----", StringComparison.Ordinal);

    private static string RepairPem(string value)
    {
        value = value.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (value.Contains('\n')) return value;

        // No real newline: a key pasted from a place that escapes backslashes shows them as the characters '\' 'n'.
        return value
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal);
    }

    // string.Trim() does not remove U+FEFF (it is not white space), so the BOM is trimmed alongside it.
    private static string Trim(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && IsPadding(value[start])) start++;
        while (end > start && IsPadding(value[end - 1])) end--;
        return value[start..end];
    }

    private static bool IsPadding(char c) => char.IsWhiteSpace(c) || c == '\uFEFF';

    // A lone surrogate typed into the text is rejected here with an ArgumentException. One written as an escape in a property
    // name is an InvalidOperationException, thrown by the duplicate-property check that reads the names (one in a value
    // parses, and throws later: see TryWriteNormalized). Everything else the parser refuses is a JsonException.
    private static bool TryParse(string text, [NotNullWhen(true)] out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(text, ParseOptions);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            // Malformed text, a repeated property, nesting past the depth limit, or invalid UTF-16. The parser's message
            // can quote the offending text, so it is dropped here and never reaches a caller.
            document = null;
            return false;
        }
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = "";
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(name, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? "";
        return true;
    }
}
