import { describe, expect, it } from 'vitest';
import { chartThemeOf, withAlpha, LIGHT_THEME } from './chart-colors';
import { chartConfig, legendItems, type ChartConfigOptions } from './chart-config';
import { chart } from './testing/fixtures';
import { enHost } from './testing/host';
import type { Series } from './types';

// The chart in Windows forced-colours (high-contrast) mode: system colours only, the newest series in the text colour (the most contrast against
// Canvas), and a marker shape per series so that colour is still not the only carrier when the palette has two colours and a ramp of greys.

const AUTHOR = { text: 'rgb(34, 34, 34)', surface: 'rgb(255, 255, 255)', primary: 'rgb(47, 111, 222)' };
const NIGHT = chartThemeOf(AUTHOR, { text: 'rgb(255, 255, 255)', surface: 'rgb(0, 0, 0)', highlight: 'rgb(26, 235, 255)', grayText: 'rgb(63, 242, 63)' });

const options = (overrides: Partial<ChartConfigOptions> = {}): ChartConfigOptions => ({ host: enHost(), theme: NIGHT, reducedMotion: false, ...overrides });
const series = (key: string, values: (number | null)[], overrides: Partial<Series> = {}): Series => ({ key, values, emphasis: false, style: 'solid', ...overrides });

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const cfg = (block: Parameters<typeof chartConfig>[0], overrides?: Partial<ChartConfigOptions>) => chartConfig(block, options(overrides)) as any;

const years = chart({
  labels: ['01', '02', '03'],
  series: [
    series('year_2024', [1, 2, 3], { style: 'dashed' }),
    series('year_2025', [2, 3, 4]),
    series('year_2026', [3, 4, 5], { emphasis: true }),
  ],
});

describe('chartConfig in forced-colours mode', () => {
  it('draws the emphasised series in the text colour and the older ones stepping toward GrayText', () => {
    const [oldest, middle, newest] = cfg(years).data.datasets;

    expect(newest.borderColor).toBe(NIGHT.text);
    expect(oldest.borderColor).toBe(NIGHT.neutral);
    expect(middle.borderColor).not.toBe(newest.borderColor);
    expect(middle.borderColor).not.toBe(oldest.borderColor);
  });

  it('draws the emphasised series in the text colour even when it is not the last one', () => {
    const block = chart({ series: [series('year_2024', [1], { emphasis: true }), series('year_2025', [2]), series('year_2026', [3])] });

    const [first, middle] = cfg(block).data.datasets;

    expect(first.borderColor).toBe(NIGHT.text);
    expect(middle.borderColor).not.toBe(NIGHT.text);
    // It is still told from the other series that are in the text colour by its width, its fill and its marker.
    expect(first.borderWidth).toBeGreaterThan(middle.borderWidth);
  });

  it('keeps the dash and the width: an older year is still dashed and thinner, whatever the colours', () => {
    const [oldest, middle, newest] = cfg(years).data.datasets;

    expect(oldest.borderDash).toEqual([4, 3]);
    expect(middle.borderDash).toEqual([]);
    expect(newest.borderWidth).toBeGreaterThan(middle.borderWidth);
    expect(newest.backgroundColor).toBe(withAlpha(NIGHT.text, 0.1));
  });

  it('gives each series its own marker shape, because two colours and a ramp of greys are a weak carrier on their own', () => {
    const shapes = cfg(years).data.datasets.map((set: { pointStyle: string }) => set.pointStyle);

    expect(new Set(shapes).size).toBe(3);
    for (const shape of shapes) expect(['circle', 'rect', 'triangle', 'rectRot', 'cross', 'star']).toContain(shape);
  });

  it('tells the two brand series apart by colour AND marker (neither is emphasised, both are solid)', () => {
    const brand = chart({ palette: 'segment', series: [series('brand', [1, 2]), series('non_brand', [3, 4])] });

    const [a, b] = cfg(brand).data.datasets;

    expect(a.borderColor).toBe(NIGHT.text);
    expect(b.borderColor).toBe(NIGHT.highlight);
    expect(a.pointStyle).not.toBe(b.pointStyle);
  });

  it('takes the axes from the system colours: ticks in the text colour, the grid in GrayText', () => {
    const { scales, plugins } = cfg(years).options;

    expect(scales.y.ticks.color).toBe(NIGHT.text);
    expect(scales.x.ticks.color).toBe(NIGHT.text);
    expect(scales.y.grid.color).toBe(NIGHT.grid);
    expect(plugins.tooltip.backgroundColor).toBe(NIGHT.tooltipBackground);
    expect(plugins.tooltip.bodyColor).toBe(NIGHT.tooltipText);
  });

  it('dots the grid, because GrayText is also the colour the oldest series starts from and a solid grid would read as a line of data', () => {
    const { scales } = cfg(years).options;

    // In Chart.js 4 the dash of the grid lines is the `border.dash` of the scale.
    expect(scales.y.border.dash).toEqual([1, 4]);
    expect(scales.x.border.dash).toEqual([1, 4]);
  });

  it('gives the legend the same colours as the plot', () => {
    const items = legendItems(years, options());
    const sets = cfg(years).data.datasets;

    items.forEach((item, index) => expect(item.color).toBe(sets[index].borderColor));
    expect(items[2].color).toBe(NIGHT.text);
  });
});

describe('chartConfig outside forced-colours mode is as it was', () => {
  it('has no marker shape of its own: every point is the default circle', () => {
    const sets = cfg(years, { theme: LIGHT_THEME }).data.datasets;

    for (const set of sets) expect('pointStyle' in set).toBe(false);
  });

  it('has a solid grid', () => {
    const { scales } = cfg(years, { theme: LIGHT_THEME }).options;

    expect('border' in scales.y).toBe(false);
    expect('border' in scales.x).toBe(false);
  });

  it('colours the emphasised series from the palette, not the text colour', () => {
    const [, , newest] = cfg(years, { theme: LIGHT_THEME }).data.datasets;

    expect(newest.borderColor).not.toBe(LIGHT_THEME.text);
  });
});
