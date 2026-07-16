// Installs dotted-token support by replacing UmbLocalizationController.prototype.string
// with a version that (a) resolves dotted / hyphenated dictionary keys as a single token,
// and (b) is SURFACE-AWARE: our content-dictionary keys resolve only where translations
// belong (the Content section); elsewhere they render as the raw `#Token`.
//
// Umbraco's built-in string() tokenizes with `/#\w+/g` (stops at `.`/`-`) and resolves
// every key everywhere. We swap in resolveDottedTokens, using the controller's own public
// `term()` as the lookup - no access to Umbraco internals. Verified identical in Umbraco
// 17.3.0 and 18.0.0 (same string()/term()/regex).
//
// Gating rule: for a token whose key we registered (`isOurKey`), resolve only when
// `isTranslatingSurface()` is true; otherwise return null so the raw token shows. Keys we
// did NOT register (Umbraco's own UI keys like `buttons_save`) always resolve, so the
// backoffice chrome is never shown raw.
//
// The concrete UmbLocalizationController and the two predicates are INJECTED by the caller
// (bundle.manifests.ts) so this module stays free of `@umbraco-cms/backoffice/*` and is
// unit-testable in the plain node/vitest environment.

import { resolveDottedTokens } from "./resolve-tokens.logic.js";

/** The slice of UmbLocalizationController the patch reads and replaces. */
export interface Patchable {
  string(text: unknown, ...args: unknown[]): string;
  term(key: string, ...args: unknown[]): string;
}

/** Surface/ownership predicates that decide when our keys translate. */
export interface StringPatchDeps {
  /** True when `key` is a content-dictionary key we registered (not an Umbraco UI key). */
  isOurKey(key: string): boolean;
  /** True when the current surface should translate our keys (the Content section). */
  isTranslatingSurface(): boolean;
}

// Marker stored on the patched prototype so a repeat install is a no-op.
const PATCH_MARK = "__esattoDottedTokenPatch";

/**
 * Replaces `string()` on the given controller class. Idempotent: installing twice leaves
 * the first patch in place. After install, `localize.string("#a.b.c")` resolves the whole
 * dotted key on translating surfaces, and shows the raw token elsewhere.
 */
export function installDottedTokenSupport(
  controllerClass: { prototype: Patchable },
  deps: StringPatchDeps,
): void {
  const proto = controllerClass.prototype as Patchable & { [PATCH_MARK]?: boolean };
  if (proto[PATCH_MARK]) {
    return;
  }

  proto.string = function (this: Patchable, text: unknown, ...args: unknown[]): string {
    return resolveDottedTokens(text as string, (key) => {
      // Our keys render raw off translating surfaces (e.g. the Settings section).
      if (deps.isOurKey(key) && !deps.isTranslatingSurface()) {
        return null;
      }
      // Umbraco's term() returns the key itself (String(key)) when nothing matched;
      // translate that back to `null` so resolveDottedTokens can fall back / keep the token.
      const result = this.term(key, ...args);
      return result === key ? null : result;
    });
  };
  proto[PATCH_MARK] = true;
}
