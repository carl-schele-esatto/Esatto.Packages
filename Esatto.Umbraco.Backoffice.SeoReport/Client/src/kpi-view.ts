// How one key figure is written, without a DOM: the value by its format, its label and description, its icon, and its trend as an arrow, the
// words and the size of the change. Shared by the overview card, the key-figure tile and the search table, so a figure reads the same everywhere.
//
// A figure that is null is "No data", written as words: never a dash that reads as a value, and never a zero that reads as a measurement.
// A trend's direction is read from the SIGN of its figure and written as an arrow AND a word; the tone (green, red, blue) is an extra signal.

import { formatKpiValue, formatPercent } from './format';
import { culture, idText, refText, text, type Localizer } from './localize';
import { trendView } from './report-helpers';
import type { Kpi, Trend } from './types';

export type Tone = 'good' | 'critical' | 'notice';
export type Direction = 'up' | 'down' | 'flat';

// A decorative icon per figure (aria-hidden: the label carries the meaning). An unknown key gets the chart icon, so a new figure in the report
// is drawn without this file having to change.
const KPI_ICONS: Record<string, string> = {
  organic_sessions: 'icon-search',
  organic_engagement_rate: 'icon-hand-pointer',
  organic_session_seconds: 'icon-time',
  organic_key_events: 'icon-message',
  total_sessions: 'icon-users',
  gsc_clicks: 'icon-mouse-cursor',
  clicks_vs_sessions: 'icon-split',
};

export const kpiIcon = (key: string): string => KPI_ICONS[key] ?? 'icon-chart';

/** Up is green, down is red, anything else blue. The arrow and the words say the same, so the tone is never the only carrier. */
export const toneOf = (direction: Direction | null | undefined): Tone => (direction === 'up' ? 'good' : direction === 'down' ? 'critical' : 'notice');

export interface TrendText {
  /** The arrow, the word and the size of the change; null when the trend has no figure (a note, a missing comparison). */
  pill: { direction: Direction; arrow: string; words: string; figure: string } | null;
  /** What the change is measured against ("vs the same month last year"), or a free-standing note. */
  note: string;
}

/** A trend written for the reader, or null when it has neither a figure nor a text. */
export function trendText(host: Localizer, trend: Trend | null | undefined): TrendText | null {
  const view = trendView(trend);
  const note = refText(host, 'trend', trend?.text);
  if (!view && !note) return null;

  const wordsKey = view?.direction === 'up' ? 'uiTrendUpLabel' : view?.direction === 'down' ? 'uiTrendDownLabel' : 'uiTrendFlatLabel';
  return {
    pill: view
      ? {
          direction: view.direction,
          arrow: view.arrow,
          words: text(host, wordsKey),
          // "flat" has no direction of its own and keeps its sign.
          figure: formatPercent(view.change, culture(host), { fixed: true, signed: view.direction === 'flat' }),
        }
      : null,
    note,
  };
}

export interface KpiTileView {
  label: string;
  description: string;
  /** The figure written by its format, or "No data" when it is null. */
  value: string;
  hasValue: boolean;
  icon: string;
  tone: Tone;
  trend: TrendText | null;
}

export function kpiTileView(host: Localizer, kpi: Kpi): KpiTileView {
  const written = formatKpiValue(kpi.value, kpi.format, culture(host));
  const trend = trendText(host, kpi.trend);

  return {
    label: idText(host, 'kpi', kpi.key, 'Label'),
    description: idText(host, 'kpi', kpi.key, 'Description'),
    value: written || text(host, 'uiNoValueText'),
    hasValue: written !== '',
    icon: kpiIcon(kpi.key),
    tone: toneOf(trend?.pill?.direction),
    trend,
  };
}
