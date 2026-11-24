// Shapes of the server's JSON.
//
// TWO NAMING STYLES, ON PURPOSE. The report and everything inside it (Report.cs) keep the snake_case names its records pin with
// explicit JsonPropertyNames (schema_version, data_status, change_percent, ...). EVERY OTHER response is written by the Management
// API in camelCase (the records in Api/Responses.cs, Api/AnalysisResponse.cs, Api/DailyResponse.cs, Settings/SettingsResponse.cs),
// and an enum arrives as a camelCase word. The server sends no prose: only keys with their arguments, numbers, "YYYY-MM" months
// and "YYYY-MM-DD" days; the English wording is in lang/en.ts.
//
// A property that is nullable in C# is `| null` here (the host writes nulls); a property a record leaves out when it has none is
// optional (`?`).

// ---- Text references -----------------------------------------------------------------------------------------------------

/** The values a text is written with. A value the server had nothing to say about arrives as an explicit null. */
export type TextArgs = Record<string, string | number | boolean | null>;

/** A key and its arguments (TextRef). WHERE the reference sits decides which translation keys it has: see resolveTextRef. */
export interface TextRef {
  key: string;
  /** Left out when there are none. */
  args?: TextArgs;
}

// ---- The report (snake_case) ---------------------------------------------------------------------------------------------

export type DataStatus = 'live' | 'noData';

export interface Report {
  schema_version: string;
  /** "YYYY-MM". */
  report_period: string;
  generated_at: string;
  /** Null reads as "live". */
  data_status: DataStatus | null;
  data_status_note: TextRef | null;
  sources: SourceStatus[];
  traffic: TrafficSection;
  measured: Measured | null;
}

export interface Measured {
  gsc_property: string | null;
  ga4_property: string | null;
  brand_regex: string | null;
}

export interface SourceStatus {
  /** "ga4" or "gsc"; the text base of the source's name (ui<Id>Label). */
  id: string;
  latest_period: string;
  note: TextRef | null;
}

/** integer | signed | percent | decimal | duration (seconds). A format this client does not know is written as a plain number. */
export type KpiFormat = 'integer' | 'signed' | 'percent' | 'decimal' | 'duration' | (string & {});

export interface Kpi {
  key: string;
  /** Null is "no data", never zero. */
  value: number | null;
  format: KpiFormat;
  source: string | null;
  period: string | null;
  trend: Trend | null;
}

export type TrendDirection = 'up' | 'down' | 'note';

export interface Trend {
  direction: TrendDirection | (string & {});
  /** Null for a note without a computed change. */
  change_percent: number | null;
  text: TextRef | null;
}

export type ChartType = 'line' | 'bar';
export type ChartPalette = 'ramp' | 'categorical' | 'segment';

export interface ChartBlock {
  id: string;
  type: ChartType;
  palette: ChartPalette;
  height: number;
  labels: string[];
  series: Series[];
  annotations: Annotation[];
  insights: TextRef[];
}

export interface Series {
  key: string;
  /** As long as the chart's labels; null is a gap, never zero. */
  values: (number | null)[];
  emphasis: boolean;
  style: 'solid' | 'dashed';
}

export type AnnotationSeverity = 'warning' | 'info';

export interface Annotation {
  period: string | null;
  severity: AnnotationSeverity;
  text: TextRef;
}

export interface TrafficSection {
  kpis: Kpi[];
  organic: ChartBlock;
  total: ChartBlock;
  search_comparison: ChartBlock;
  search: SearchBlock | null;
}

export interface SearchBlock {
  id: string;
  segments: SearchSegment[];
  clicks: ChartBlock;
  impressions: ChartBlock;
  annotations: Annotation[];
  insights: TextRef[];
  queries?: SearchQuery[] | null;
  pages?: SearchLandingPage[] | null;
  total?: SearchSegment | null;
  query_pages?: SearchQueryPage[] | null;
}

export interface SearchSegment {
  /** "brand" | "non_brand", or "total" for SearchBlock.total. */
  key: string;
  metrics: Kpi[];
}

export interface SearchQuery {
  query: string;
  clicks: number;
  impressions: number;
  ctr_percent: number;
  position: number;
}

export interface SearchLandingPage {
  url: string;
  path: string | null;
  clicks: number;
  impressions: number;
  ctr_percent: number;
  position: number;
}

export interface SearchQueryPage {
  query: string;
  url: string;
  path: string | null;
  clicks: number;
  impressions: number;
  ctr_percent: number;
  position: number;
}

// ---- Status, report envelope, fetch (camelCase) --------------------------------------------------------------------------

export type SetupState = 'notConfigured' | 'keyUnreadable' | 'ready';

/** What is still needed to be ready: key, ga4PropertyId, searchConsoleSite. */
export type MissingSetting = 'key' | 'ga4PropertyId' | 'searchConsoleSite';

/**
 * How the backfill is going. <c>running</c> with <c>total</c> 0 is "starting" (the months are counted a moment later); not running
 * with <c>total</c> 0 after a run is "nothing to fetch".
 */
export interface FetchStatus {
  running: boolean;
  done: number;
  total: number;
  currentPeriod: string | null;
  /** Why the latest run ended badly. */
  errorCode: string | null;
  /** The last recorded failure, which survives a restart. */
  lastErrorCode: string | null;
  lastSuccessUtc: string | null;
  lastFailureUtc: string | null;
}

export interface StatusResponse {
  setup: SetupState;
  missing: MissingSetting[];
  fetch: FetchStatus;
  upToDate: boolean;
  /** The archived months, "YYYY-MM", OLDEST first. */
  periods: string[];
  expectedLatestPeriod: string;
  displayName: string | null;
}

export interface ReportResponse {
  report: Report | null;
  note: string | null;
  fetchedUtc: string | null;
  /** The month asked for, or the newest; null when nothing is archived. */
  period: string | null;
}

export interface StartFetchResponse {
  result: 'started';
  progress: FetchStatus;
}

export interface NoteResponse {
  note: string | null;
}

export interface TargetsResponse {
  terms: string[];
}

export interface UserGroupItem {
  alias: string;
  name: string;
}

// ---- Analysis (camelCase) ------------------------------------------------------------------------------------------------

export interface Statement {
  text: TextRef;
}

export interface MoveDelta {
  before: number;
  delta: number;
  /** Null for a page that is new. */
  changePercent: number | null;
  positionBefore: number | null;
}

export interface Move {
  path: string;
  url: string;
  impressions: number;
  position: number | null;
  vsPrevious: MoveDelta | null;
  vsYearAgo: MoveDelta | null;
}

export interface MovementResult {
  period: string;
  previous: { period: string } | null;
  yearAgo: { period: string } | null;
  rankedBy: string;
  losses: Move[];
  gains: Move[];
}

export interface TargetGrid {
  months: { period: string }[];
  rows: { term: string; positions: (number | null)[] }[];
}

export interface Opportunity {
  kind: string;
  text: TextRef;
  effort: number;
}

export interface TargetPage {
  path: string;
  position: number;
  ctrPercent: number;
  impressions: number;
  clicks: number;
  queries: number;
}

export interface TargetRow {
  term: string;
  status: string;
  impressions: number;
  clicks: number;
  ctrPercent: number;
  position: number | null;
  matches: number;
  example: string | null;
  advice: TextRef;
  page: TargetPage | null;
}

export interface TargetAnalysisResponse {
  rowsFetched: number;
  rows: TargetRow[];
}

/** The body of GET analysis when the archive holds nothing to analyse: exactly this. Read `hasAnalysis` first. */
export interface NoAnalysisResponse {
  hasAnalysis: false;
}

export interface AnalysisResponse {
  hasAnalysis: true;
  period: string;
  summary: Statement[];
  note: string | null;
  targetHistory: TargetGrid | null;
  movement: MovementResult | null;
  /** Null for a user outside the findings groups. */
  opportunities: Opportunity[] | null;
  targets: TargetAnalysisResponse | null;
}

// ---- Daily (camelCase) ---------------------------------------------------------------------------------------------------

export interface DailyResponse {
  chart: ChartBlock;
  /** period (the month, or null in the rolling mode), windowDays, provisionalDays (null when none can be revised). */
  textArgs: TextArgs;
  /** The empty string (the rolling window) and then the last twelve months, "YYYY-MM", newest first. */
  months: string[];
  window: { start: string; end: string; month: string | null };
  total: number;
  changePercent: number | null;
  peak: { date: string; sessions: number } | null;
  days: { date: string; sessions: number }[];
  windowDays: number;
}

export interface DailySourcesResponse {
  date: string;
  sources: { channel: string; source: string; sessions: number }[];
}

// ---- Settings (camelCase) ------------------------------------------------------------------------------------------------

export interface SeoReportSettings {
  ga4PropertyId: string | null;
  searchConsoleSite: string | null;
  brandTerms: string[];
  displayName: string | null;
  findingsUserGroups: string[];
  autoFetch: boolean;
}

export type KeySourceKind = 'none' | 'configuration' | 'database';

export interface KeyStatus {
  hasKey: boolean;
  source: KeySourceKind;
  serviceAccountEmail: string | null;
  unreadable: boolean;
}

export interface SettingsResponse {
  settings: SeoReportSettings;
  /** The camelCase names of the fields configuration sets (read-only). */
  locked: string[];
  key: KeyStatus;
}

/** The body of PUT settings. The key is write-only: it goes out here and never comes back in any response. */
export interface PutSettingsRequest {
  settings: SeoReportSettings;
  serviceAccountJson: string | null;
  removeKey: boolean;
}

/** The body of Test connection: the values in the form. A key that was entered goes out here and never comes back; left out, the key in force is tested. */
export interface ConnectionTestRequest {
  ga4PropertyId: string | null;
  searchConsoleSite: string | null;
  serviceAccountJson?: string | null;
}

export interface SourceTest {
  ok: boolean;
  errorCode: string | null;
  args: TextArgs | null;
}

export interface ConnectionTestResult {
  serviceAccountEmail: string | null;
  key: SourceTest;
  ga4: SourceTest;
  searchConsole: SourceTest;
}
