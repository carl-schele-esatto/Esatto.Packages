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
  const language = iso.split(/[-_]/)[0]?.toLowerCase() ?? "";
  return RTL_LANGUAGES.has(language) ? "rtl" : "ltr";
}

/**
 * Transforms the server payload into one LocalizationSet per culture, ready to hand to
 * `umbLocalizationManager.registerManyLocalizations`. For each dictionary key we emit
 * both the original alias and its underscore-normalized form so the `#`-token regex
 * captures dotted / hyphenated keys cleanly.
 */
export function mapResponseToLocalizationSets(
  response: DictionaryLocalizationResponse,
): LocalizationSet[] {
  const sets: LocalizationSet[] = [];

  for (const [iso, entries] of Object.entries(response.cultures ?? {})) {
    const set: LocalizationSet = {
      $code: iso,
      $dir: directionForCulture(iso),
      $weight: DICTIONARY_LOCALIZATION_WEIGHT,
    };

    for (const [key, value] of Object.entries(entries ?? {})) {
      if (typeof value !== "string" || value.length === 0) {
        continue;
      }
      set[key] = value;
      const normalized = normalizeKeyForRegex(key);
      if (normalized && normalized !== key) {
        set[normalized] = value;
      }
    }

    sets.push(set);
  }

  return sets;
}
