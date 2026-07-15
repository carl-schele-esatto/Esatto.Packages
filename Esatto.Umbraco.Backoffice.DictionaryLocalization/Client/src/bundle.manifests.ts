import type { UmbEntryPointOnInit } from "@umbraco-cms/backoffice/extension-api";
import { umbLocalizationManager } from "@umbraco-cms/backoffice/localization-api";
import { fetchAndRegisterDictionaryLocalizations } from "./entry-point.logic.js";
import { fetchAllDictionaryLocalizations } from "./api/dictionary-localization.service.js";

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
  await fetchAndRegisterDictionaryLocalizations(
    umbLocalizationManager,
    fetchAllDictionaryLocalizations,
  );
};
