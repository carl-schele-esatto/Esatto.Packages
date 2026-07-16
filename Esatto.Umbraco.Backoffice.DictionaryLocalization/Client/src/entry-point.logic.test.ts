import { describe, it, expect, beforeEach } from "vitest";
import {
  fetchAndRegisterDictionaryLocalizations,
  resetRegisteredStateForTest,
  type LocalizationManagerLike,
  type FetchLocalizationsFn,
} from "./entry-point.logic.js";
import type { LocalizationSet } from "./map-item.logic.js";

class FakeManager implements LocalizationManagerLike {
  received: LocalizationSet[][] = [];
  registerManyLocalizations(sets: LocalizationSet[]) {
    this.received.push(sets);
  }
}

const okResponse: FetchLocalizationsFn = () =>
  Promise.resolve({
    data: {
      cultures: {
        "sv-se": { TestTag: "Testtag" },
        en: { TestTag: "Test tag" },
      },
    },
  });

describe("fetchAndRegisterDictionaryLocalizations", () => {
  beforeEach(() => resetRegisteredStateForTest());

  it("registers a set per culture plus a language alias on first call", async () => {
    const manager = new FakeManager();
    const registered = await fetchAndRegisterDictionaryLocalizations(manager, okResponse);

    expect(registered).toBe(true);
    expect(manager.received).toHaveLength(1);
    const sets = manager.received[0]!;
    // en (explicit), sv-se (region), and the synthesized sv language fallback.
    expect(sets.map((s) => s.$code).sort()).toEqual(["en", "sv", "sv-se"]);
  });

  it("is a no-op on the second call within the same session", async () => {
    const manager = new FakeManager();
    await fetchAndRegisterDictionaryLocalizations(manager, okResponse);
    const second = await fetchAndRegisterDictionaryLocalizations(manager, okResponse);

    expect(second).toBe(false);
    expect(manager.received).toHaveLength(1); // still just the first call
  });

  it("leaves the guard unset when the fetch errors, so a retry can register", async () => {
    const manager = new FakeManager();
    const failing: FetchLocalizationsFn = () => Promise.resolve({ error: new Error("boom") });

    const first = await fetchAndRegisterDictionaryLocalizations(manager, failing);
    expect(first).toBe(false);
    expect(manager.received).toHaveLength(0);

    const second = await fetchAndRegisterDictionaryLocalizations(manager, okResponse);
    expect(second).toBe(true);
    expect(manager.received).toHaveLength(1);
  });

  it("treats a missing `data` field as a failure", async () => {
    const manager = new FakeManager();
    const empty: FetchLocalizationsFn = () => Promise.resolve({});

    const result = await fetchAndRegisterDictionaryLocalizations(manager, empty);
    expect(result).toBe(false);
    expect(manager.received).toHaveLength(0);
  });
});
