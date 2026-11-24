namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>
/// Turns what a person types for the two property identifiers into the form Google's APIs expect. A malformed value is
/// refused (null) instead of repaired into something empty or half-formed: a bad identifier stored silently would only
/// fail later, at Google, with an error far from its cause.
/// </summary>
public static class SettingsNormalizer
{
    private const string Ga4Prefix = "properties/";

    // A property id is a number of about ten digits; the cap only keeps a wrong paste from being stored.
    private const int MaxGa4Digits = 20;

    private const string DomainPrefix = "sc-domain:";
    private const int MaxSiteLength = 2048;
    private const int MaxHostLength = 253;
    private const int MaxLabelLength = 63;

    /// <summary>The most items a list setting holds (brand terms, user-group aliases): the first ones are kept.</summary>
    public const int MaxListItems = 50;

    /// <summary>The longest item of a list setting, in UTF-16 code units (for a user-group alias see <see cref="MaxGroupAliasLength"/>). A longer item is dropped, not shortened.</summary>
    public const int MaxListItemLength = 100;

    /// <summary>
    /// The longest user-group alias, in UTF-16 code units: Umbraco's own limit, so no alias a person can pick is dropped. That
    /// matters because an empty list of groups means everyone with access.
    /// </summary>
    public const int MaxGroupAliasLength = 200;

    /// <summary>The longest display name, in UTF-16 code units. A longer one is cut.</summary>
    public const int MaxDisplayNameLength = 200;

    /// <summary>
    /// <c>123456789</c> or <c>Properties/123456789</c> (any case, any surrounding white space) becomes
    /// <c>properties/123456789</c>. Anything else, including an empty input, is null.
    /// </summary>
    public static string? NormalizeGa4(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        var digits = text.StartsWith(Ga4Prefix, StringComparison.OrdinalIgnoreCase) ? text[Ga4Prefix.Length..] : text;
        return digits.Length is > 0 and <= MaxGa4Digits && digits.All(char.IsAsciiDigit) ? Ga4Prefix + digits : null;
    }

    /// <summary>
    /// A bare host (<c>example.com</c>, one closing slash allowed) or <c>sc-domain:example.com</c> becomes the domain property
    /// <c>sc-domain:example.com</c>. An <c>http(s)</c> URL becomes a URL-prefix property: scheme and host in lower case, the
    /// path as typed, always ending in <c>/</c>, the scheme's default port dropped. Anything else is null: white space, another
    /// scheme, a query, a fragment, credentials, a bare host with a path (the scheme is never guessed), or a host that is not a
    /// domain name or an IP address. The result is never longer than 2048 characters: an input that would grow past that (a
    /// URL prefix given its closing slash) is refused, so whatever is stored normalizes to itself when it is read back.
    /// </summary>
    public static string? NormalizeSearchConsole(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxSiteLength || text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return null;
        }

        var normalized = text.StartsWith(DomainPrefix, StringComparison.OrdinalIgnoreCase)
            ? DomainProperty(text[DomainPrefix.Length..])
            : StartsWithHttpScheme(text) ? UrlPrefixProperty(text) : DomainProperty(text);

        return normalized is { Length: <= MaxSiteLength } ? normalized : null;
    }

    /// <summary>
    /// A list the way the settings keep it (brand terms, user-group aliases), by the same rules and in the same order as the target
    /// queries (see <c>TargetQueryService.Normalize</c>), except that the spelling is kept: each item trimmed with any run of white
    /// space inside it as one space; blanks (and nulls) dropped; duplicates dropped ignoring case, the first spelling kept; an item
    /// longer than <see cref="MaxListItemLength"/> dropped, not shortened; the list cut at <see cref="MaxListItems"/>, so the last
    /// items go. The order is kept. Never null, and a list that comes out of here comes out the same when it goes in again.
    /// </summary>
    internal static string[] NormalizeList(IEnumerable<string?>? values) => Tidy(values, MaxListItemLength);

    /// <summary>
    /// The user-group aliases the way the settings keep them: as <see cref="NormalizeList"/>, except that an alias may be up to
    /// <see cref="MaxGroupAliasLength"/> characters long.
    /// </summary>
    internal static string[] NormalizeGroupAliases(IEnumerable<string?>? values) => Tidy(values, MaxGroupAliasLength);

    private static string[] Tidy(IEnumerable<string?>? values, int maxItemLength)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();

        foreach (var value in values ?? [])
        {
            var item = value is null ? "" : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (item.Length == 0 || item.Length > maxItemLength || !seen.Add(item)) continue;

            kept.Add(item);
            if (kept.Count == MaxListItems) break;
        }

        return [.. kept];
    }

    /// <summary>
    /// The display name the way it is kept: trimmed, cut to <see cref="MaxDisplayNameLength"/> (never inside a surrogate pair, and
    /// without the white space the cut leaves at the end), null when blank.
    /// </summary>
    internal static string? NormalizeDisplayName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        if (name.Length <= MaxDisplayNameLength) return name;

        var cut = char.IsHighSurrogate(name[MaxDisplayNameLength - 1]) ? MaxDisplayNameLength - 1 : MaxDisplayNameLength;
        return name[..cut].TrimEnd();
    }

    private static bool StartsWithHttpScheme(string text)
        => text.StartsWith(Uri.UriSchemeHttp + "://", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith(Uri.UriSchemeHttps + "://", StringComparison.OrdinalIgnoreCase);

    private static string? DomainProperty(string host)
    {
        var name = host.EndsWith('/') ? host[..^1] : host;   // one closing slash is tolerated, as in "example.com/"
        return IsDomainName(name) ? DomainPrefix + name.ToLowerInvariant() : null;
    }

    private static string? UrlPrefixProperty(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
            || !IsAddressableHost(uri))
        {
            return null;
        }

        var authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        var path = uri.AbsolutePath;
        return $"{uri.Scheme}://{authority}{path}{(path.EndsWith('/') ? "" : "/")}";
    }

    private static bool IsAddressableHost(Uri uri)
        => uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 || IsDomainName(uri.Host);

    // Search Console can only verify a public name, so a single label ("localhost", "example") is not accepted.
    private static bool IsDomainName(string host)
    {
        if (host.Length is 0 or > MaxHostLength) return false;

        var labels = host.Split('.');
        return labels.Length >= 2 && labels.All(IsLabel);
    }

    private static bool IsLabel(string label)
        => label.Length is > 0 and <= MaxLabelLength
           && label[0] != '-'
           && label[^1] != '-'
           && label.All(c => char.IsLetterOrDigit(c) || c == '-');
}
