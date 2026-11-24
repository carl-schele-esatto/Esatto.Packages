import { describe, expect, it } from 'vitest';
import { DARK_THEME, LIGHT_THEME, chartThemeOf, luminance, paletteColors, parseColor, rampColors, withAlpha } from './chart-colors';

// OKLab distance (x100), the measure the palettes were validated with (a distance of 15 or more tells two series apart), and a
// protanopia simulation (Machado 2009) for the red-green case.
const linear = (c: number) => ((c /= 255) <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
const rgbOf = (css: string) => {
  const c = parseColor(css)!;
  return [c.r, c.g, c.b] as const;
};
function oklab([r, g, b]: readonly number[]) {
  const [lr, lg, lb] = [linear(r), linear(g), linear(b)];
  const l = Math.cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
  const m = Math.cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
  const s = Math.cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
  return [0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s, 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s, 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s];
}
const distance = (a: readonly number[], b: readonly number[]) => {
  const [x, y] = [oklab(a), oklab(b)];
  return 100 * Math.hypot(x[0] - y[0], x[1] - y[1], x[2] - y[2]);
};
function protanopia([r, g, b]: readonly number[]): number[] {
  const [lr, lg, lb] = [linear(r), linear(g), linear(b)];
  const back = (c: number) => {
    const v = Math.min(1, Math.max(0, c));
    return Math.round(255 * (v <= 0.0031308 ? 12.92 * v : 1.055 * v ** (1 / 2.4) - 0.055));
  };
  return [
    back(0.152286 * lr + 1.052583 * lg - 0.204868 * lb),
    back(0.114503 * lr + 0.786281 * lg + 0.099216 * lb),
    back(-0.003882 * lr - 0.048116 * lg + 1.051998 * lb),
  ];
}

describe('parseColor', () => {
  it('reads hex in its three lengths', () => {
    expect(parseColor('#1871ca')).toEqual({ r: 24, g: 113, b: 202, a: 1 });
    expect(parseColor('#fff')).toEqual({ r: 255, g: 255, b: 255, a: 1 });
    expect(parseColor('#00000080')).toEqual({ r: 0, g: 0, b: 0, a: 128 / 255 });
  });

  it('reads what getComputedStyle gives: rgb() and rgba() with commas or spaces, and color(srgb ...)', () => {
    expect(parseColor('rgb(15, 23, 42)')).toEqual({ r: 15, g: 23, b: 42, a: 1 });
    expect(parseColor('rgba(15, 23, 42, 0.5)')).toEqual({ r: 15, g: 23, b: 42, a: 0.5 });
    expect(parseColor('rgb(15 23 42 / 25%)')).toEqual({ r: 15, g: 23, b: 42, a: 0.25 });
    expect(parseColor('color(srgb 1 0.5 0)')).toEqual({ r: 255, g: 128, b: 0, a: 1 });
    expect(parseColor('color(srgb 0 0 1 / 0.5)')).toEqual({ r: 0, g: 0, b: 255, a: 0.5 });
  });

  it('is null for what it cannot read, so a caller falls back instead of drawing with a guess', () => {
    for (const value of ['', '  ', 'red', 'color-mix(in srgb, red 50%, blue)', 'var(--x)', '#12', 'rgb(1, 2)', null, undefined]) {
      expect(parseColor(value as string), String(value)).toBeNull();
    }
  });
});

describe('withAlpha', () => {
  it('writes a colour with an opacity, and returns the colour unchanged when it cannot read it', () => {
    expect(withAlpha('#1871ca', 0.2)).toBe('rgba(24, 113, 202, 0.2)');
    expect(withAlpha('rgb(15, 23, 42)', 0.08)).toBe('rgba(15, 23, 42, 0.08)');
    expect(withAlpha('currentColor', 0.2)).toBe('currentColor');
  });
});

describe('luminance', () => {
  it('is 0 for black and 1 for white, and tells a dark surface from a light one', () => {
    expect(luminance(parseColor('#000')!)).toBe(0);
    expect(luminance(parseColor('#fff')!)).toBe(1);
    expect(luminance(parseColor('#1b264f')!)).toBeLessThan(0.1);
    expect(luminance(parseColor('#f3f3f5')!)).toBeGreaterThan(0.8);
  });
});

describe('chartThemeOf', () => {
  it('is light on a light surface: dark text, a faint grid, and a tooltip that is the text colour turned over', () => {
    const theme = chartThemeOf({ text: 'rgb(34, 34, 34)', surface: 'rgb(255, 255, 255)', primary: 'rgb(47, 111, 222)' });

    expect(theme.scheme).toBe('light');
    expect(theme.text).toBe('rgb(34, 34, 34)');
    expect(theme.grid).toBe('rgba(34, 34, 34, 0.1)');
    expect(theme.tooltipBackground).toBe('rgb(34, 34, 34)');
    expect(theme.tooltipText).toBe('rgb(255, 255, 255)');
    expect(theme.primary).toBe('rgb(47, 111, 222)');
  });

  it('is dark on a dark surface (dark and high-contrast backoffice themes)', () => {
    const dark = chartThemeOf({ text: 'rgb(240, 240, 240)', surface: 'rgb(27, 38, 79)', primary: 'rgb(47, 111, 222)' });
    const highContrast = chartThemeOf({ text: 'rgb(255, 255, 255)', surface: 'rgb(0, 0, 0)', primary: 'rgb(47, 111, 222)' });

    expect(dark.scheme).toBe('dark');
    expect(highContrast.scheme).toBe('dark');
    expect(highContrast.tooltipBackground).toBe('rgb(255, 255, 255)');
    expect(highContrast.tooltipText).toBe('rgb(0, 0, 0)');
  });

  it('falls back to a whole theme when a colour cannot be read, never a half one', () => {
    expect(chartThemeOf({ text: '', surface: 'rgb(255, 255, 255)', primary: '' })).toEqual(LIGHT_THEME);
    expect(chartThemeOf({ text: 'x', surface: 'y', primary: 'w' })).toEqual(LIGHT_THEME);
  });

  it('keeps a readable primary when only that one is missing', () => {
    const theme = chartThemeOf({ text: 'rgb(34, 34, 34)', surface: 'rgb(255, 255, 255)', primary: 'oops' });

    expect(theme.primary).toBe(LIGHT_THEME.primary);
    expect(theme.scheme).toBe('light');
  });

  it('has the same keys in both fallbacks', () => {
    expect(Object.keys(DARK_THEME).sort()).toEqual(Object.keys(LIGHT_THEME).sort());
  });
});

describe('rampColors', () => {
  const lightnessOf = (css: string) => oklab(rgbOf(css))[0];

  it('is one colour for one series, none for none', () => {
    expect(rampColors('#1871ca', 0, 'light')).toEqual([]);
    expect(rampColors('#1871ca', 1, 'light')).toEqual(['#1871ca']);
    expect(rampColors('#1871ca', 1, 'dark')).toHaveLength(1);
  });

  it('on a light surface runs from light (the oldest) to dark (the newest), so the newest has the most weight', () => {
    const colors = rampColors('#1871ca', 4, 'light');

    expect(colors).toHaveLength(4);
    expect(new Set(colors).size).toBe(4);
    for (let i = 1; i < colors.length; i++) expect(lightnessOf(colors[i])).toBeLessThan(lightnessOf(colors[i - 1]));
  });

  it('on a dark surface runs the other way, from dim to bright, so the newest still has the most weight', () => {
    const colors = rampColors('#1871ca', 4, 'dark');

    expect(new Set(colors).size).toBe(4);
    for (let i = 1; i < colors.length; i++) expect(lightnessOf(colors[i])).toBeGreaterThan(lightnessOf(colors[i - 1]));
  });

  it('keeps every shade readable against its surface', () => {
    for (const color of rampColors('#2f6fde', 6, 'light')) expect(distance(rgbOf(color), [255, 255, 255]), color).toBeGreaterThan(12);
    for (const color of rampColors('#2f6fde', 6, 'dark')) expect(distance(rgbOf(color), [20, 20, 20]), color).toBeGreaterThan(30);
  });

  it('keeps the hue of the colour it is given', () => {
    const [first] = rampColors('#2f6fde', 3, 'light');
    const { r, g, b } = parseColor(first)!;

    expect(b).toBeGreaterThan(r);
    expect(b).toBeGreaterThan(g);
  });
});

describe('paletteColors', () => {
  it('uses the ramp for "ramp" and, as the other palettes are not drawn by anything yet, for "categorical" and an unknown name', () => {
    expect(paletteColors('ramp', 3, LIGHT_THEME)).toEqual(rampColors(LIGHT_THEME.primary, 3, 'light'));
    expect(paletteColors('categorical', 3, LIGHT_THEME)).toEqual(rampColors(LIGHT_THEME.primary, 3, 'light'));
    expect(paletteColors('nonsense', 2, DARK_THEME)).toEqual(rampColors(DARK_THEME.primary, 2, 'dark'));
  });

  it('uses two identity colours for "segment", and a neutral for a series beyond them', () => {
    const colors = paletteColors('segment', 3, LIGHT_THEME);

    expect(colors).toHaveLength(3);
    expect(colors[0]).not.toBe(colors[1]);
    expect(colors[2]).toBe(LIGHT_THEME.neutral);
    expect(paletteColors('segment', 0, LIGHT_THEME)).toEqual([]);
  });

  it('keeps the two segment colours apart, for people who see colour normally and for protanopia, and clear of the surface, in both themes', () => {
    for (const [theme, surface] of [[LIGHT_THEME, [255, 255, 255]], [DARK_THEME, [20, 20, 20]]] as const) {
      const [a, b] = paletteColors('segment', 2, theme).map(rgbOf);

      expect(distance(a, b)).toBeGreaterThanOrEqual(15);
      expect(distance(protanopia(a), protanopia(b))).toBeGreaterThanOrEqual(12);
      expect(distance(a, surface)).toBeGreaterThanOrEqual(25);
      expect(distance(b, surface)).toBeGreaterThanOrEqual(25);
    }
  });
});
