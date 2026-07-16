import { describe, it, expect } from "vitest";
import { rewriteBareTokensToUfm } from "./rewrite-ufm-tokens.logic.js";

// A known-key predicate backed by a fixed set, mirroring how the real patch asks the
// localization controller whether a key resolves.
const known = (...keys: string[]) => (key: string) => keys.includes(key);

describe("rewriteBareTokensToUfm", () => {
  it("wraps a bare dotted token into the UFM localize component", () => {
    expect(
      rewriteBareTokensToUfm("#SEO.MetaKeywords.Description", known("SEO.MetaKeywords.Description")),
    ).toBe("{#SEO.MetaKeywords.Description}");
  });

  it("wraps a flat token", () => {
    expect(rewriteBareTokensToUfm("#TestTag", known("TestTag"))).toBe("{#TestTag}");
  });

  it("wraps a hyphenated token", () => {
    expect(rewriteBareTokensToUfm("#header-contact-cta", known("header-contact-cta"))).toBe(
      "{#header-contact-cta}",
    );
  });

  it("leaves unknown tokens untouched (literal #123, #hashtag)", () => {
    expect(rewriteBareTokensToUfm("see issue #123 and #hashtag", known("Other"))).toBe(
      "see issue #123 and #hashtag",
    );
  });

  it("does not double-wrap an existing {#Key} token", () => {
    expect(
      rewriteBareTokensToUfm("{#SEO.MetaKeywords.Description}", known("SEO.MetaKeywords.Description")),
    ).toBe("{#SEO.MetaKeywords.Description}");
  });

  it("does not touch a Markdown heading (# with a space)", () => {
    expect(rewriteBareTokensToUfm("# Heading", known("Heading"))).toBe("# Heading");
  });

  it("keeps trailing punctuation outside the braces", () => {
    expect(rewriteBareTokensToUfm("End with #Foo.", known("Foo"))).toBe("End with {#Foo}.");
  });

  it("does not treat C# as a token (no word char boundary before #)", () => {
    expect(rewriteBareTokensToUfm("I like C#Sharp", known("Sharp"))).toBe("I like C#Sharp");
  });

  it("wraps a token that follows normal text and a space", () => {
    expect(rewriteBareTokensToUfm("Prefix #A.B done", known("A.B"))).toBe("Prefix {#A.B} done");
  });

  it("handles multiple tokens in one string, mixing known and unknown", () => {
    expect(
      rewriteBareTokensToUfm("#A.B then #C.D", known("A.B")),
    ).toBe("{#A.B} then #C.D");
  });

  it("returns non-string input unchanged", () => {
    // @ts-expect-error deliberately passing a non-string to prove the guard
    expect(rewriteBareTokensToUfm(undefined, known())).toBeUndefined();
  });
});
