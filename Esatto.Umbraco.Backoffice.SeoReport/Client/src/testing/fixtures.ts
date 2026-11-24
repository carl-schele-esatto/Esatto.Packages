// Small synthetic reports and statuses for the tests. Invented figures only; nothing here comes from a real site.

import type {
  Annotation,
  ChartBlock,
  FetchStatus,
  Kpi,
  Report,
  SearchBlock,
  SearchLandingPage,
  SearchQuery,
  SearchSegment,
  StatusResponse,
} from '../types';

export function chart(overrides: Partial<ChartBlock> = {}): ChartBlock {
  return {
    id: 'traffic_organic',
    type: 'line',
    palette: 'ramp',
    height: 300,
    labels: ['01', '02', '03'],
    series: [{ key: 'year_2026', values: [10, 20, 30], emphasis: true, style: 'solid' }],
    annotations: [],
    insights: [],
    ...overrides,
  };
}

export function kpi(key: string, value: number | null, format = 'integer', overrides: Partial<Kpi> = {}): Kpi {
  return { key, value, format, source: 'ga4', period: '2026-10', trend: null, ...overrides };
}

export function warning(key = 'pagesNotClicked'): Annotation {
  return { period: null, severity: 'warning', text: { key } };
}

export function info(key = 'anonymizedQueries'): Annotation {
  return { period: null, severity: 'info', text: { key } };
}

export function searchBlock(overrides: Partial<SearchBlock> = {}): SearchBlock {
  return {
    id: 'traffic_search',
    segments: [
      { key: 'brand', metrics: [] },
      { key: 'non_brand', metrics: [] },
    ],
    clicks: chart({ id: 'traffic_search_clicks' }),
    impressions: chart({ id: 'traffic_search_impressions' }),
    annotations: [],
    insights: [],
    ...overrides,
  };
}

export function query(overrides: Partial<SearchQuery> = {}): SearchQuery {
  return { query: 'blue widgets', clicks: 120, impressions: 4800, ctr_percent: 2.5, position: 4.2, ...overrides };
}

export function landingPage(overrides: Partial<SearchLandingPage> = {}): SearchLandingPage {
  return {
    url: 'https://www.example.com/widgets/blue',
    path: '/widgets/blue',
    clicks: 80,
    impressions: 2400,
    ctr_percent: 3.33,
    position: 5.1,
    ...overrides,
  };
}

type SegmentFigures = { clicks?: number | null; impressions?: number | null; ctr?: number | null; position?: number | null };

/** A segment with its four measures: clicks (with a rise year on year), impressions, click-through rate and average position. */
export function segment(key: string, figures: SegmentFigures = {}): SearchSegment {
  const pick = (own: number | null | undefined, fallback: number) => (own === undefined ? fallback : own);
  return {
    key,
    metrics: [
      kpi(`${key}_clicks`, pick(figures.clicks, 300), 'integer', {
        source: 'gsc',
        trend: { direction: 'up', change_percent: 12.3, text: { key: 'vsSameMonthLastYear' } },
      }),
      kpi(`${key}_impressions`, pick(figures.impressions, 9000), 'integer', { source: 'gsc' }),
      kpi(`${key}_ctr`, pick(figures.ctr, 3.33), 'percent', { source: 'gsc' }),
      kpi(`${key}_position`, pick(figures.position, 7.4), 'decimal', { source: 'gsc' }),
    ],
  };
}

export function report(overrides: Partial<Report> = {}, search: SearchBlock | null = searchBlock()): Report {
  return {
    schema_version: '1',
    report_period: '2026-10',
    generated_at: '2026-11-02T04:00:00Z',
    data_status: 'live',
    data_status_note: null,
    sources: [],
    traffic: {
      kpis: [kpi('organic_sessions', 1200), kpi('organic_engagement_rate', 62.4, 'percent')],
      organic: chart(),
      total: chart({ id: 'traffic_total' }),
      search_comparison: chart({ id: 'traffic_search_comparison' }),
      search,
    },
    measured: null,
    ...overrides,
  };
}

export function fetchStatus(overrides: Partial<FetchStatus> = {}): FetchStatus {
  return {
    running: false,
    done: 0,
    total: 0,
    currentPeriod: null,
    errorCode: null,
    lastErrorCode: null,
    lastSuccessUtc: null,
    lastFailureUtc: null,
    ...overrides,
  };
}

export function status(overrides: Partial<StatusResponse> = {}): StatusResponse {
  return {
    setup: 'ready',
    missing: [],
    fetch: fetchStatus(),
    upToDate: true,
    periods: ['2026-08', '2026-09', '2026-10'],
    expectedLatestPeriod: '2026-10',
    displayName: null,
    ...overrides,
  };
}
