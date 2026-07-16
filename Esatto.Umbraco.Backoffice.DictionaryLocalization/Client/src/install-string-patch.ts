// Installs dotted-token support by replacing UmbLocalizationController.prototype.string
// with a version that resolves dotted / hyphenated dictionary keys as a single token.
//
// Umbraco's built-in string() tokenizes with `/#\w+/g` (stops at `.`/`-`), so
// `#SEO.MetaKeywords.Description` never resolves. We swap in resolveDottedTokens, using
// the controller's own public `term()` as the lookup - no access to Umbraco internals.
// Verified identical in Umbraco 17.3.0 and 18.0.0 (same string()/term()/regex).
//
// The concrete UmbLocalizationController is INJECTED by the caller (bundle.manifests.ts)
// rather than imported here, so this module stays free of `@umbraco-cms/backoffice/*`
// and is unit-testable in the plain node/vitest environment.

import { resolveDottedTokens } from "./resolve-tokens.logic.js";

/** The slice of UmbLocalizationController the patch reads and replaces. */
export interface Patchable {
  string(text: unknown, ...args: unknown[]): string;
  term(key: string, ...args: unknown[]): string;
}

// Marker stored on the patched prototype so a repeat install is a no-op. Keyed on the
// prototype itself (not a module global) so it is naturally per-class and testable.
const PATCH_MARK = "__esattoDottedTokenPatch";

/**
 * Replaces `string()` on the given controller class. Idempotent: installing twice leaves
 * the first patch in place. After install, `localize.string("#a.b.c")` resolves the whole
 * dotted key.
 */
export function installDottedTokenSupport(controllerClass: { prototype: Patchable }): void {
  const proto = controllerClass.prototype as Patchable & { [PATCH_MARK]?: boolean };
  if (proto[PATCH_MARK]) {
    return;
  }

  proto.string = function (this: Patchable, text: unknown, ...args: unknown[]): string {
    return resolveDottedTokens(text as string, (key) => {
      // Umbraco's term() returns the key itself (String(key)) when nothing matched;
      // translate that back to `null` so resolveDottedTokens can fall back / keep the token.
      const result = this.term(key, ...args);
      return result === key ? null : result;
    });
  };
  proto[PATCH_MARK] = true;
}
