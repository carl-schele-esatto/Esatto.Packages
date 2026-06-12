import { describe, it, expect } from "vitest";
import { mapItem } from "./map-item.logic.js";

describe("mapItem", () => {
  it("maps a full item to the dictionary collection model", () => {
    const result = mapItem({
      id: "abc",
      name: "MyKey",
      parent: { id: "parent-id" },
      translatedIsoCodes: ["en", "sv"],
    });
    expect(result).toEqual({
      entityType: "dictionary",
      unique: "abc",
      name: "MyKey",
      parentUnique: "parent-id",
      translatedIsoCodes: ["en", "sv"],
    });
  });

  it("maps a null or absent parent to parentUnique null", () => {
    expect(mapItem({ id: "x", name: "Root", parent: null }).parentUnique).toBeNull();
    expect(mapItem({ id: "y", name: "Root2" }).parentUnique).toBeNull();
  });

  it("defaults a missing name and translatedIsoCodes", () => {
    const result = mapItem({ id: "z" });
    expect(result.name).toBe("");
    expect(result.translatedIsoCodes).toEqual([]);
    expect(result.entityType).toBe("dictionary");
  });
});
