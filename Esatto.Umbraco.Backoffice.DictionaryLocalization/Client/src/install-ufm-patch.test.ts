import { describe, it, expect } from "vitest";
import { installUfmHashTokenSupport } from "./install-ufm-patch.js";

/**
 * Mirrors the slice of Umbraco's `<umb-ufm-render>` the patch relies on:
 *  - a reactive `markdown` accessor (get/set on the prototype, backed by a field),
 *  - `localize.term(key)` returning the value, or the key itself when unknown.
 */
function makeFakeRenderClass(map: Record<string, string>) {
  return class FakeUfmRender {
    #markdown = "";
    localize = {
      term: (key: string): string => (key in map ? map[key]! : key),
    };
    get markdown(): string {
      return this.#markdown;
    }
    set markdown(value: string) {
      this.#markdown = value;
    }
  };
}

describe("installUfmHashTokenSupport", () => {
  it("rewrites a bare, known #Key on read so UFM can localize it", () => {
    const El = makeFakeRenderClass({ "SEO.MetaKeywords.Description": "Meta beskrivning" });
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => true });

    const el = new El();
    el.markdown = "#SEO.MetaKeywords.Description";
    expect(el.markdown).toBe("{#SEO.MetaKeywords.Description}");
  });

  it("leaves an unknown bare token untouched", () => {
    const El = makeFakeRenderClass({});
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => true });

    const el = new El();
    el.markdown = "see #123";
    expect(el.markdown).toBe("see #123");
  });

  it("does not double-wrap an existing {#Key}", () => {
    const El = makeFakeRenderClass({ "A.B": "ok" });
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => true });

    const el = new El();
    el.markdown = "{#A.B}";
    expect(el.markdown).toBe("{#A.B}");
  });

  it("preserves the raw value through the setter (reactivity untouched)", () => {
    const El = makeFakeRenderClass({});
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => true });

    const el = new El();
    el.markdown = "plain text";
    expect(el.markdown).toBe("plain text");
  });

  it("is idempotent — installing twice keeps one wrapper", () => {
    const El = makeFakeRenderClass({ "A.B": "ok" });
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => true });
    const afterFirst = Object.getOwnPropertyDescriptor(El.prototype, "markdown");
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => true });
    expect(Object.getOwnPropertyDescriptor(El.prototype, "markdown")).toStrictEqual(afterFirst);

    const el = new El();
    el.markdown = "#A.B";
    expect(el.markdown).toBe("{#A.B}");
  });

  it("no-ops safely when the class has no markdown accessor", () => {
    class NoAccessor {}
    expect(() => installUfmHashTokenSupport(NoAccessor, { isTranslatingSurface: () => true })).not.toThrow();
  });

  it("leaves a bare #Key raw on a non-translating surface", () => {
    const El = makeFakeRenderClass({ "SEO.MetaKeywords.Description": "Meta beskrivning" });
    installUfmHashTokenSupport(El, { isTranslatingSurface: () => false });

    const el = new El();
    el.markdown = "#SEO.MetaKeywords.Description";
    expect(el.markdown).toBe("#SEO.MetaKeywords.Description");
  });
});
