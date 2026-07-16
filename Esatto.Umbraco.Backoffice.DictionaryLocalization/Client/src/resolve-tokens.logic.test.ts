import { describe, it, expect } from "vitest";
import { resolveDottedTokens, type TermLookup } from "./resolve-tokens.logic.js";

/** Build a lookup from a flat map; unknown keys return null (the "not found" contract). */
function lookupFrom(map: Record<string, string>): TermLookup {
  return (key) => (key in map ? map[key]! : null);
}

describe("resolveDottedTokens", () => {
  it("resolves a fully dotted key as a single token", () => {
    const lookup = lookupFrom({ "SEO.MetaKeywords.Description": "Meta beskrivning" });
    expect(resolveDottedTokens("#SEO.MetaKeywords.Description", lookup)).toBe("Meta beskrivning");
  });

  it("still resolves a flat key with no separators", () => {
    const lookup = lookupFrom({ TestTag: "Test tag" });
    expect(resolveDottedTokens("#TestTag", lookup)).toBe("Test tag");
  });

  it("resolves a hyphenated key as a single token", () => {
    const lookup = lookupFrom({ "header-contact-cta": "Kontakt" });
    expect(resolveDottedTokens("#header-contact-cta", lookup)).toBe("Kontakt");
  });

  it("leaves an unknown token untouched", () => {
    const lookup = lookupFrom({});
    expect(resolveDottedTokens("#SEO.MetaKeywords.Description", lookup)).toBe(
      "#SEO.MetaKeywords.Description",
    );
  });

  it("backs off past trailing punctuation to the longest resolvable key", () => {
    // "#Foo." — the trailing period is sentence punctuation, not part of the key.
    const lookup = lookupFrom({ Foo: "Bar" });
    expect(resolveDottedTokens("End with #Foo.", lookup)).toBe("End with Bar.");
  });

  it("resolves the longest registered prefix and keeps the rest as literal", () => {
    // Only "SEO.MetaKeywords" is registered; ".Extra" is not part of any key.
    const lookup = lookupFrom({ "SEO.MetaKeywords": "Nyckelord" });
    expect(resolveDottedTokens("#SEO.MetaKeywords.Extra", lookup)).toBe("Nyckelord.Extra");
  });

  it("resolves multiple tokens in one string", () => {
    const lookup = lookupFrom({ "A.B": "one", "C.D": "two" });
    expect(resolveDottedTokens("#A.B and #C.D", lookup)).toBe("one and two");
  });

  it("returns an empty string for non-string input", () => {
    const lookup = lookupFrom({});
    // @ts-expect-error exercising the runtime guard
    expect(resolveDottedTokens(undefined, lookup)).toBe("");
  });
});
