// One key figure as a tile: an icon, the figure, its label and description, and its trend. Used by the overview card for the figures that are not
// its main one; what it says (the figure by its format, "No data" for a null, the arrow and the word of the trend) is decided in kpi-view.ts.
//
// A figure that is null is "No data", written as words: never a dash that reads as a value and never a zero that reads as a measurement.

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import { kpiTileView } from '../kpi-view';
import type { Kpi } from '../types';
import { renderTrend, trendStyles } from './trend';

@customElement('esatto-seo-report-kpi-tile')
export class EsattoSeoReportKpiTileElement extends UmbLitElement {
  @property({ attribute: false }) kpi: Kpi | null = null;

  override render() {
    if (!this.kpi) return nothing;

    const view = kpiTileView(this, this.kpi);
    return html`
      <div class="tile tone-${view.tone}">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${view.icon}></uui-icon></span>
        <div class="tile-text">
          <span class="tile-value ${view.hasValue ? '' : 'none'}">${view.value}</span>
          <span class="tile-label">${view.label}</span>
          ${view.description ? html`<span class="tile-desc">${view.description}</span>` : nothing}
          ${renderTrend(view.trend)}
        </div>
      </div>
    `;
  }

  static override styles = [
    shared,
    trendStyles,
    css`
      /* The tile fills its grid cell: the host is the cell's content, and the percentage below needs a definite height to resolve against. */
      :host { display: block; height: 100%; }
      .tile {
        display: flex; gap: var(--uui-size-space-4); align-items: flex-start; padding: var(--uui-size-space-4) var(--uui-size-space-5);
        height: 100%; box-sizing: border-box;
        border-radius: var(--seo-radius); border: 1px solid color-mix(in srgb, var(--tone) 35%, var(--seo-line));
        background: linear-gradient(135deg, color-mix(in srgb, var(--tone) 14%, var(--uui-color-surface)), var(--uui-color-surface) 78%);
        box-shadow: var(--uui-shadow-depth-1);
      }
      .tile .icon-badge { width: 44px; height: 44px; font-size: 22px; box-shadow: 0 6px 14px -6px var(--tone); }
      .tile-text { display: grid; gap: 2px; flex: 1; min-width: 0; justify-items: start; }
      .tile-value { font-size: 30px; font-weight: 800; line-height: 1.1; letter-spacing: -0.02em; font-variant-numeric: tabular-nums; }
      .tile-value.none { font-size: 18px; font-weight: 600; color: var(--seo-muted); letter-spacing: 0; }
      .tile-label { font-size: 13px; font-weight: 600; color: var(--seo-muted); }
      .tile-desc { font-size: 0.75rem; line-height: 1.35; color: var(--seo-muted); }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-kpi-tile': EsattoSeoReportKpiTileElement;
  }
}
