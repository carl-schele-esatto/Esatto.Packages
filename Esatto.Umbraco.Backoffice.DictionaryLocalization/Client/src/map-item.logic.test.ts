import { describe, it, expect } from "vitest";
import {
  mapResponseToLocalizationSets,
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
  it("emits one set per culture", () => {
    const sets = mapResponseToLocalizationSets({
      cultures: {
        "sv-se": { TestTag: "Testtag" },
        en: { TestTag: "Test tag" },
      },
    });
    expect(sets).toHaveLength(2);
    expect(sets.find((s) => s.$code === "sv-se")).toBeDefined();
    expect(sets.find((s) => s.$code === "en")).toBeDefined();
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
