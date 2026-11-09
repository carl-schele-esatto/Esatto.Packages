namespace Esatto.Umbraco.Backoffice.Seo.Facts;

/// <summary>
/// robots.txt as Googlebot reads it (RFC 9309): the <c>googlebot</c> group if there is one, else <c>*</c>;
/// longest matching rule wins, <c>Allow</c> wins a tie; <c>*</c> and a trailing <c>$</c> are wildcards.
/// </summary>
public sealed class RobotsTxt
{
    private sealed record Rule(bool Allow, string Pattern);

    private readonly IReadOnlyList<Rule> _rules;

    private RobotsTxt(IReadOnlyList<Rule> rules, IReadOnlyList<string> sitemaps)
    {
        _rules = rules;
        Sitemaps = sitemaps;
    }

    /// <summary>Sitemap addresses listed with <c>Sitemap:</c> (any group, any host).</summary>
    public IReadOnlyList<string> Sitemaps { get; }

    /// <summary>The whole site is blocked: <c>Disallow: /</c> with no <c>Allow</c> rule.</summary>
    public bool DisallowsAll => _rules.Any(r => !r.Allow && r.Pattern == "/") && !_rules.Any(r => r.Allow);

    public static RobotsTxt Parse(string? text)
    {
        var groups = new List<(List<string> Agents, List<Rule> Rules)>();
        var sitemaps = new List<string>();
        (List<string> Agents, List<Rule> Rules)? current = null;
        var lastWasAgent = false;

        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            switch (key)
            {
                case "user-agent":
                    if (!lastWasAgent || current is null)
                    {
                        current = ([], []);
                        groups.Add(current.Value);
                    }
                    current.Value.Agents.Add(value.ToLowerInvariant());
                    lastWasAgent = true;
                    break;
                case "allow" or "disallow":
                    lastWasAgent = false;
                    if (current is null || value.Length == 0) break; // empty Disallow = allow everything
                    current.Value.Rules.Add(new Rule(key == "allow", value));
                    break;
                case "sitemap":
                    if (value.Length > 0) sitemaps.Add(value);
                    break;
                default:
                    lastWasAgent = false;
                    break;
            }
        }

        var mine = groups.Where(g => g.Agents.Contains("googlebot")).SelectMany(g => g.Rules).ToList();
        if (!groups.Any(g => g.Agents.Contains("googlebot")))
            mine = groups.Where(g => g.Agents.Contains("*")).SelectMany(g => g.Rules).ToList();

        return new RobotsTxt(mine, sitemaps);
    }

    /// <summary>True when Googlebot may crawl <paramref name="pathAndQuery"/> (e.g. "/om?x=1").</summary>
    public bool IsAllowed(string pathAndQuery)
    {
        var path = string.IsNullOrEmpty(pathAndQuery) ? "/" : pathAndQuery;
        Rule? best = null;
        foreach (var rule in _rules)
        {
            if (!Matches(rule.Pattern, path)) continue;
            if (best is null
                || rule.Pattern.Length > best.Pattern.Length
                || (rule.Pattern.Length == best.Pattern.Length && rule.Allow && !best.Allow))
                best = rule;
        }

        return best?.Allow ?? true;
    }

    /// <summary>
    /// The rule's pattern matches the start of the path; <c>*</c> is any run of characters, a trailing <c>$</c>
    /// anchors the end. Greedy with one backtrack point, so at worst pattern × path steps (no regex that
    /// a hostile robots.txt could make backtrack for ever).
    /// </summary>
    internal static bool Matches(string pattern, string path)
    {
        var anchored = pattern.EndsWith('$');
        var p = anchored ? pattern[..^1] : pattern + "*"; // unanchored = anything may follow
        int i = 0, j = 0, star = -1, mark = 0;
        while (j < path.Length)
        {
            if (i < p.Length && p[i] == '*')
            {
                star = i++;
                mark = j;
            }
            else if (i < p.Length && p[i] == path[j])
            {
                i++;
                j++;
            }
            else if (star >= 0)
            {
                i = star + 1;
                j = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (i < p.Length && p[i] == '*') i++;
        return i == p.Length;
    }
}
