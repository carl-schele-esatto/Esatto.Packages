// The browser side of drawing a chart: loading the library when it is first needed, reading the page's colours, and noticing when they change.
// Everything that can be decided without a browser is in chart-config.ts and chart-colors.ts (tested); this is the glue that needs one.

import { chartThemeOf, type ChartTheme } from '../chart-colors';

type ChartModule = typeof import('./chart-bundle');

let loading: Promise<ChartModule> | null = null;

/**
 * The charting library, loaded once and on first use (a separate chunk of the package's build, so the dashboard itself does not carry it).
 * A failed load is not remembered: the next chart that is drawn tries again.
 */
export function loadChart(): Promise<ChartModule> {
  if (!loading) {
    loading = import('./chart-bundle');
    loading.catch(() => (loading = null));
  }
  return loading;
}

/** The person asked for less motion. Read at the moment a chart is drawn. */
export function prefersReducedMotion(): boolean {
  try {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  } catch {
    return false;
  }
}

const FORCED_COLORS = '(forced-colors: active)';

/** Windows' forced-colours (high-contrast) mode is on: the browser repaints the page with the system palette, but not the pixels of a canvas. */
export function forcedColorsActive(): boolean {
  try {
    return window.matchMedia(FORCED_COLORS).matches;
  } catch {
    return false;
  }
}

/**
 * The theme of the page a chart sits on, from what its colours RESOLVE to there. A canvas cannot use `var(--uui-color-text)`, and a custom property
 * read directly is the text it was written with (it may be a color-mix() the canvas cannot take), so each colour is put on a probe element and read
 * back as the browser computes it (rgb()). `root` is the shadow root of the card: the dashboard's own custom properties (--seo-notice) are defined on
 * its host.
 *
 * IN FORCED-COLOURS MODE the author's colours are not what the browser paints (the page is repainted with the system palette, a canvas is not), so
 * the probe resolves the system colours instead (CanvasText, Canvas, Highlight, GrayText) and chartThemeOf makes the theme from them. The probe
 * keeps the default `forced-color-adjust` there, so the browser really answers with the system colours. Outside the mode the probe is set to
 * `forced-color-adjust: none`, which then changes nothing, and the author's variables are read as before.
 */
export function readTheme(root: ParentNode): ChartTheme {
  const forced = forcedColorsActive();

  const probe = document.createElement('span');
  probe.style.cssText = `position:absolute;visibility:hidden;pointer-events:none;width:0;height:0${forced ? '' : ';forced-color-adjust:none'}`;
  root.append(probe);

  // The COMPUTED colour (rgb()), never the keyword or the var() that was asked for.
  const resolve = (color: string): string => {
    probe.style.color = '';
    probe.style.color = color;
    return getComputedStyle(probe).color;
  };

  try {
    return chartThemeOf(
      { text: resolve('var(--uui-color-text)'), surface: resolve('var(--uui-color-surface)'), primary: resolve('var(--seo-notice)') },
      forced ? { text: resolve('CanvasText'), surface: resolve('Canvas'), highlight: resolve('Highlight'), grayText: resolve('GrayText') } : null,
    );
  } finally {
    probe.remove();
  }
}

/**
 * Calls `onChange` (at most once a frame) when the page's colours may have changed: the backoffice theme is applied by changing the document's
 * attributes or its stylesheets, and the system's own preference by a media query. It does not say what changed; the caller reads the theme again
 * and redraws only if it is a different one. Returns what stops the watching.
 */
export function watchTheme(onChange: () => void): () => void {
  let frame = 0;
  const schedule = () => {
    if (frame) return;
    frame = requestAnimationFrame(() => {
      frame = 0;
      onChange();
    });
  };

  const observer = new MutationObserver(schedule);
  observer.observe(document.documentElement, { attributes: true });
  if (document.body) observer.observe(document.body, { attributes: true });
  observer.observe(document.head, { childList: true });

  const queries = ['(prefers-color-scheme: dark)', FORCED_COLORS, '(prefers-contrast: more)'].map((query) => window.matchMedia(query));
  queries.forEach((query) => query.addEventListener('change', schedule));

  return () => {
    observer.disconnect();
    queries.forEach((query) => query.removeEventListener('change', schedule));
    if (frame) cancelAnimationFrame(frame);
  };
}

/**
 * Resolves once the element has a size, or with false when `cancelled` says the chart is no longer wanted. A chart is measured when it is created,
 * and one created inside something that is not laid out yet (a section the backoffice shows a moment later) would stay collapsed; so it waits.
 * `stop` is called with a function that ends the wait early (the element left the page).
 */
export function whenSized(element: HTMLElement, cancelled: () => boolean, stop: (cancel: () => void) => void): Promise<boolean> {
  const sized = () => element.clientWidth > 0 && element.clientHeight > 0;
  if (sized()) return Promise.resolve(!cancelled());

  return new Promise((resolve) => {
    const observer = new ResizeObserver(() => {
      if (cancelled()) {
        observer.disconnect();
        resolve(false);
      } else if (sized()) {
        observer.disconnect();
        resolve(true);
      }
    });
    stop(() => {
      observer.disconnect();
      resolve(false);
    });
    observer.observe(element);
  });
}
