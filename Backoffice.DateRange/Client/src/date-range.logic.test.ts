import { describe, it, expect } from "vitest";
import {
  normalizeValue,
  isRangeComplete,
  isRangeValid,
  applyStartChange,
  applyEndChange,
} from "./date-range.logic.js";

describe("normalizeValue", () => {
  it("returns an empty range for null/undefined", () => {
    expect(normalizeValue(null)).toEqual({ from: null, to: null });
    expect(normalizeValue(undefined)).toEqual({ from: null, to: null });
  });

  it("passes through a valid object", () => {
    expect(normalizeValue({ from: "2026-05-01", to: "2026-05-10" })).toEqual({
      from: "2026-05-01",
      to: "2026-05-10",
    });
  });

  it("coerces missing keys to null", () => {
    expect(normalizeValue({ from: "2026-05-01" })).toEqual({
      from: "2026-05-01",
      to: null,
    });
  });

  it("ignores non-object junk", () => {
    expect(normalizeValue("nonsense")).toEqual({ from: null, to: null });
  });
});

describe("isRangeComplete", () => {
  it("is true only when both ends are set", () => {
    expect(isRangeComplete({ from: "2026-05-01", to: "2026-05-10" })).toBe(true);
    expect(isRangeComplete({ from: "2026-05-01", to: null })).toBe(false);
    expect(isRangeComplete({ from: null, to: "2026-05-10" })).toBe(false);
    expect(isRangeComplete({ from: null, to: null })).toBe(false);
  });
});

describe("isRangeValid", () => {
  it("is valid when incomplete (nothing to compare yet)", () => {
    expect(isRangeValid({ from: "2026-05-01", to: null })).toBe(true);
    expect(isRangeValid({ from: null, to: null })).toBe(true);
  });

  it("is valid when from <= to", () => {
    expect(isRangeValid({ from: "2026-05-01", to: "2026-05-10" })).toBe(true);
    expect(isRangeValid({ from: "2026-05-01", to: "2026-05-01" })).toBe(true);
  });

  it("is invalid when from > to", () => {
    expect(isRangeValid({ from: "2026-05-10", to: "2026-05-01" })).toBe(false);
  });
});

describe("applyStartChange", () => {
  it("sets the start and keeps a still-valid end", () => {
    const result = applyStartChange({ from: "2026-05-01", to: "2026-05-10" }, "2026-05-03");
    expect(result).toEqual({ from: "2026-05-03", to: "2026-05-10" });
  });

  it("clears the end when the new start is after it", () => {
    const result = applyStartChange({ from: "2026-05-01", to: "2026-05-05" }, "2026-05-10");
    expect(result).toEqual({ from: "2026-05-10", to: null });
  });

  it("keeps the end when new start equals it", () => {
    const result = applyStartChange({ from: "2026-05-01", to: "2026-05-05" }, "2026-05-05");
    expect(result).toEqual({ from: "2026-05-05", to: "2026-05-05" });
  });
});

describe("applyEndChange", () => {
  it("sets the end", () => {
    const result = applyEndChange({ from: "2026-05-01", to: null }, "2026-05-09");
    expect(result).toEqual({ from: "2026-05-01", to: "2026-05-09" });
  });
});
