// The editor's target queries as text, and their limits, without a DOM.
//
// THE SERVER'S NORMALIZATION IS THE AUTHORITY (TargetQueryService.Normalize): each term trimmed, white space inside it collapsed to one space,
// lower-cased, blanks and duplicates dropped (the first kept), a term over 100 characters dropped (never shortened), the list cut at 50. What is
// sent is only what was typed, one term per line; what comes back is the list as it was kept, and that is what the editor shows afterwards.
//
// termsFromText and termsCheck REPEAT the rules, for the counters and the warnings under the text box (they say what is about to be lost before
// it is lost) and never for what is sent: a rule that drifts here changes a counter, not a list.
//
// The brand terms on the Settings tab follow the same rules with the same limits (SettingsNormalizer.NormalizeList, MaxListItems and
// MaxListItemLength), except that a term keeps its spelling and duplicates are found ignoring case: brandTermsCheck counts them that way.

/** The most terms a list holds: ITargetQueryService.MaxTerms, and SettingsNormalizer.MaxListItems for the brand terms. */
export const MAX_TERMS = 50;

/** The longest term, in UTF-16 code units: ITargetQueryService.MaxTermLength, and SettingsNormalizer.MaxListItemLength for the brand terms. */
export const MAX_TERM_LENGTH = 100;

const LINE_BREAK = /\r\n|\r|\n/;

/** The terms as typed, for the request: one per line, trimmed, blank lines left out, in the order typed. */
export function enteredTerms(text: string | null | undefined): string[] {
  return (text ?? '')
    .split(LINE_BREAK)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
}

// Trimmed, runs of white space as one space.
const collapse = (term: string): string => term.split(/\s+/).filter(Boolean).join(' ');

// How a list tidies a term (what is kept and measured) and when two terms are the same.
interface TermRules {
  tidy: (term: string) => string;
  key: (term: string) => string;
}

// The server's Tidy for target queries: trimmed, runs of white space as one space, lower case.
const TARGET_RULES: TermRules = { tidy: (term) => collapse(term).toLowerCase(), key: (term) => term };

// SettingsNormalizer.NormalizeList for brand terms: the spelling is kept, duplicates are found ignoring case.
const BRAND_RULES: TermRules = { tidy: collapse, key: (term) => term.toLowerCase() };

export interface TermsCheck {
  /** The distinct terms that fit the length limit: what the list would hold without the limit on its size. */
  count: number;
  /** How many of them are kept: at most MAX_TERMS. */
  kept: number;
  /** There are more than the list holds. */
  tooMany: boolean;
  /** How many distinct terms are over MAX_TERM_LENGTH and will be left out. */
  tooLong: number;
}

function examine(text: string | null | undefined, rules: TermRules = TARGET_RULES): { fitting: string[]; tooLong: number } {
  const seen = new Set<string>();
  const fitting: string[] = [];
  let tooLong = 0;

  for (const entered of enteredTerms(text)) {
    const term = rules.tidy(entered);
    const key = rules.key(term);
    if (term.length === 0 || seen.has(key)) continue;

    seen.add(key);
    if (term.length > MAX_TERM_LENGTH) tooLong += 1;
    else fitting.push(term);
  }

  return { fitting, tooLong };
}

function check(text: string | null | undefined, rules: TermRules): TermsCheck {
  const { fitting, tooLong } = examine(text, rules);
  return { count: fitting.length, kept: Math.min(fitting.length, MAX_TERMS), tooMany: fitting.length > MAX_TERMS, tooLong };
}

/** A preview of the list the server keeps for a text. */
export function termsFromText(text: string | null | undefined): string[] {
  return examine(text).fitting.slice(0, MAX_TERMS);
}

/** The numbers behind the counters and the warnings. */
export function termsCheck(text: string | null | undefined): TermsCheck {
  return check(text, TARGET_RULES);
}

/** The same numbers for the brand terms on the Settings tab: their spelling is kept, so only the case-blind duplicates fall away. */
export function brandTermsCheck(text: string | null | undefined): TermsCheck {
  return check(text, BRAND_RULES);
}

/** The list as the text box shows it: one term per line. */
export function textFromTerms(terms: readonly string[] | null | undefined): string {
  return (terms ?? []).join('\n');
}
