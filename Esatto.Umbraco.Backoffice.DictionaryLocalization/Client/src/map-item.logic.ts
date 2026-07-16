// Pure transform between the server payload and umbLocalizationManager's
// UmbLocalizationSetBase shape. Kept side-effect-free so it is trivially unit-testable
// and is the single definition of the response<->set contract.

/** The envelope the server endpoint returns. */
export interface DictionaryLocalizationResponse {
  cultures: Record<string, Record<string, string>>;
}

/**
 * The subset of umbLocalizationManager's UmbLocalizationSetBase we produce. Every set
 * carries the ISO code, direction, weight, and one entry per dictionary key. Extra
 * fields on the actual type are optional and irrelevant here.
 */
export interface LocalizationSet {
  $code: string;
  $dir: "ltr" | "rtl";
  $weight: number;
  [key: string]: string | number;
}

/**
 * Weight for our sets. Higher = registered later = wins on merge in the manager. We
 * pick a modest positive value: high enough that dictionary items shadow the default
 * Umbraco UI keys with the same name (rare but possible), low enough that a consumer
 * with a hand-registered override still wins if they use a bigger weight.
 */
export const DICTIONARY_LOCALIZATION_WEIGHT = 100;

/**
 * Right-to-left ISO 639-1 language codes we recognize. Umbraco itself does not ship
 * with these, but a consumer might add e.g. Arabic content dictionary entries. Keep
 * the list conservative — a wrong direction is a much rarer bug than a missed key.
 */
const RTL_LANGUAGES = new Set(["ar", "he", "fa", "ur", "ps", "syr", "dv"]);

/**
 * Given a dictionary key (e.g. `ContactForm.Company`), returns the underscore-
 * normalized variant that `/#\w+/g` captures cleanly (`ContactForm_Company`). Returns
 * `null` if the input is already flat (no `.` or `-`), so callers can skip duplicate
 * registration.
 */
export function normalizeKeyForRegex(key: string): string | null {
  if (!/[.\-]/.test(key)) {
    return null;
  }
  return key.replace(/[.\-]/g, "_");
}

/**
 * Given the culture code the dictionary stored under (e.g. `sv-SE`, `en`), returns
 * `ltr` / `rtl`. Falls back to `ltr` on anything unrecognized.
 */
function directionForCulture(iso: string): "ltr" | "rtl" {
  return RTL_LANGUAGES.has(languagePart(iso)) ? "rtl" : "ltr";
}

/** True when the culture code carries a region (e.g. `sv-se`), not just a language (`sv`). */
function hasRegion(iso: string): boolean {
  return /[-_]/.test(iso);
}

/** The lowercased language part of a culture code: `sv-SE` -> `sv`, `en` -> `en`. */
function languagePart(iso: string): string {
  return iso.split(/[-_]/)[0]?.toLowerCase() ?? "";
}

/**
 * Copies every non-empty entry into `target`, emitting both the original alias and its
 * underscore-normalized form so the `#`-token regex captures dotted / hyphenated keys.
 */
function assignEntries(
  target: LocalizationSet,
  entries: Record<string, string> | undefined,
): void {
  for (const [key, value] of Object.entries(entries ?? {})) {
    if (typeof value !== "string" || value.length === 0) {
      continue;
    }
    target[key] = value;
    const normalized = normalizeKeyForRegex(key);
    if (normalized && normalized !== key) {
      target[normalized] = value;
    }
  }
}

/**
 * Transforms the server payload into LocalizationSets ready for
 * `umbLocalizationManager.registerManyLocalizations`. One set per culture in the payload,
 * plus a **language-only fallback**: for a region culture like `sv-se`, we also emit a
 * bare-language `sv` set carrying the same entries.
 *
 * Why: Umbraco resolves a UI culture as region → language → `en` (never language → region).
 * So a value stored under a region code (`sv-SE`) is invisible to a bare-language UI (`sv`),
 * which only checks `sv` then `en`. Synthesizing the `sv` bucket makes a region-coded
 * dictionary resolve for BOTH `sv` and `sv-SE` users — the same way a language-coded `en`
 * dictionary already resolves for `en`, `en-GB` and `en-US`.
 *
 * A synthesized bucket is skipped when the payload already has an explicit one for that
 * language (we never overwrite editor-provided `sv`). If a language has several regions
 * (`sv-se` and `sv-fi`), their entries are merged into the one `sv` bucket, last region wins.
 */
export function mapResponseToLocalizationSets(
  response: DictionaryLocalizationResponse,
): LocalizationSet[] {
  const cultures = response.cultures ?? {};
  const sets: LocalizationSet[] = [];

  // Languages present explicitly (e.g. `sv`, `en`) — never shadow these with a synthesized alias.
  const explicitLanguages = new Set(
    Object.keys(cultures)
      .filter((iso) => !hasRegion(iso))
      .map((iso) => iso.toLowerCase()),
  );

  // Region entries folded down to their language, built as we go, emitted after the loop.
  const synthesizedByLanguage = new Map<string, Record<string, string>>();

  for (const [iso, entries] of Object.entries(cultures)) {
    const set: LocalizationSet = {
      $code: iso,
      $dir: directionForCulture(iso),
      $weight: DICTIONARY_LOCALIZATION_WEIGHT,
    };
    assignEntries(set, entries);
    sets.push(set);

    const language = languagePart(iso);
    if (hasRegion(iso) && !explicitLanguages.has(language)) {
      const bucket = synthesizedByLanguage.get(language) ?? {};
      for (const [key, value] of Object.entries(entries ?? {})) {
        if (typeof value !== "string" || value.length === 0) {
          continue;
        }
        bucket[key] = value; // last region wins if a language has several
      }
      synthesizedByLanguage.set(language, bucket);
    }
  }

  for (const [language, entries] of synthesizedByLanguage) {
    const set: LocalizationSet = {
      $code: language,
      $dir: directionForCulture(language),
      $weight: DICTIONARY_LOCALIZATION_WEIGHT,
    };
    assignEntries(set, entries);
    sets.push(set);
  }

  return sets;
}
