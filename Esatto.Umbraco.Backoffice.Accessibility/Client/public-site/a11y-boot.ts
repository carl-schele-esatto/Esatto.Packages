// Runs synchronously in <head> (emitted by <a11y-head />) so preferences apply before first paint.
// Configuration comes from this script tag's data attributes:
//   data-follow-motion / data-follow-contrast  "true" | "false"
//   data-options                                panel options on offer, e.g. "text,spacing,motion"

import { applyAttributes, attributesFor, parseOptions, readPrefs, safeStorage, type Prefs } from './prefs';

declare global {
  interface Window {
    esattoA11y?: { apply(prefs?: Prefs): void; offered: string[] };
    esattoA11yScan?: boolean;
  }
}

(() => {
  const script = document.currentScript as HTMLScriptElement | null;
  const data = script?.dataset ?? {};
  const follow = { motion: data.followMotion === 'true', contrast: data.followContrast === 'true' };
  const offered = parseOptions(data.options);
  const motion = matchMedia('(prefers-reduced-motion: reduce)');
  const contrast = matchMedia('(prefers-contrast: more)');

  // Pages loaded by the accessibility scanner carry this flag, so sites can keep them out of analytics.
  if (new URLSearchParams(location.search).get('esatto-a11y-scan') === '1') window.esattoA11yScan = true;

  const apply = (prefs: Prefs = readPrefs(safeStorage())) => {
    applyAttributes(document.documentElement, attributesFor(prefs, { reducedMotion: motion.matches, moreContrast: contrast.matches }, follow, offered));
    if (document.documentElement.hasAttribute('data-a11y-motion')) pauseAutoplay();
  };

  window.esattoA11y = { apply, offered };
  apply();
  motion.addEventListener('change', () => apply());
  contrast.addEventListener('change', () => apply());
  document.addEventListener('DOMContentLoaded', () => {
    if (document.documentElement.hasAttribute('data-a11y-motion')) pauseAutoplay();
  });
})();

function pauseAutoplay() {
  for (const video of Array.from(document.querySelectorAll<HTMLVideoElement>('video[autoplay]'))) {
    video.pause();
    video.removeAttribute('autoplay');
  }
}
