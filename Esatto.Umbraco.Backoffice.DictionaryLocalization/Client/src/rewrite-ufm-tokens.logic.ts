// Pure, side-effect-free rewrite that lets a BARE `#Key` resolve inside content that
// Umbraco renders as UFM (Umbraco Flavored Markdown) — most visibly a content-type
// property's *description*.
//
// Unlike a property *label* (rendered through `localize.string()`, which this package
// already patches for dotted keys), a *description* is rendered by `<umb-ufm-render>`.
// UFM only localizes via its built-in component syntax `{#Key}` (marker `#`) — a bare
// `#Key` is just markdown text and never resolves. Rather than fight UFM, we rewrite a
// bare `#Key` into the `{#Key}` the UFM localize component already understands, so the
// SAME `#Key` an editor writes in a label works verbatim in a description too.
//
// Guard rails, so we only touch real localization tokens:
//   - Only rewrite a key the lookup actually knows (`isKnownKey`). Literal text like
//     `#123` (an issue ref) or `#hashtag` that is not a dictionary key is left alone.
//   - Never touch a `#` already inside a UFM marker (`{#Key}`) — the lookbehind excludes
//     a preceding `{` (and `#`), so existing tokens are not double-wrapped.
//   - Never touch a Markdown ATX heading (`# Heading`): the key must start with a word
//     char immediately after `#`, and a heading has a space there.

/** Returns true when `key` resolves to a dictionary value (i.e. is a real token). */
export type KnownKeyPredicate = (key: string) => boolean;

// A bare token: `#` (not preceded by a word char, `{`, or another `#`), then a word
// char, then any run of word chars, dots and hyphens. Mirrors the label-side token
// grammar in resolve-tokens.logic.ts so labels and descriptions accept the same keys.
const BARE_TOKEN = /(?<![\w{#])#(\w[\w.-]*)/g;

/**
 * Rewrites every bare `#Key` in `markdown` to `{#Key}` when `isKnownKey(Key)` is true,
 * so Umbraco's UFM localize component resolves it. A trailing `.`/`-` run is kept OUTSIDE
 * the braces (so `#Foo.` -> `{#Foo}.`, matching sentence punctuation). Unknown tokens and
 * non-string input are returned unchanged.
 */
export function rewriteBareTokensToUfm(
  markdown: string,
  isKnownKey: KnownKeyPredicate,
): string {
  if (typeof markdown !== "string") {
    return markdown;
  }
  return markdown.replace(BARE_TOKEN, (match, raw: string) => {
    // Trailing separators are punctuation, not part of the key: `Foo.` -> key `Foo`, tail `.`.
    const key = raw.replace(/[.-]+$/, "");
    const tail = raw.slice(key.length);
    return isKnownKey(key) ? `{#${key}}${tail}` : match;
  });
}
