// The colours of a chart, which Chart.js draws on a canvas and so cannot take from CSS. Pure functions (parsing a colour, a theme made from
// what the page's colours resolve to, the palettes), so they are tested without a browser; reading the colours from the page is the chart
// card's job (chart-runtime.ts).
//
// The report carries only the NAME of a palette (Report.cs: "No presentation: no hex colours"), and the client resolves it:
//   ramp         one hue from light to dark, the oldest series first (years by recency). The newest is always the one with the most weight
//                against the surface: the darkest on a light surface, the brightest on a dark one
//   segment      two identity colours for the site's own segments (brand and non-brand; the daily curve and its baseline). The accent is left
//                out on purpose, so it means one thing only
//   categorical  not drawn by any chart yet; it falls back to the ramp, as an unknown name does
// Colour is never the only carrier: `emphasis` makes a line thicker, `style: dashed` dashes it, and every series has a legend entry with its name.
//
// The segment pair was validated with OKLab distances (15 or more tells two series apart), for normal vision and for protanopia. The dark pair
// differs from the light one in lightness as well as hue, because two colours that differ only in hue fall together for red-green colour
// blindness, and a dark surface needs lighter colours.

export interface Rgba {
  r: number;
  g: number;
  b: number;
  /** 0 to 1. */
  a: number;
}

const HEX = /^#([0-9a-f]{3}|[0-9a-f]{6}|[0-9a-f]{8})$/i;
const FUNCTION = /^(rgba?|color)\(\s*(?:srgb\s+)?([^)]*)\)$/i;

/** One colour channel: a number, or a percentage of the range. */
function channel(part: string, range: number): number | null {
  const percent = part.endsWith('%');
  const value = Number(percent ? part.slice(0, -1) : part);
  if (!Number.isFinite(value)) return null;
  return percent ? (value / 100) * range : value;
}

/**
 * A CSS colour as its channels. It reads what the browser resolves a colour to (<c>getComputedStyle</c> gives rgb() or rgba(), with commas or
 * with spaces, and color(srgb ...) for a mixed colour) and hex. Null for anything else (a name, color-mix(), var()), so a caller falls back to a
 * theme it knows instead of drawing with a guess.
 */
export function parseColor(value: string | null | undefined): Rgba | null {
  const text = value?.trim();
  if (!text) return null;

  const hex = HEX.exec(text);
  if (hex) {
    let digits = hex[1];
    if (digits.length === 3) digits = [...digits].map((digit) => digit + digit).join('');
    const byte = (index: number) => Number.parseInt(digits.slice(index, index + 2), 16);
    return { r: byte(0), g: byte(2), b: byte(4), a: digits.length === 8 ? byte(6) / 255 : 1 };
  }

  const call = FUNCTION.exec(text);
  if (!call) return null;

  const isColorFunction = call[1].toLowerCase() === 'color';
  const [colour, alpha] = call[2].split('/').map((part) => part.trim());
  const parts = colour.split(/[\s,]+/).filter(Boolean);
  // rgba(r, g, b, a) has its opacity as a fourth part.
  const opacity = alpha ?? (parts.length === 4 ? parts.pop() : undefined);
  if (parts.length !== 3) return null;

  const range = isColorFunction ? 1 : 255;
  const [r, g, b] = parts.map((part) => channel(part, range));
  const a = opacity === undefined ? 1 : channel(opacity, 1);
  if (r === null || g === null || b === null || a === null) return null;

  const scale = isColorFunction ? 255 : 1;
  const byte = (v: number) => Math.round(Math.min(255, Math.max(0, v * scale)));
  return { r: byte(r), g: byte(g), b: byte(b), a: Math.min(1, Math.max(0, a)) };
}

/** The colour as CSS: rgb() when opaque, rgba() otherwise. */
export function toCss({ r, g, b, a }: Rgba): string {
  return a >= 1 ? `rgb(${r}, ${g}, ${b})` : `rgba(${r}, ${g}, ${b}, ${a})`;
}

const hex2 = (value: number) => Math.round(Math.min(255, Math.max(0, value))).toString(16).padStart(2, '0');
const toHex = ({ r, g, b }: Rgba) => `#${hex2(r)}${hex2(g)}${hex2(b)}`;

/** A colour with another opacity; the colour as it was given when it cannot be read. */
export function withAlpha(color: string, alpha: number): string {
  const parsed = parseColor(color);
  return parsed ? toCss({ ...parsed, a: alpha }) : color;
}

/** Relative luminance (WCAG): 0 is black, 1 is white. */
export function luminance({ r, g, b }: Rgba): number {
  const linear = (value: number) => {
    const c = value / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
}

// The colour of `front` over `back`, front at `share` (0 to 1).
function mix(front: Rgba, back: Rgba, share: number): Rgba {
  const part = (f: number, b: number) => Math.round(f * share + b * (1 - share));
  return { r: part(front.r, back.r), g: part(front.g, back.g), b: part(front.b, back.b), a: 1 };
}

// ---- The theme -------------------------------------------------------------------------------------------------------------------

export type Scheme = 'light' | 'dark';

/** What a chart draws its furniture with, from the colours of the page it sits on. */
export interface ChartTheme {
  scheme: Scheme;
  text: string;
  /** The tick labels. */
  muted: string;
  grid: string;
  surface: string;
  tooltipBackground: string;
  tooltipText: string;
  tooltipTitle: string;
  /** The hue of the ramp. */
  primary: string;
  /** A series beyond the colours of its palette (in forced-colours mode: GrayText, the colour the older series start from). */
  neutral: string;
  /** The system palette of Windows' forced-colours (high-contrast) mode is in force: the colours are Canvas, CanvasText, Highlight and GrayText. */
  forced: boolean;
  /** The accent: the second segment series in forced-colours mode (outside it, the accent is the ramp's `primary`). */
  highlight: string;
}

function themeOf(scheme: Scheme, text: Rgba, surface: Rgba, primary: string, neutral: string): ChartTheme {
  return {
    scheme,
    forced: false,
    highlight: primary,
    text: toCss(text),
    muted: toCss(mix(text, surface, 0.7)),
    grid: toCss({ ...text, a: 0.1 }),
    surface: toCss(surface),
    // The tooltip is the page turned over: the text colour as its ground, the surface colour as its text.
    tooltipBackground: toCss(text),
    tooltipText: toCss(surface),
    tooltipTitle: toCss(mix(surface, text, 0.75)),
    primary,
    neutral,
  };
}

const PRIMARY = '#2f6fde';

/** What a chart uses when the page's colours cannot be read, on a light surface. */
export const LIGHT_THEME: ChartTheme = themeOf('light', { r: 34, g: 34, b: 34, a: 1 }, { r: 255, g: 255, b: 255, a: 1 }, PRIMARY, '#64748b');

/** The same on a dark surface. */
export const DARK_THEME: ChartTheme = themeOf('dark', { r: 240, g: 240, b: 240, a: 1 }, { r: 20, g: 20, b: 20, a: 1 }, PRIMARY, '#94a3b8');

/** What the four system colours of forced-colours mode (Canvas, CanvasText, Highlight, GrayText) resolve to, as CSS colour text. */
export interface SystemColors {
  /** CanvasText. */
  text: string;
  /** Canvas. */
  surface: string;
  /** Highlight. */
  highlight: string;
  /** GrayText. */
  grayText: string;
}

// The theme of forced-colours mode: Canvas and CanvasText as the surface and the text (the scheme follows the luminance of Canvas), Highlight as
// the accent, GrayText for the grid and as the colour older series start from. The tick labels are in the text colour (readable first). A
// Highlight or GrayText that cannot be read falls back to the text colour, and to a mix of text and surface. Null when Canvas or CanvasText cannot.
function forcedTheme(system: SystemColors): ChartTheme | null {
  const text = parseColor(system.text);
  const surface = parseColor(system.surface);
  if (!text || !surface) return null;

  const highlight = toCss(parseColor(system.highlight) ?? text);
  const gray = toCss(parseColor(system.grayText) ?? mix(text, surface, 0.6));

  return {
    scheme: luminance(surface) < 0.4 ? 'dark' : 'light',
    forced: true,
    text: toCss(text),
    muted: toCss(text),
    grid: gray,
    surface: toCss(surface),
    tooltipBackground: toCss(text),
    tooltipText: toCss(surface),
    tooltipTitle: toCss(surface),
    primary: highlight,
    highlight,
    neutral: gray,
  };
}

/**
 * The theme for the colours the page's text and surface and accent resolve to (CSS colour text, as the browser computes it). A surface darker
 * than the middle is the dark scheme, which covers the dark and the high-contrast backoffice themes alike without knowing their names. When the
 * text or the surface cannot be read the whole fallback theme of the scheme is used, never half of one.
 */
export function chartThemeOf(samples: { text: string; surface: string; primary: string }, system?: SystemColors | null): ChartTheme {
  // Forced-colours mode: the page is painted with the system palette and the pixels of a canvas are not adjusted, so the chart takes the SYSTEM
  // colours, not the author's. When Canvas or CanvasText cannot be read the author's colours are the best there is.
  const forced = system ? forcedTheme(system) : null;
  if (forced) return forced;

  const text = parseColor(samples.text);
  const surface = parseColor(samples.surface);
  const dark = surface !== null && luminance(surface) < 0.4;
  const fallback = dark ? DARK_THEME : LIGHT_THEME;
  if (!text || !surface) return fallback;

  const primary = parseColor(samples.primary);
  return themeOf(dark ? 'dark' : 'light', text, surface, primary ? toCss(primary) : fallback.primary, fallback.neutral);
}

// ---- The palettes ----------------------------------------------------------------------------------------------------------------

function toHsl({ r, g, b }: Rgba): { h: number; s: number; l: number } {
  const [red, green, blue] = [r / 255, g / 255, b / 255];
  const max = Math.max(red, green, blue);
  const delta = max - Math.min(red, green, blue);
  const l = (max + Math.min(red, green, blue)) / 2;
  if (delta === 0) return { h: 0, s: 0, l };

  const s = delta / (1 - Math.abs(2 * l - 1));
  const hue = max === red ? 60 * (((green - blue) / delta) % 6) : max === green ? 60 * ((blue - red) / delta + 2) : 60 * ((red - green) / delta + 4);
  return { h: (hue + 360) % 360, s, l };
}

function fromHsl(h: number, s: number, l: number): Rgba {
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
  const sector = Math.floor(h / 60) % 6;
  const [r1, g1, b1] = [[c, x, 0], [x, c, 0], [0, c, x], [0, x, c], [x, 0, c], [c, 0, x]][sector];
  const m = l - c / 2;
  return { r: (r1 + m) * 255, g: (g1 + m) * 255, b: (b1 + m) * 255, a: 1 };
}

// Lightness of the ramp, from its first (oldest) to its last (newest) colour.
const RAMP = {
  light: { from: 0.7, to: 0.26 },
  dark: { from: 0.48, to: 0.82 },
};
const RAMP_SATURATION = 0.55;

/** The ordinal ramp in the hue of `primary`: the oldest series first, the newest with the most weight against the surface. */
export function rampColors(primary: string, count: number, scheme: Scheme): string[] {
  if (count <= 0) return [];

  const base = parseColor(primary) ?? parseColor(PRIMARY)!;
  const { h, s } = toHsl(base);
  const { from, to } = RAMP[scheme];

  if (count === 1) return [scheme === 'light' ? toHex(base) : toHex(fromHsl(h, Math.max(s, RAMP_SATURATION), 0.68))];

  const lastSaturation = Math.min(Math.max(s, RAMP_SATURATION), 1);
  return Array.from({ length: count }, (_, index) => {
    const t = index / (count - 1);
    return toHex(fromHsl(h, RAMP_SATURATION + t * (lastSaturation - RAMP_SATURATION), from + t * (to - from)));
  });
}

const SEGMENT: Record<Scheme, readonly string[]> = {
  light: ['#eb6834', '#3f6212'],
  dark: ['#ffb590', '#5a9a28'],
};

// The palettes in forced-colours mode, from the system colours only. `segment`: the text colour and the accent, then GrayText. Anything else: a
// ramp from GrayText (the oldest) to the text colour (the newest), which is the colour with the most contrast against Canvas by definition, so the
// newest series always has the most weight. What tells an older year apart from the newest is also its dash and its thinner line.
function forcedPalette(palette: string, count: number, theme: ChartTheme): string[] {
  if (palette === 'segment') return Array.from({ length: count }, (_, index) => [theme.text, theme.highlight][index] ?? theme.neutral);
  if (count === 1) return [theme.text];

  const text = parseColor(theme.text)!;
  const gray = parseColor(theme.neutral)!;
  return Array.from({ length: count }, (_, index) => toCss(mix(text, gray, index / (count - 1))));
}

/** The colours of a chart's series, in their order, for a palette name and the theme the chart is drawn in. */
export function paletteColors(palette: string, count: number, theme: ChartTheme): string[] {
  if (count <= 0) return [];
  if (theme.forced) return forcedPalette(palette, count, theme);
  if (palette === 'segment') return Array.from({ length: count }, (_, index) => SEGMENT[theme.scheme][index] ?? theme.neutral);
  return rampColors(theme.primary, count, theme.scheme);
}
