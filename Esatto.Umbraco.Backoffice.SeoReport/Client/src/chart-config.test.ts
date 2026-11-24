import { describe, expect, it, vi } from 'vitest';
import { DARK_THEME, LIGHT_THEME, paletteColors, withAlpha } from './chart-colors';
import { chartConfig, legendItems, type ChartConfigOptions } from './chart-config';
import { chart } from './testing/fixtures';
import { enHost } from './testing/host';
import type { ChartBlock, Series } from './types';

const options = (overrides: Partial<ChartConfigOptions> = {}): ChartConfigOptions => ({
  host: enHost(),
  theme: LIGHT_THEME,
  reducedMotion: false,
  ...overrides,
});

const series = (key: string, values: (number | null)[], overrides: Partial<Series> = {}): Series => ({
  key,
  values,
  emphasis: false,
  style: 'solid',
  ...overrides,
});

// The configuration is plain data and a few callbacks; read it loosely, as Chart.js does.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
const cfg = (block: ChartBlock, overrides?: Partial<ChartConfigOptions>) => chartConfig(block, options(overrides)) as any;

const yearChart = chart({
  labels: ['01', '02', '03'],
  series: [
    series('year_2024', [1, 2, 3], { style: 'dashed' }),
    series('year_2025', [2, null, 4]),
    series('year_2026', [3, 4, 5], { emphasis: true }),
  ],
});

describe('chartConfig: the data', () => {
  it('has one dataset per series, in order, named by the series label, with the values as they are', () => {
    const config = cfg(yearChart);

    expect(config.type).toBe('line');
    expect(config.data.datasets.map((set: { label: string }) => set.label)).toEqual(['2024', '2025', '2026']);
    expect(config.data.datasets[0].data).toEqual([1, 2, 3]);
    expect(config.data.labels).toEqual(['Jan', 'Feb', 'Mar']);
  });

  it('keeps a gap a gap: null stays null and the line breaks there instead of dropping to zero', () => {
    const config = cfg(yearChart);

    expect(config.data.datasets[1].data).toEqual([2, null, 4]);
    for (const set of config.data.datasets) expect(set.spanGaps).toBe(false);
  });

  it('does not share the series values with the chart block (the block is never changed by drawing)', () => {
    const config = cfg(yearChart);

    expect(config.data.datasets[0].data).not.toBe(yearChart.series[0].values);
  });

  it('writes the axis labels for the language of the page', () => {
    const periods = chart({ labels: ['2026-01', '2026-02'], series: [series('brand', [1, 2])] });

    expect(cfg(periods).data.labels).toEqual(['Jan 2026', 'Feb 2026']);
    expect(cfg(periods, { host: enHost('sv') }).data.labels).toEqual(['jan. 2026', 'feb. 2026']);
  });

  it('is a bar chart for a bar block, with the same gaps and a tinted bar', () => {
    const bars = chart({ type: 'bar', labels: ['2026-01'], series: [series('brand', [null])] });
    const config = cfg(bars);

    expect(config.type).toBe('bar');
    expect(config.data.datasets[0].data).toEqual([null]);
    expect(config.data.datasets[0].backgroundColor).toMatch(/^rgba\(/);
  });

  it('has no datasets for a block without series (the card does not draw it, but the builder must not throw)', () => {
    expect(cfg(chart({ series: [] })).data.datasets).toEqual([]);
  });
});

describe('chartConfig: how a series looks', () => {
  it('dashes a dashed series and leaves a solid one solid: colour is not the only carrier', () => {
    const [dashed, solid] = cfg(yearChart).data.datasets;

    expect(dashed.borderDash).toEqual([4, 3]);
    expect(solid.borderDash).toEqual([]);
  });

  it('draws the emphasised series thicker, with larger points and a faint fill under it, and the rest thin and unfilled', () => {
    const [, plain, emphasised] = cfg(yearChart).data.datasets;

    expect(emphasised.borderWidth).toBeGreaterThan(plain.borderWidth);
    expect(emphasised.pointRadius).toBeGreaterThan(plain.pointRadius);
    expect(emphasised.fill).toBe('origin');
    expect(emphasised.backgroundColor).toBe(withAlpha(emphasised.borderColor, 0.1));
    expect(plain.fill).toBe(false);
    expect(plain.backgroundColor).toBe('transparent');
  });

  it('colours the series from the palette the block names, in the theme the card is drawn in', () => {
    const light = cfg(yearChart).data.datasets.map((set: { borderColor: string }) => set.borderColor);
    const dark = cfg(yearChart, { theme: DARK_THEME }).data.datasets.map((set: { borderColor: string }) => set.borderColor);

    expect(light).toEqual(paletteColors('ramp', 3, LIGHT_THEME));
    expect(dark).toEqual(paletteColors('ramp', 3, DARK_THEME));
    expect(light).not.toEqual(dark);

    const segments = chart({ palette: 'segment', series: [series('brand', [1]), series('non_brand', [2])] });
    expect(cfg(segments).data.datasets.map((set: { borderColor: string }) => set.borderColor)).toEqual(paletteColors('segment', 2, LIGHT_THEME));
  });

  it('gives the same colour, width and dash to a series in the legend as on the plot', () => {
    const items = legendItems(yearChart, options());
    const sets = cfg(yearChart).data.datasets;

    expect(items.map((item) => item.label)).toEqual(['2024', '2025', '2026']);
    items.forEach((item, index) => {
      expect(item.color).toBe(sets[index].borderColor);
      expect(item.dashed).toBe(sets[index].borderDash.length > 0);
      expect(item.width).toBe(Math.max(2, sets[index].borderWidth));
    });
    expect(items[0].dashed).toBe(true);
    expect(items[2].dashed).toBe(false);
  });

  it('has a legend only for two series or more: a lone series is named by the chart heading', () => {
    expect(legendItems(chart({ series: [series('year_2026', [1])] }), options())).toEqual([]);
    expect(legendItems(yearChart, options())).toHaveLength(3);
  });
});

describe('chartConfig: motion, size and furniture', () => {
  it('turns the animation off when the person asked for less motion, and leaves the default otherwise', () => {
    expect(cfg(yearChart, { reducedMotion: true }).options.animation).toBe(false);
    expect(cfg(yearChart, { reducedMotion: false }).options.animation).toBeUndefined();
  });

  it('is responsive within its container, whose height the block gives', () => {
    const { options: chartOptions } = cfg(yearChart);

    expect(chartOptions.responsive).toBe(true);
    expect(chartOptions.maintainAspectRatio).toBe(false);
  });

  it('hides the library legend (the card draws its own, as text) and reads all series at one point', () => {
    const { options: chartOptions } = cfg(yearChart);

    expect(chartOptions.plugins.legend.display).toBe(false);
    expect(chartOptions.interaction).toEqual({ mode: 'index', intersect: false });
  });

  it('takes the colours of the furniture from the theme', () => {
    const light = cfg(yearChart, { theme: LIGHT_THEME }).options;
    const dark = cfg(yearChart, { theme: DARK_THEME }).options;

    expect(light.scales.y.grid.color).toBe(LIGHT_THEME.grid);
    expect(light.scales.y.ticks.color).toBe(LIGHT_THEME.muted);
    expect(light.plugins.tooltip.backgroundColor).toBe(LIGHT_THEME.tooltipBackground);
    expect(dark.scales.y.grid.color).toBe(DARK_THEME.grid);
    expect(dark.plugins.tooltip.bodyColor).toBe(DARK_THEME.tooltipText);
  });

  it('starts the value axis at zero: a line is read against it', () => {
    expect(cfg(yearChart).options.scales.y.beginAtZero).toBe(true);
  });

  it('writes the value axis for the language, abbreviated only when the scale needs it', () => {
    const small = cfg(chart({ series: [series('year_2026', [10, 2500.5])] })).options.scales.y.ticks.callback;
    const large = cfg(chart({ series: [series('year_2026', [10, 25000])] })).options.scales.y.ticks.callback;

    expect(small(1500)).toBe('1,500');
    expect(large(15000)).toBe('15K');
    expect(large(500)).toBe('500');
  });

  it('keeps whole steps on the value axis when every value is a whole number', () => {
    expect(cfg(chart({ series: [series('year_2026', [1, 2, 3])] })).options.scales.y.ticks.precision).toBe(0);
    expect(cfg(chart({ series: [series('year_2026', [1.5, 2])] })).options.scales.y.ticks.precision).toBeUndefined();
  });
});

describe('chartConfig: the tooltip', () => {
  const tooltip = () => cfg(yearChart).options.plugins.tooltip.callbacks;

  it('titles a point with its label in full', () => {
    expect(tooltip().title([{ dataIndex: 1 }])).toBe('February');
  });

  it('writes a value with its series name, formatted for the language', () => {
    expect(tooltip().label({ dataset: { label: '2026' }, parsed: { y: 12345.5 } })).toBe(' 2026: 12,345.5');
  });

  it('says "No data" for a point that has none, not a dash', () => {
    expect(tooltip().label({ dataset: { label: '2025' }, parsed: { y: null } })).toBe(' 2025: No data');
  });
});

describe('chartConfig: choosing a point', () => {
  it('has no click handler unless it was asked for one', () => {
    expect(cfg(yearChart).options.onClick).toBeUndefined();
  });

  it('reports the index of the point under the click', () => {
    const onPoint = vi.fn();
    const { onClick } = cfg(yearChart, { onPoint }).options;

    onClick({}, [{ index: 2 }, { index: 2 }]);

    expect(onPoint).toHaveBeenCalledOnce();
    expect(onPoint).toHaveBeenCalledWith(2);
  });

  it('ignores a click that is not on a point', () => {
    const onPoint = vi.fn();

    cfg(yearChart, { onPoint }).options.onClick({}, []);

    expect(onPoint).not.toHaveBeenCalled();
  });
});
