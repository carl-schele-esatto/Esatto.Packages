// How the client turns what the server sends into words.
//
// The server sends keys (a base id, plus arguments) and never prose; every English text is in lang/en.ts, section esattoSeoReport,
// looked up as esattoSeoReport_<key>. This file is the TypeScript half of Reporting/ReportKeys.cs and has to stay identical to it:
// lang/key-vectors.json is the set of examples both are tested against.

import { displayCode } from './error-codes';
import type { TextArgs, TextRef } from './types';

/** The localization set every text of this package is in: `esattoSeoReport_<key>`. */
export const KEY_PREFIX = 'esattoSeoReport_';

export type Area = 'kpi' | 'chart' | 'series' | 'annotation' | 'insight' | 'narrative' | 'opportunity' | 'target' | 'error' | 'ui';
export type Part = 'Label' | 'Description' | 'Title' | 'Subtitle' | 'Text';

const AREAS: readonly string[] = ['kpi', 'chart', 'series', 'annotation', 'insight', 'narrative', 'opportunity', 'target', 'error', 'ui'];
const PARTS: readonly string[] = ['Label', 'Description', 'Title', 'Subtitle', 'Text'];

// Letters and digits joined by single underscores (ReportKeys.IdPattern). Without the m flag $ matches only at the very end.
const ID = /^[A-Za-z0-9]+(_[A-Za-z0-9]+)*$/;

// A series id that ends in an underscore and EXACTLY four digits (ReportKeys.YearSuffix): "sessions_2026" is the series "sessions"
// of the year 2026. Five digits, three digits or no underscore is not a year.
const YEAR_SUFFIX = /^([A-Za-z0-9]+(?:_[A-Za-z0-9]+)*)_([0-9]{4})$/;

/**
 * The translation key of one text: area + PascalCase(baseId) + part ("kpi", "traffic_organic", "Label" gives kpiTrafficOrganicLabel),
 * exactly <c>ReportKeys.Lookup</c>. The area is only a prefix. For area `series` a trailing "_YYYY" is taken off the id first (see
 * <see cref="seriesText"/>). Throws a RangeError for an unknown area or part and for an id that is not well formed, as the C# does.
 */
export function lookupKey(area: Area, baseId: string, part: Part): string {
  if (!AREAS.includes(area)) throw new RangeError(`Unknown key area '${area}'.`);
  if (!PARTS.includes(part)) throw new RangeError(`Unknown key part '${part}'.`);
  if (!ID.test(baseId)) throw new RangeError(`'${baseId}' is not a valid key id; use letters and digits joined by single underscores.`);

  const id = area === 'series' ? (YEAR_SUFFIX.exec(baseId)?.[1] ?? baseId) : baseId;
  const words = id.split('_').map((word) => word[0].toUpperCase() + word.slice(1));
  return area + words.join('') + part;
}

/** The year a series id carries as its suffix ("clicks_2026" gives 2026), or null when it has none (<c>ReportKeys.SeriesYear</c>). */
export function seriesYear(seriesKey: string): number | null {
  const match = YEAR_SUFFIX.exec(seriesKey);
  return match ? Number(match[2]) : null;
}

/**
 * The label of a series: the key without the year, and the year as the argument <c>year</c> the label reads (no arguments for a
 * series without a year). One text per kind of series instead of one per year.
 */
export function seriesText(seriesKey: string): { key: string; args: { year: number } | undefined } {
  const year = seriesYear(seriesKey);
  return { key: lookupKey('series', seriesKey, 'Label'), args: year === null ? undefined : { year } };
}

// ---- Where a text sits decides its key ---------------------------------------------------------------------------------------
//
// Report.cs lists every place a text can sit and the area and parts it is looked up with, and the analysis classes (Narrative,
// Opportunities, TargetAnalysis) say the same for theirs; these two tables are that list. A text reference's arguments go to every
// part of it unchanged.

interface Place {
  area: Area;
  parts: readonly Part[];
}

/** A place that holds a TextRef (a key with arguments). */
export type TextLocation =
  | 'dataStatusNote'
  | 'sourceNote'
  | 'trend'
  | 'annotation'
  | 'insight'
  | 'narrative'
  | 'opportunity'
  | 'targetAdvice';

const TEXT_LOCATIONS: Record<TextLocation, Place> = {
  dataStatusNote: { area: 'ui', parts: ['Text'] }, // Report.data_status_note
  sourceNote: { area: 'ui', parts: ['Text'] }, // SourceStatus.note
  trend: { area: 'kpi', parts: ['Text'] }, // Trend.text (any KPI)
  annotation: { area: 'annotation', parts: ['Title', 'Text'] }, // Annotation.text
  insight: { area: 'insight', parts: ['Text'] }, // ChartBlock.insights[], SearchBlock.insights[]
  narrative: { area: 'narrative', parts: ['Text'] }, // Statement.text (the analysis' summary)
  opportunity: { area: 'opportunity', parts: ['Title', 'Text'] }, // Opportunity.text: the key is the kind, both parts get the arguments
  targetAdvice: { area: 'target', parts: ['Text'] }, // TargetRow.advice
};

/** A place that holds a bare id (the id itself is the text base). */
export type IdLocation = 'kpi' | 'segmentMetric' | 'chart' | 'searchBlock' | 'source';

const ID_LOCATIONS: Record<IdLocation, Place> = {
  kpi: { area: 'kpi', parts: ['Label', 'Description'] }, // TrafficSection.kpis[].key
  segmentMetric: { area: 'kpi', parts: ['Label'] }, // SearchSegment.metrics[].key
  chart: { area: 'chart', parts: ['Title', 'Subtitle', 'Description'] }, // ChartBlock.id
  searchBlock: { area: 'chart', parts: ['Title', 'Subtitle'] }, // SearchBlock.id
  source: { area: 'ui', parts: ['Label'] }, // SourceStatus.id
};
// A series' label (Series.key, SearchSegment.key) is not here: it has the year rule, which is seriesText.

export type Keys = Partial<Record<Part, string>>;

/** The translation keys of a text reference by where it sits (one per part of that place), and the arguments to write them with. */
export function resolveTextRef(location: TextLocation, ref: TextRef): { keys: Keys; args: TextArgs } {
  const { area, parts } = TEXT_LOCATIONS[location];
  return { keys: Object.fromEntries(parts.map((part) => [part, lookupKey(area, ref.key, part)])), args: ref.args ?? {} };
}

/** The translation keys of an id by where it sits (one per part of that place). */
export function resolveId(location: IdLocation, id: string): Keys {
  const { area, parts } = ID_LOCATIONS[location];
  return Object.fromEntries(parts.map((part) => [part, lookupKey(area, id, part)]));
}

// ---- Problems ------------------------------------------------------------------------------------------------------------------

// ErrorCodes.BaseId: the code in snake_case, which is how a problem's code is registered in ReportKeys (apiDisabled is api_disabled).
const snake = (code: string): string => code.replace(/[A-Z]/g, (letter, index: number) => (index > 0 ? '_' : '') + letter.toLowerCase());

/** The key of a problem's title or text: "error", the code, the part (apiDisabled gives errorApiDisabledTitle). */
export function errorKey(code: string, part: 'Title' | 'Text'): string {
  return lookupKey('error', snake(code), part);
}

// ---- Looking a key up ---------------------------------------------------------------------------------------------------------

/** What the client needs from the element it renders in: Umbraco's localize controller, which every UmbLitElement has. */
export interface Localizer {
  localize: {
    term(key: string, ...args: unknown[]): string;
    /** The language of the backoffice, for Intl. */
    lang(): string;
  };
}

/**
 * The text of a key, in the language of the backoffice, falling back to English. <c>key</c> is without the set's prefix.
 *
 * A text with values is a function of ONE object (the argument names the server sends, see en.ts), and Umbraco calls a function-valued
 * term as <c>term(...args)</c>, so what is passed here must be exactly one object, and never a spread of its values. Missing
 * arguments (<c>undefined</c> or <c>null</c>) become an empty object: a function's default (" = {}") only replaces undefined, and
 * destructuring null throws. A null INSIDE the object is kept, because the texts read it as "nothing to say here".
 */
export function text(host: Localizer, key: string, args?: TextArgs | Record<string, unknown> | null): string {
  return host.localize.term(KEY_PREFIX + key, args ?? {});
}

/**
 * The text of a text reference by where it sits (resolveTextRef), for the part asked for (Text, or Title for an annotation). Nothing for a
 * missing reference and for one whose key is malformed: a bad value in an old archived report must not stop a render half way.
 */
export function refText(host: Localizer, location: TextLocation, ref: TextRef | null | undefined, part: Part = 'Text'): string {
  if (!ref) return '';
  try {
    const { keys, args } = resolveTextRef(location, ref);
    const key = keys[part];
    return key ? text(host, key, args) : '';
  } catch (error) {
    if (error instanceof RangeError) return '';
    throw error;
  }
}

/** The text of an id by where it sits (resolveId), for the part asked for. Nothing for a malformed id or a part that place has none of. */
export function idText(host: Localizer, location: IdLocation, id: string, part: Part, args?: TextArgs | null): string {
  try {
    const key = resolveId(location, id)[part];
    return key ? text(host, key, args) : '';
  } catch (error) {
    if (error instanceof RangeError) return '';
    throw error;
  }
}

/** The title and the text of a problem, by its code (a code the client has no words for reads as `unknown`). */
export function errorTexts(host: Localizer, code: unknown, args?: TextArgs | null): { title: string; text: string } {
  const shown = displayCode(code);
  return { title: text(host, errorKey(shown, 'Title'), args), text: text(host, errorKey(shown, 'Text'), args) };
}

/** The language to format numbers and dates in: the backoffice's. */
export const culture = (host: Localizer): string => host.localize.lang();
