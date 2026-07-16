import { describe, it, expect } from "vitest";
import { installDottedTokenSupport, type StringPatchDeps } from "./install-string-patch.js";

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

// Default deps: everything is "ours" and the surface translates → resolve normally.
const translatingDeps = (ourKeys: string[]): StringPatchDeps => ({
  isOurKey: (k) => ourKeys.includes(k),
  isTranslatingSurface: () => true,
});
const nonTranslatingDeps = (ourKeys: string[]): StringPatchDeps => ({
  isOurKey: (k) => ourKeys.includes(k),
  isTranslatingSurface: () => false,
});

describe("installDottedTokenSupport", () => {
  it("resolves a dotted token as one key on a translating surface", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl, translatingDeps(["SEO.MetaKeywords.Description"]));

    const ctrl = new Ctrl({ "SEO.MetaKeywords.Description": "Meta beskrivning" });
    expect(ctrl.string("#SEO.MetaKeywords.Description")).toBe("Meta beskrivning");
  });

  it("shows OUR key raw on a non-translating surface", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl, nonTranslatingDeps(["SEO.MetaKeywords.Description"]));

    const ctrl = new Ctrl({ "SEO.MetaKeywords.Description": "Meta beskrivning" });
    expect(ctrl.string("#SEO.MetaKeywords.Description")).toBe("#SEO.MetaKeywords.Description");
  });

  it("still resolves an Umbraco (non-our) key on a non-translating surface", () => {
    const Ctrl = makeFakeControllerClass();
    // isOurKey false for everything → Umbraco chrome keys always resolve.
    installDottedTokenSupport(Ctrl, nonTranslatingDeps([]));

    const ctrl = new Ctrl({ buttons_save: "Spara" });
    expect(ctrl.string("#buttons_save")).toBe("Spara");
  });

  it("leaves unknown tokens untouched", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl, translatingDeps([]));

    const ctrl = new Ctrl({});
    expect(ctrl.string("#Unknown.Key")).toBe("#Unknown.Key");
  });

  it("is idempotent — installing twice does not re-wrap the method", () => {
    const Ctrl = makeFakeControllerClass();
    installDottedTokenSupport(Ctrl, translatingDeps(["A.B"]));
    const afterFirst = Ctrl.prototype.string;
    installDottedTokenSupport(Ctrl, translatingDeps(["A.B"]));

    expect(Ctrl.prototype.string).toBe(afterFirst);
    const ctrl = new Ctrl({ "A.B": "ok" });
    expect(ctrl.string("#A.B")).toBe("ok");
  });
});
