import type { UmbEntryPointOnInit } from "@umbraco-cms/backoffice/extension-api";
import {
  umbLocalizationManager,
  UmbLocalizationController,
} from "@umbraco-cms/backoffice/localization-api";
import { fetchAndRegisterDictionaryLocalizations } from "./entry-point.logic.js";
import { fetchAllDictionaryLocalizations } from "./api/dictionary-localization.service.js";
import { installDottedTokenSupport } from "./install-string-patch.js";
import { installUfmHashTokenSupport } from "./install-ufm-patch.js";
import { isKnownDictionaryKey } from "./dictionary-keys.js";
import { currentIsContentSurface } from "./surface.logic.js";

/**
 * Entry point: fetch the whole content dictionary from our authenticated endpoint and
 * register each culture as a LocalizationSet into umbLocalizationManager. After this runs,
 * `localize.string("#Key")` / `localize.term("Key")` resolve against the content Dictionary
 * in the current user's culture (with the manager's region → language → `en` fallback).
 *
 * Surface policy: our content-dictionary tokens resolve on the **Content** section (where
 * editors work) and render as the raw `#Token` everywhere else (Settings, etc.), so admins
 * configuring doctypes see the keys. Umbraco's own UI keys always resolve.
 *
 * No `onUnload`: umbLocalizationManager exposes no removal API in 17.3.0 and its lifetime is
 * document-scoped anyway.
 */
export const onInit: UmbEntryPointOnInit = async () => {
  // Replace localize.string()'s `/#\w+/g` tokenizer so dotted / hyphenated keys resolve as
  // ONE token in property LABELS (and anywhere localize.string() is used). Surface-aware:
  // our keys resolve on the Content section only; Umbraco's own keys always resolve.
  // Installed first (synchronous, no I/O) so it is in place regardless of the fetch below.
  installDottedTokenSupport(UmbLocalizationController, {
    isOurKey: isKnownDictionaryKey,
    isTranslatingSurface: currentIsContentSurface,
  });

  // Property DESCRIPTIONS are rendered as UFM (markdown), not via localize.string(), so a
  // bare `#Key` never resolves there. Wrap <umb-ufm-render> so a bare `#Key` naming a real
  // dictionary entry is rewritten to the `{#Key}` UFM token — on the Content section only.
  // Fire-and-forget: a failure must not break startup.
  customElements
    .whenDefined("umb-ufm-render")
    .then((renderClass) =>
      installUfmHashTokenSupport(renderClass, { isTranslatingSurface: currentIsContentSurface }),
    )
    .catch(() => {
      /* element never defined / patch failed — descriptions keep needing `{#Key}` */
    });

  await fetchAndRegisterDictionaryLocalizations(
    umbLocalizationManager,
    fetchAllDictionaryLocalizations,
  );
};
