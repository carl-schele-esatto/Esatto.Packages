import { type UmbDateRangeValue, EMPTY_RANGE } from "./types.js";

/** Convert whatever Umbraco hands us into a well-formed range object. */
export function normalizeValue(value: unknown): UmbDateRangeValue {
  if (value && typeof value === "object") {
    const v = value as Record<string, unknown>;
    return {
      from: typeof v.from === "string" ? v.from : null,
      to: typeof v.to === "string" ? v.to : null,
    };
  }
  // Spread so callers never share the module-level constant's reference.
  return { ...EMPTY_RANGE };
}

/** Both ends picked. */
export function isRangeComplete(value: UmbDateRangeValue): boolean {
  return value.from !== null && value.to !== null;
}

/** Valid if incomplete, or if from <= to. */
export function isRangeValid(value: UmbDateRangeValue): boolean {
  if (value.from === null || value.to === null) return true;
  return new Date(value.from).getTime() <= new Date(value.to).getTime();
}

/** Set the start; clear the end if it now precedes the start. */
export function applyStartChange(
  current: UmbDateRangeValue,
  newFrom: string
): UmbDateRangeValue {
  const endBeforeStart =
    current.to !== null &&
    new Date(current.to).getTime() < new Date(newFrom).getTime();
  return { from: newFrom, to: endBeforeStart ? null : current.to };
}

/** Set the end. */
export function applyEndChange(
  current: UmbDateRangeValue,
  newTo: string
): UmbDateRangeValue {
  return { from: current.from, to: newTo };
}
