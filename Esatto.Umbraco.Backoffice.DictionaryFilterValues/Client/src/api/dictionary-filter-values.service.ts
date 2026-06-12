import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";

// A `type` alias (not an `interface`) so it is assignable to the client's
// `query: Record<string, unknown>` parameter — interfaces lack the implicit index signature.
export type DictionaryFilterValuesQuery = {
  filter?: string;
  skip?: number;
  take?: number;
};

const ENDPOINT = "/umbraco/management/api/v1/backoffice/dictionary-filter-values";

/**
 * Calls our authenticated Management API endpoint via the shared backoffice HTTP
 * client. The `security: [bearer]` declaration makes the client attach the
 * backoffice token automatically — the same mechanism the built-in services use,
 * so no manual token handling and no `window.fetch` patching.
 */
export function searchDictionaryFilterValues(query: DictionaryFilterValuesQuery) {
  return umbHttpClient.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: ENDPOINT,
    query,
  });
}
