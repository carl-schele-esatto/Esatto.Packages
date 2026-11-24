import { describe, expect, it } from 'vitest';
import { LIGHT_THEME, chartThemeOf, luminance, paletteColors, parseColor, rampColors } from './chart-colors';

// Windows forced-colours (high-contrast) mode. The browser repaints the page with the system palette (Canvas, CanvasText, Highlight, GrayText)
// but does not touch the pixels of a canvas. A chart that read the AUTHOR's colours would draw dark axes on a black page, with the newest series
// as the darkest line. So the theme is taken from the system colours: `chartThemeOf(samples, system)` with the four resolved system colours,
// which wins over the author's samples.

// The palette of the "Night sky" high-contrast theme, and of "High Contrast White" (black on white): the two ends of what Windows ships.
const NIGHT = { text: 'rgb(255, 255, 255)', surface: 'rgb(0, 0, 0)', highlight: 'rgb(26, 235, 255)', grayText: 'rgb(63, 242, 63)' };
const WHITE = { text: 'rgb(0, 0, 0)', surface: 'rgb(255, 255, 255)', highlight: 'rgb(55, 0, 110)', grayText: 'rgb(96, 0, 0)' };
// What the author's own colours say on a light page (they are not what the browser paints in forced-colours mode).
const AUTHOR_LIGHT = { text: 'rgb(34, 34, 34)', surface: 'rgb(255, 255, 255)', primary: 'rgb(47, 111, 222)' };

const contrast = (a: string, b: string) => {
  const [x, y] = [luminance(parseColor(a)!), luminance(parseColor(b)!)].sort((m, n) => n - m);
  return (x + 0.05) / (y + 0.05);
};

describe('chartThemeOf in forced-colours mode', () => {
  it('on a black Canvas is the dark scheme in the system colours, whatever the author colours say', () => {
    const theme = chartThemeOf(AUTHOR_LIGHT, NIGHT);

    expect(theme.forced).toBe(true);
    expect(theme.scheme).toBe('dark');
    expect(theme.text).toBe('rgb(255, 255, 255)');
    expect(theme.surface).toBe('rgb(0, 0, 0)');
    expect(theme.highlight).toBe('rgb(26, 235, 255)');
  });

  it('draws the tick labels in the text colour (readable first) and the grid in GrayText', () => {
    const theme = chartThemeOf(AUTHOR_LIGHT, NIGHT);

    expect(theme.muted).toBe(theme.text);
    expect(theme.grid).toBe('rgb(63, 242, 63)');
    expect(theme.neutral).toBe('rgb(63, 242, 63)');
  });

  it('turns the tooltip over: Canvas on CanvasText', () => {
    const theme = chartThemeOf(AUTHOR_LIGHT, NIGHT);

    expect(theme.tooltipBackground).toBe('rgb(255, 255, 255)');
    expect(theme.tooltipText).toBe('rgb(0, 0, 0)');
  });

  it('on a white Canvas is the light scheme in the system colours', () => {
    const theme = chartThemeOf({ text: 'rgb(240, 240, 240)', surface: 'rgb(20, 20, 20)', primary: 'rgb(47, 111, 222)' }, WHITE);

    expect(theme.forced).toBe(true);
    expect(theme.scheme).toBe('light');
    expect(theme.text).toBe('rgb(0, 0, 0)');
    expect(theme.surface).toBe('rgb(255, 255, 255)');
    expect(theme.grid).toBe('rgb(96, 0, 0)');
  });

  it('decides the scheme from the Canvas colour and not from the author surface', () => {
    // A dark author surface on a light Canvas, and the other way round.
    expect(chartThemeOf({ ...AUTHOR_LIGHT, surface: 'rgb(0, 0, 0)' }, WHITE).scheme).toBe('light');
    expect(chartThemeOf({ ...AUTHOR_LIGHT, surface: 'rgb(255, 255, 255)' }, NIGHT).scheme).toBe('dark');
  });

  it('is a different theme from the same colours outside forced-colours mode, so a switch redraws the chart', () => {
    const forced = chartThemeOf(AUTHOR_LIGHT, { ...WHITE, text: 'rgb(34, 34, 34)', surface: 'rgb(255, 255, 255)' });
    const normal = chartThemeOf(AUTHOR_LIGHT);

    expect(forced.forced).toBe(true);
    expect(normal.forced).toBe(false);
    expect(JSON.stringify(forced)).not.toBe(JSON.stringify(normal));
  });

  it('leaves the other path as it was: no system colours (or null) is the author theme, not forced', () => {
    const normal = chartThemeOf(AUTHOR_LIGHT);

    expect(chartThemeOf(AUTHOR_LIGHT, null)).toEqual(normal);
    expect(chartThemeOf(AUTHOR_LIGHT, undefined)).toEqual(normal);
    expect(normal.forced).toBe(false);
    expect(normal.scheme).toBe('light');
    expect(normal.text).toBe('rgb(34, 34, 34)');
    expect(normal.grid).toBe('rgba(34, 34, 34, 0.1)');
  });

  it('falls back to the author colours when the system Canvas or CanvasText cannot be read, and is then not forced', () => {
    const normal = chartThemeOf(AUTHOR_LIGHT);

    expect(chartThemeOf(AUTHOR_LIGHT, { ...NIGHT, surface: '' })).toEqual(normal);
    expect(chartThemeOf(AUTHOR_LIGHT, { ...NIGHT, text: 'CanvasText' })).toEqual(normal);
    expect(chartThemeOf(AUTHOR_LIGHT, { text: '', surface: '', highlight: '', grayText: '' })).toEqual(normal);
  });

  it('and falls back to a whole theme when nothing at all can be read', () => {
    expect(chartThemeOf({ text: '', surface: '', primary: '' }, { text: '', surface: '', highlight: '', grayText: '' })).toEqual(LIGHT_THEME);
  });

  it('uses the text colour for the accent, and a mix for GrayText, when only those two cannot be read', () => {
    const theme = chartThemeOf(AUTHOR_LIGHT, { ...NIGHT, highlight: '', grayText: 'nonsense' });

    expect(theme.forced).toBe(true);
    expect(theme.highlight).toBe(theme.text);
    expect(parseColor(theme.grid)).not.toBeNull();
    expect(parseColor(theme.neutral)).not.toBeNull();
  });
});

describe('paletteColors in forced-colours mode', () => {
  const night = chartThemeOf(AUTHOR_LIGHT, NIGHT);
  const white = chartThemeOf(AUTHOR_LIGHT, WHITE);

  it('has the newest series in the text colour, the one with the most contrast against Canvas, and older ones stepping toward GrayText', () => {
    for (const theme of [night, white]) {
      const colors = paletteColors('ramp', 4, theme);

      expect(colors).toHaveLength(4);
      expect(colors[3]).toBe(theme.text);
      expect(colors[0]).toBe(theme.neutral);
      expect(new Set(colors).size).toBe(4);
      for (let i = 1; i < colors.length; i++) expect(contrast(colors[i], theme.surface)).toBeGreaterThanOrEqual(contrast(colors[i - 1], theme.surface));
    }
  });

  it('keeps every colour readable against Canvas', () => {
    for (const theme of [night, white]) {
      for (const color of paletteColors('ramp', 6, theme)) expect(contrast(color, theme.surface), color).toBeGreaterThanOrEqual(3);
    }
  });

  it('is the text colour for one series', () => {
    expect(paletteColors('ramp', 1, night)).toEqual([night.text]);
  });

  it('uses the text colour and the accent for the two segment series, and GrayText beyond them', () => {
    expect(paletteColors('segment', 3, night)).toEqual([night.text, night.highlight, night.neutral]);
    expect(paletteColors('segment', 0, night)).toEqual([]);
  });

  it('treats "categorical" and an unknown palette like the ramp, as outside forced-colours mode', () => {
    expect(paletteColors('categorical', 3, white)).toEqual(paletteColors('ramp', 3, white));
  });

  it('leaves the palettes outside forced-colours mode as they were', () => {
    expect(paletteColors('ramp', 3, chartThemeOf(AUTHOR_LIGHT))).toEqual(rampColors('rgb(47, 111, 222)', 3, 'light'));
  });
});
