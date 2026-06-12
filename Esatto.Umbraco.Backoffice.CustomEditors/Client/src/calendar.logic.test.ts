import { describe, it, expect } from "vitest";
import {
  toDayKey,
  buildMonthGrid,
  isDayDisabled,
  parseDayKey,
  dayPart,
} from "./calendar.logic.js";

describe("toDayKey", () => {
  it("formats a date as YYYY-MM-DD in local time", () => {
    expect(toDayKey(new Date(2026, 4, 1))).toBe("2026-05-01");
    expect(toDayKey(new Date(2026, 11, 9))).toBe("2026-12-09");
  });
});

describe("parseDayKey", () => {
  it("parses a YYYY-MM-DD key into a local date (no UTC day shift)", () => {
    const d = parseDayKey("2026-03-01");
    expect(d.getFullYear()).toBe(2026);
    expect(d.getMonth()).toBe(2);
    expect(d.getDate()).toBe(1);
  });

  it("slices off any time component before parsing", () => {
    const d = parseDayKey("2026-03-01T14:00:00");
    expect(d.getFullYear()).toBe(2026);
    expect(d.getMonth()).toBe(2);
    expect(d.getDate()).toBe(1);
  });

  it("round-trips through toDayKey", () => {
    expect(toDayKey(parseDayKey("2026-12-31"))).toBe("2026-12-31");
  });
});

describe("dayPart", () => {
  it("returns the date portion of an ISO string", () => {
    expect(dayPart("2026-05-03T14:00:00")).toBe("2026-05-03");
    expect(dayPart("2026-05-03")).toBe("2026-05-03");
  });

  it("is null-aware", () => {
    expect(dayPart(null)).toBe(null);
  });
});

describe("buildMonthGrid", () => {
  it("returns whole weeks (length divisible by 7)", () => {
    const grid = buildMonthGrid(2026, 4); // May 2026
    expect(grid.length % 7).toBe(0);
  });

  it("includes every day of the target month", () => {
    const grid = buildMonthGrid(2026, 4); // May has 31 days
    const inMonth = grid.filter((d) => d.inCurrentMonth);
    expect(inMonth.length).toBe(31);
    expect(inMonth[0].key).toBe("2026-05-01");
    expect(inMonth[30].key).toBe("2026-05-31");
  });

  it("starts the grid on a Monday", () => {
    const grid = buildMonthGrid(2026, 4); // 1 May 2026 is a Friday
    // First cell is the Monday on/before the 1st => 27 Apr 2026
    expect(grid[0].key).toBe("2026-04-27");
    expect(grid[0].inCurrentMonth).toBe(false);
  });
});

describe("isDayDisabled", () => {
  it("disables days before min", () => {
    expect(isDayDisabled("2026-05-01", "2026-05-03", null)).toBe(true);
    expect(isDayDisabled("2026-05-03", "2026-05-03", null)).toBe(false);
    expect(isDayDisabled("2026-05-05", "2026-05-03", null)).toBe(false);
  });

  it("disables days after max", () => {
    expect(isDayDisabled("2026-05-31", null, "2026-05-10")).toBe(true);
    expect(isDayDisabled("2026-05-10", null, "2026-05-10")).toBe(false);
  });

  it("never disables when bounds are null", () => {
    expect(isDayDisabled("2026-05-01", null, null)).toBe(false);
  });

  it("compares dates only, ignoring any time component on the bound", () => {
    expect(isDayDisabled("2026-05-03", "2026-05-03T14:00:00", null)).toBe(false);
  });
});
