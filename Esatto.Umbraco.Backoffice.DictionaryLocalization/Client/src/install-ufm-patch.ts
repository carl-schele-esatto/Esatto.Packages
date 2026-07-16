// Installs bare-`#Key` support inside UFM-rendered content (property descriptions) by
// wrapping the `markdown` accessor on Umbraco's `<umb-ufm-render>` element class.
//
// The element reads `this.markdown` and hands it to the UFM parser. We wrap the getter so
// it returns a version where every bare `#Key` that resolves to a real dictionary value is
// rewritten to `{#Key}` — the token UFM's own localize component understands. The setter is
// left untouched, so Lit's reactivity (change detection on the stored raw value) is intact.
//
// The concrete element class is INJECTED by the caller (bundle.manifests.ts, via
// customElements.whenDefined) so this module stays free of `@umbraco-cms/backoffice/*` and
// is unit-testable in plain node/vitest.

import { rewriteBareTokensToUfm } from "./rewrite-ufm-tokens.logic.js";

/** The slice of the `<umb-ufm-render>` instance the wrapped getter relies on. */
export interface UfmRenderInstance {
  // Umbraco's UmbLitElement supplies `localize`; term() returns the value or the key when unknown.
  localize?: { term(key: string): string };
}

// Marker stored on the patched prototype so a repeat install is a no-op.
const PATCH_MARK = "__esattoUfmHashPatch";

/**
 * Wraps `markdown` on the given `<umb-ufm-render>` class so bare `#Key` tokens resolve.
 * Idempotent. No-ops safely if the class does not expose a `markdown` get/set accessor
 * (e.g. a future Umbraco refactor) — the feature simply stays inert rather than throwing.
 */
export function installUfmHashTokenSupport(renderClass: { prototype: object }): void {
  const proto = renderClass.prototype as Record<string, unknown> & { [PATCH_MARK]?: boolean };
  if (proto[PATCH_MARK]) {
    return;
  }

  const descriptor = Object.getOwnPropertyDescriptor(proto, "markdown");
  if (!descriptor || typeof descriptor.get !== "function" || typeof descriptor.set !== "function") {
    return;
  }

  const originalGet = descriptor.get;
  const originalSet = descriptor.set;

  Object.defineProperty(proto, "markdown", {
    configurable: true,
    enumerable: descriptor.enumerable,
    get(this: UfmRenderInstance) {
      const raw = originalGet.call(this);
      if (typeof raw !== "string") {
        return raw;
      }
      const localize = this.localize;
      if (!localize) {
        return raw;
      }
      // A key is "known" when term() returns something other than the key itself
      // (Umbraco's not-found contract), matching the label-side patch.
      return rewriteBareTokensToUfm(raw, (key) => localize.term(key) !== key);
    },
    set(this: UfmRenderInstance, value: unknown) {
      originalSet.call(this, value);
    },
  });

  proto[PATCH_MARK] = true;
}
