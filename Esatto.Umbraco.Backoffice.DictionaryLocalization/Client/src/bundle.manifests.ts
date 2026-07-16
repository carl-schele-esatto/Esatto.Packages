import type { UmbEntryPointOnInit } from "@umbraco-cms/backoffice/extension-api";
import {
  umbLocalizationManager,
  UmbLocalizationController,
} from "@umbraco-cms/backoffice/localization-api";
import { fetchAndRegisterDictionaryLocalizations } from "./entry-point.logic.js";
import { fetchAllDictionaryLocalizations } from "./api/dictionary-localization.service.js";
import { installDottedTokenSupport } from "./install-string-patch.js";
import { installUfmHashTokenSupport } from "./install-ufm-patch.js";

/**
 * Entry point: fetch the whole content dictionary from our authenticated endpoint
 * and register each culture as a LocalizationSet into umbLocalizationManager. After
 * this runs, `localize.string("#Key")` / `localize.term("Key")` in the backoffice
 * resolve against the content Dictionary in the current user's culture (with the
 * manager's built-in region → language → `en` fallback).
 *
 * No `onUnload`: umbLocalizationManager exposes no removal API in 17.3.0 and its
 * lifetime is document-scoped anyway.
 */
export const onInit: UmbEntryPointOnInit = async () => {
  // Replace localize.string()'s `/#\w+/g` tokenizer so dotted / hyphenated keys resolve
  // as ONE token (e.g. `#SEO.MetaKeywords.Description`) in property LABELS and anywhere
  // else localize.string() is used. Installed first (synchronous, no I/O) so it is in
  // place regardless of whether the fetch below succeeds.
  installDottedTokenSupport(UmbLocalizationController);

  // Property DESCRIPTIONS are rendered as UFM (markdown), not via localize.string(), so a
  // bare `#Key` never resolves there. Wrap <umb-ufm-render> so a bare `#Key` that names a
  // real dictionary entry is rewritten to the `{#Key}` UFM localize token — no braces for
  // editors to add. whenDefined resolves once the element registers; the prototype patch
  // then applies to every instance. Fire-and-forget: a failure must not break startup.
  customElements
    .whenDefined("umb-ufm-render")
    .then((renderClass) => installUfmHashTokenSupport(renderClass))
    .catch(() => {
      /* element never defined / patch failed — descriptions keep needing `{#Key}` */
    });

  await fetchAndRegisterDictionaryLocalizations(
    umbLocalizationManager,
    fetchAllDictionaryLocalizations,
  );
};
