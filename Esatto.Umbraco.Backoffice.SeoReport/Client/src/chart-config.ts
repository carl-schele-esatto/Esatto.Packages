// The Chart.js configuration for a chart block, built as plain data (no DOM, no Chart.js code at run time: the type import below is erased), so
// every rule about how a chart looks is tested without a browser. The chart card hands the result to Chart.js.
//
// THE RULES
//   - A null value is a gap: the line breaks (spanGaps false), it is never drawn as zero. Outside Search Console's sixteen months there is no
//     data point, and drawing one as 0 would be a solution to the wrong problem.
//   - Colour is never the only carrier. `emphasis` is a thicker line with larger points and a faint fill, `style: dashed` is a dashed line,
//     and every chart with two series or more has a legend of series NAMES whose entries repeat the line's colour, width and dash.
//   - Colours come from the palette the block names, resolved for the theme the page is in (chart-colors.ts), and the furniture (grid, ticks,
//     tooltip) takes the page's own text and surface colours, so the chart reads in the light, dark and high-contrast backoffice themes. In
//     Windows' forced-colours mode (which repaints the page but not a canvas) the theme is made of the SYSTEM colours: the newest series in the
//     text colour, the older ones stepping toward GrayText, and a marker shape per series.
//   - `reducedMotion` turns every animation off.
//   - Words are written for the language: axis labels, series names, the value axis and the tooltip.

import type { ChartConfiguration } from 'chart.js';
import { paletteColors, withAlpha, type ChartTheme } from './chart-colors';
import { chartLabels, chartSeriesLabel } from './chart-view';
import { formatCompact, formatNumber } from './format';
import { culture, text, type Localizer } from './localize';
import type { ChartBlock } from './types';

export interface ChartConfigOptions {
  host: Localizer;
  theme: ChartTheme;
  /** The person prefers less motion (prefers-reduced-motion): nothing is animated. */
  reducedMotion: boolean;
  /** Called with the index of the point (the label) that was clicked. Without it the chart is not clickable. */
  onPoint?: (index: number) => void;
}

const DASH = [4, 3];

interface Look {
  color: string;
  width: number;
  dashed: boolean;
  emphasis: boolean;
}

// How each series looks, the one source for the plot and the legend.
function looks(block: ChartBlock, theme: ChartTheme): Look[] {
  const series = block.series ?? [];
  const colors = paletteColors(block.palette, series.length, theme);

  return series.map((item, index) => ({
    // In forced-colours mode the emphasised (newest) series is always in the text colour: the one with the most contrast against Canvas.
    color: theme.forced && item.emphasis === true ? theme.text : (colors[index] ?? theme.neutral),
    width: item.emphasis ? 2.5 : 1.5,
    dashed: item.style === 'dashed',
    emphasis: item.emphasis === true,
  }));
}

// In forced-colours mode a chart has two colours or a ramp of greys, which is a weak carrier on its own, so each series also gets its own marker
// shape. Outside it the points are the library's default circle, and no shape is set.
const MARKERS = ['circle', 'rect', 'triangle', 'rectRot', 'cross', 'star'] as const;

export interface LegendItem {
  label: string;
  color: string;
  width: number;
  dashed: boolean;
}

/** The legend: the series' names, each with its line's colour, width and dash. None for fewer than two series, which the heading already names. */
export function legendItems(block: ChartBlock, { host, theme }: Pick<ChartConfigOptions, 'host' | 'theme'>): LegendItem[] {
  const series = block.series ?? [];
  if (series.length < 2) return [];

  const styles = looks(block, theme);
  // At least 2 px, so the underline of a legend entry shows for the thin, unemphasised series as well.
  return series.map((item, index) => ({
    label: chartSeriesLabel(host, item),
    color: styles[index].color,
    width: Math.max(2, styles[index].width),
    dashed: styles[index].dashed,
  }));
}

const isWhole = (values: (number | null)[]): boolean => values.every((value) => value === null || Number.isInteger(value));

/** The Chart.js configuration of a block. */
export function chartConfig(block: ChartBlock, { host, theme, reducedMotion, onPoint }: ChartConfigOptions): ChartConfiguration<'line' | 'bar'> {
  const c = culture(host);
  const isBar = block.type === 'bar';
  const series = block.series ?? [];
  const styles = looks(block, theme);
  const noData = text(host, 'uiNoValueText');
  const tableLabels = chartLabels(block, c, 'table');

  const datasets = series.map((item, index) => {
    const { color, width, dashed, emphasis } = styles[index];
    const base = { label: chartSeriesLabel(host, item), data: [...(item.values ?? [])] };

    if (isBar) {
      return { ...base, backgroundColor: withAlpha(color, 0.2), borderColor: color, borderWidth: 1.5, borderRadius: 5, borderSkipped: false as const };
    }

    return {
      ...base,
      borderColor: color,
      backgroundColor: emphasis ? withAlpha(color, 0.1) : 'transparent',
      fill: emphasis ? ('origin' as const) : false,
      borderWidth: width,
      borderDash: dashed ? [...DASH] : [],
      pointRadius: emphasis ? 4 : 2.5,
      pointHoverRadius: 6,
      pointBackgroundColor: color,
      ...(theme.forced ? { pointStyle: MARKERS[index % MARKERS.length] } : {}),
      tension: 0.3,
      // Null is no data point. The line breaks there; it does not fall to zero.
      spanGaps: false,
    };
  });

  const values = series.flatMap((item) => item.values ?? []);
  const largest = Math.max(0, ...values.filter((value): value is number => typeof value === 'number'));
  // Abbreviated only once the scale needs it, so small figures do not become "0K".
  const abbreviate = largest >= 10000;
  // In forced-colours mode the grid is GrayText, also the colour the oldest series starts from: dotted, so it does not read as a line of data. (In
  // Chart.js 4 the dash of the grid lines is `border.dash` of the scale.)
  const gridDash = theme.forced ? { border: { dash: [1, 4] } } : {};

  const config = {
    type: isBar ? 'bar' : 'line',
    data: { labels: chartLabels(block, c, 'axis'), datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      ...(reducedMotion ? { animation: false as const } : {}),
      interaction: { mode: 'index' as const, intersect: false },
      ...(onPoint ? { onClick: (_event: unknown, elements: { index: number }[]) => (elements.length > 0 ? onPoint(elements[0].index) : undefined) } : {}),
      plugins: {
        // The card draws the legend as text.
        legend: { display: false },
        tooltip: {
          backgroundColor: theme.tooltipBackground,
          titleColor: theme.tooltipTitle,
          bodyColor: theme.tooltipText,
          padding: 10,
          cornerRadius: 8,
          titleFont: { size: 11 },
          bodyFont: { size: 12 },
          callbacks: {
            title: (items: { dataIndex: number }[]) => tableLabels[items[0]?.dataIndex] ?? '',
            label: (item: { dataset: { label?: string }; parsed: { y: number | null } }) =>
              ` ${item.dataset.label ?? ''}: ${item.parsed.y === null || item.parsed.y === undefined ? noData : formatNumber(item.parsed.y, c)}`,
          },
        },
      },
      scales: {
        x: { grid: isBar ? { display: false } : { color: theme.grid, lineWidth: 1 }, ...gridDash, ticks: { color: theme.muted, maxRotation: 0 } },
        y: {
          beginAtZero: true,
          grid: { color: theme.grid, lineWidth: 1 },
          ...gridDash,
          ticks: {
            color: theme.muted,
            ...(isWhole(values) ? { precision: 0 } : {}),
            callback: (value: string | number) => (typeof value === 'number' ? (abbreviate ? formatCompact(value, c) : formatNumber(value, c)) : value),
          },
        },
      },
    },
  };

  return config as unknown as ChartConfiguration<'line' | 'bar'>;
}
