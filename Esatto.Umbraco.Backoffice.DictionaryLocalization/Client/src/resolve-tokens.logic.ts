// Pure, side-effect-free tokenizer used to replace Umbraco's built-in `#`-token
// substitution so dotted / hyphenated dictionary keys resolve as a SINGLE token.
//
// Umbraco's own `localize.string()` tokenizes with `/#\w+/g`, which stops at the
// first `.` or `-`. That means `#SEO.MetaKeywords.Description` only ever captures
// `#SEO`. This resolver captures the whole dotted/hyphenated run and then resolves
// the LONGEST key the lookup knows, keeping any unconsumed remainder as literal text.
// Unknown tokens are left exactly as written — identical to Umbraco's fallback.

/** Resolves a key to its localized value, or `null` when the key is unknown. */
export type TermLookup = (key: string) => string | null;

// Start at a word char (so a lone `#` is ignored), then greedily consume word chars,
// dots and hyphens. `#` is excluded so adjacent tokens don't merge.
const TOKEN = /#\w[\w.-]*/g;

/**
 * Builds the candidate keys to try, longest first, by trimming trailing
 * `.`/`-`-delimited segments: `A.B.C` -> `["A.B.C", "A.B", "A"]`. A trailing
 * separator (e.g. `Foo.`) is handled the same way: `["Foo.", "Foo"]`.
 */
function candidatePrefixes(body: string): string[] {
  const out: string[] = [];
  let s = body;
  while (s.length > 0) {
    out.push(s);
    const idx = Math.max(s.lastIndexOf("."), s.lastIndexOf("-"));
    if (idx <= 0) break; // no separator, or only a leading one — nothing more to trim
    s = s.slice(0, idx);
  }
  return out;
}

/**
 * Replaces every `#token` in `text` with its localized value via `lookup`. Dotted and
 * hyphenated keys resolve as one token; the longest known key wins; unknown tokens are
 * returned unchanged. Non-string input yields an empty string (matches Umbraco).
 */
export function resolveDottedTokens(text: string, lookup: TermLookup): string {
  if (typeof text !== "string") {
    return "";
  }
  return text.replace(TOKEN, (match) => {
    const body = match.slice(1); // drop the leading '#'
    for (const candidate of candidatePrefixes(body)) {
      const value = lookup(candidate);
      if (value !== null) {
        return value + body.slice(candidate.length); // re-append the unconsumed tail
      }
    }
    return match; // nothing resolved — leave the token untouched
  });
}
