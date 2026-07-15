import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";

const ENDPOINT = "/umbraco/management/api/v1/backoffice/dictionary-localization/all";

/**
 * Calls our authenticated Management API endpoint via the shared backoffice HTTP
 * client. The `security: [bearer]` declaration makes the client attach the backoffice
 * token automatically — same mechanism the built-in services use.
 */
export function fetchAllDictionaryLocalizations() {
  return umbHttpClient.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: ENDPOINT,
  });
}
