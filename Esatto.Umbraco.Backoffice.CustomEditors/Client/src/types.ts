/** The stored property value. ISO 8601 strings, or null when not yet picked. */
export interface UmbDateRangeValue {
  from: string | null;
  to: string | null;
}

/** Resolved configuration for the editor, read from the data type settings. */
export interface DateRangeConfig {
  /** When true, each end also has a time component (date+time mode). */
  includeTime: boolean;
  /** Optional absolute earliest date (ISO 8601) the range must fall within. */
  minDate: string | null;
  /** Optional absolute latest date (ISO 8601) the range must fall within. */
  maxDate: string | null;
}

export const EMPTY_RANGE: UmbDateRangeValue = { from: null, to: null };
