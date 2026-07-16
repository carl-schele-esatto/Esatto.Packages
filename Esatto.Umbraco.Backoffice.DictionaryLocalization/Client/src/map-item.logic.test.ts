import { describe, it, expect } from "vitest";
import {
  mapResponseToLocalizationSets,
  collectDictionaryKeys,
  normalizeKeyForRegex,
  DICTIONARY_LOCALIZATION_WEIGHT,
} from "./map-item.logic.js";

describe("normalizeKeyForRegex", () => {
  it("returns null when the key has no . or -", () => {
    expect(normalizeKeyForRegex("TestTag")).toBeNull();
    expect(normalizeKeyForRegex("simple_key_123")).toBeNull();
  });

  it("replaces . with _", () => {
    expect(normalizeKeyForRegex("ContactForm.Company")).toBe("ContactForm_Company");
  });

  it("replaces - with _", () => {
    expect(normalizeKeyForRegex("header-contact-cta")).toBe("header_contact_cta");
  });

  it("replaces mixed . and -", () => {
    expect(normalizeKeyForRegex("header.contact-cta.label")).toBe("header_contact_cta_label");
  });
});

describe("mapResponseToLocalizationSets", () => {
  it("emits one set per culture, plus a language alias for region cultures", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: {
        "sv-se": { TestTag: "Testtag" },
        en: { TestTag: "Test tag" },
      },
    });
    // sv-se (region) + en (explicit language) + synthesized sv (from sv-se) = 3
    expect(sets).toHaveLength(3);
    expect(sets.find((s) => s.$code === "sv-se")).toBeDefined();
    expect(sets.find((s) => s.$code === "en")).toBeDefined();
    expect(sets.find((s) => s.$code === "sv")).toBeDefined();
  });

  it("stamps $dir=ltr and the shared $weight", () => {
    const [set] = mapResponseToLocalizationSets({ cultures: { en: { A: "B" } } });
    expect(set.$dir).toBe("ltr");
    expect(set.$weight).toBe(DICTIONARY_LOCALIZATION_WEIGHT);
  });

  it("marks known RTL cultures as rtl", () => {
    const [set] = mapResponseToLocalizationSets({ cultures: { "ar-eg": { Hello: "مرحبا" } } });
    expect(set.$dir).toBe("rtl");
  });

  it("registers both dotted and underscore-normalized aliases for the same value", () => {
    const [set] = mapResponseToLocalizationSets({
      cultures: { en: { "ContactForm.Company": "Company" } },
    });
    expect(set["ContactForm.Company"]).toBe("Company");
    expect(set["ContactForm_Company"]).toBe("Company");
  });

  it("does not double-emit when the key is already flat", () => {
    const [set] = mapResponseToLocalizationSets({
      cultures: { en: { TestTag: "Test tag" } },
    });
    // Only three fields: $code, $dir, $weight, plus the one key.
    const dataKeys = Object.keys(set).filter((k) => !k.startsWith("$"));
    expect(dataKeys).toEqual(["TestTag"]);
  });

  it("normalizes hyphenated keys too", () => {
    const [set] = mapResponseToLocalizationSets({
      cultures: { en: { "header-contact-cta": "Contact" } },
    });
    expect(set["header-contact-cta"]).toBe("Contact");
    expect(set["header_contact_cta"]).toBe("Contact");
  });

  it("drops empty and non-string values silently", () => {
    const [set] = mapResponseToLocalizationSets({
      cultures: {
        en: {
          Empty: "",
          // @ts-expect-error simulate malformed server data
          NotAString: 42,
          Good: "ok",
        },
      },
    });
    expect(set.Empty).toBeUndefined();
    expect(set.NotAString).toBeUndefined();
    expect(set.Good).toBe("ok");
  });

  it("returns an empty array when cultures is missing", () => {
    expect(mapResponseToLocalizationSets({ cultures: {} })).toEqual([]);
    // @ts-expect-error simulate a completely empty response
    expect(mapResponseToLocalizationSets({})).toEqual([]);
  });
});

describe("mapResponseToLocalizationSets — language-only fallback", () => {
  it("synthesizes a language bucket from a region culture so bare-language UI resolves", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: { "sv-se": { Greeting: "Hej" } },
    });
    const sv = sets.find((s) => s.$code === "sv");
    expect(sv).toBeDefined();
    expect(sv!.Greeting).toBe("Hej");
  });

  it("does not synthesize when an explicit language bucket already exists", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: { "sv-se": { A: "region" }, sv: { A: "language" } },
    });
    const svSets = sets.filter((s) => s.$code === "sv");
    expect(svSets).toHaveLength(1);
    expect(svSets[0]!.A).toBe("language"); // explicit value preserved, not overwritten
  });

  it("merges multiple regions of the same language (last region wins on conflict)", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: { "sv-se": { A: "se", Only: "seOnly" }, "sv-fi": { A: "fi" } },
    });
    const sv = sets.find((s) => s.$code === "sv");
    expect(sv).toBeDefined();
    expect(sv!.Only).toBe("seOnly"); // union of keys across regions
    expect(sv!.A).toBe("fi"); // later region (sv-fi) wins the shared key
  });

  it("does not synthesize an alias for a language-only culture", () => {
    const sets = mapResponseToLocalizationSets({ cultures: { en: { A: "B" } } });
    expect(sets).toHaveLength(1);
    expect(sets[0]!.$code).toBe("en");
  });

  it("carries dotted and underscore aliases into the synthesized bucket", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: { "sv-se": { "A.B": "v" } },
    });
    const sv = sets.find((s) => s.$code === "sv");
    expect(sv!["A.B"]).toBe("v");
    expect(sv!["A_B"]).toBe("v");
  });

  it("keeps the synthesized RTL direction from the region's language", () => {
    const sets = mapResponseToLocalizationSets({ cultures: { "ar-eg": { Hi: "مرحبا" } } });
    const ar = sets.find((s) => s.$code === "ar");
    expect(ar).toBeDefined();
    expect(ar!.$dir).toBe("rtl");
  });
});

describe("collectDictionaryKeys", () => {
  it("returns dictionary keys (dotted + normalized) and excludes $ metadata", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: { "sv-se": { "SEO.MetaKeywords.Description": "v", TestTag: "t" } },
    });
    const keys = collectDictionaryKeys(sets);
    expect(keys).toContain("SEO.MetaKeywords.Description");
    expect(keys).toContain("SEO_MetaKeywords_Description"); // normalized alias
    expect(keys).toContain("TestTag");
    expect(keys.some((k) => k.startsWith("$"))).toBe(false);
  });

  it("de-duplicates keys shared across cultures", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: { "sv-se": { A: "a" }, en: { A: "b" } },
    });
    expect(collectDictionaryKeys(sets).filter((k) => k === "A")).toHaveLength(1);
  });
});
