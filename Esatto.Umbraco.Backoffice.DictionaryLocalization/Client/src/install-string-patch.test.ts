import { describe, it, expect } from "vitest";
import { installDottedTokenSupport } from "./install-string-patch.js";

/**
 * Mirrors the slice of Umbraco's UmbLocalizationController the patch relies on:
 *  - `term(key)` returns the localized value, or the key itself when unknown.
 *  - `string(text)` is the built-in `/#\w+/g` tokenizer we expect to be replaced.
 * Methods live on the prototype so patching the class affects instances.
 */
function makeFakeControllerClass() {
  return class FakeController {
    #map: Record<string, string>;
    constructor(map: Record<string, string>) {
      this.#map = map;
    }
    term(key: string): string {
      return key in this.#map ? this.#map[key]! : key;
    }
    string(text: string): string {
      return text.replace(/#\w+/g, (m) => {
        const v = this.term(m.slice(1));
        return v === m.slice(1) ? m : v;
      });
    }
  };
}

describe("installDottedTokenSupport", () => {
  it("replaces string() so a dotted token resolves as one key", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl);

    const ctrl = new Ctrl({ "SEO.MetaKeywords.Description": "Meta beskrivning" });
    expect(ctrl.string("#SEO.MetaKeywords.Description")).toBe("Meta beskrivning");
  });

  it("leaves unknown tokens untouched (via term's key-when-unknown contract)", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl);

    const ctrl = new Ctrl({});
    expect(ctrl.string("#Unknown.Key")).toBe("#Unknown.Key");
  });

  it("is idempotent — installing twice does not re-wrap the method", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl);
    const afterFirst = Ctrl.prototype.string;
    installDottedTokenSupport(Ctrl);

    expect(Ctrl.prototype.string).toBe(afterFirst);
    const ctrl = new Ctrl({ "A.B": "ok" });
    expect(ctrl.string("#A.B")).toBe("ok");
  });
});
