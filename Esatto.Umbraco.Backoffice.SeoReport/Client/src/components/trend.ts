// A key figure's trend as the pill the overview, the key-figure tile and the search table all draw: an arrow, a word (shown to a screen reader
// and not to the eye) and the size of the change, and beside it the text it is measured against. What it says is decided in kpi-view.ts; this
// only draws it.
//
// The direction is never carried by colour alone: the arrow and the word say it too, and the tint (a rise green, a fall red, anything else
// plain) sits on the theme's own surface with the theme's own text colour, so the pill reads in the light, dark and high-contrast themes.

import { css, html, nothing } from '@umbraco-cms/backoffice/external/lit';
import type { TrendText } from '../kpi-view';

/** The pill and the text beside it; nothing for no trend. */
export function renderTrend(trend: TrendText | null | undefined) {
  if (!trend) return nothing;

  const { pill, note } = trend;
  return html`
    <span class="trend">
      ${pill
        ? html`<span class="pill ${pill.direction}"
            ><span aria-hidden="true">${pill.arrow}</span><span class="visually-hidden">${pill.words} </span>${pill.figure}</span
          >`
        : nothing}
      ${note ? html`<span class="muted">${note}</span>` : nothing}
    </span>
  `;
}

export const trendStyles = css`
  .trend { font-size: 0.8rem; display: inline-flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: var(--uui-size-space-2); }
  .pill {
    display: inline-flex; gap: 4px; align-items: center; padding: 1px 8px; border-radius: 999px; font-weight: 700; white-space: nowrap;
    font-variant-numeric: tabular-nums; color: var(--uui-color-text);
    background: var(--seo-wash); border: 1px solid var(--seo-line);
  }
  .pill.up { background: color-mix(in srgb, var(--seo-good) 14%, var(--uui-color-surface)); border-color: color-mix(in srgb, var(--seo-good) 45%, var(--seo-line)); }
  .pill.down { background: color-mix(in srgb, var(--seo-critical) 14%, var(--uui-color-surface)); border-color: color-mix(in srgb, var(--seo-critical) 45%, var(--seo-line)); }
`;
