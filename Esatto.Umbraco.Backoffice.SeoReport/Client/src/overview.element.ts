// The overview card: the month's main figure with its trend, the engaged-visits ring, the month controls, and below them the other key
// figures as tiles and the notes about the data. It only reads a report (the shell hands it down); choosing a month and "Refresh" are
// the period bar's events, which bubble to the shell.
//
// The server sends figures as numbers with a `format`; kpi-view.ts formats them for the language. A figure that is null is "no data", written as
// words: never a dash that reads as a value, and never a zero that reads as a measurement. A trend is shown as an arrow, the size of the
// change and, for a screen reader, the words up or down: direction is never carried by colour alone. A trend without a figure shows no pill.
// The other key figures are tiles (esatto-seo-report-kpi-tile), the same tile the other tabs use.

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { trendStyles, renderTrend } from './components/trend';
import { shared } from './dashboard.styles';
import { formatKpiValue } from './format';
import { toneOf, trendText } from './kpi-view';
import { culture, idText, refText, text } from './localize';
import { heroKpis, tileKpis } from './report-helpers';
import type { Kpi, Report } from './types';
import './components/kpi-tile.element';
import './period-bar.element';

const RING = { size: 168, stroke: 14 };

@customElement('esatto-seo-report-overview')
export class EsattoSeoReportOverviewElement extends UmbLitElement {
  @property({ attribute: false }) report: Report | null = null;

  /** The selected month, "YYYY-MM". */
  @property() period = '';

  /** The archived months, newest first. */
  @property({ attribute: false }) periods: string[] = [];

  @property({ attribute: false }) fetchedUtc: string | null = null;

  @property({ type: Boolean }) refreshing = false;

  /** Another month is being read: the figures are dimmed and the controls wait. */
  @property({ type: Boolean }) busy = false;

  override render() {
    if (!this.report) return nothing;

    const kpis = this.report.traffic.kpis;
    const tiles = tileKpis(kpis);

    return html`
      ${this.#hero(kpis)}
      ${tiles.length > 0
        ? html`<section aria-labelledby="tiles-title" class=${this.busy ? 'busy' : ''}>
            <h3 id="tiles-title" class="section-label">${text(this, 'uiKeyFiguresLabel')}</h3>
            <ul class="tiles">${tiles.map((kpi) => html`<li><esatto-seo-report-kpi-tile .kpi=${kpi}></esatto-seo-report-kpi-tile></li>`)}</ul>
          </section>`
        : nothing}
      ${this.#notes()}
    `;
  }

  // ---- The card -------------------------------------------------------------------------------------------------------------

  #hero(kpis: readonly Kpi[]) {
    const c = culture(this);
    const { main, ring } = heroKpis(kpis);
    const trend = trendText(this, main?.trend);
    const value = main ? formatKpiValue(main.value, main.format, c) : '';

    return html`
      <section class="card hero tone-${toneOf(trend?.pill?.direction)} ${this.busy ? 'busy' : ''}" aria-label=${text(this, 'uiOverviewLabel')}>
        ${ring
          ? this.#ring(ring)
          : html`<span class="hero-badge icon-badge solid round tone-notice" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>`}
        <div class="hero-body">
          ${main ? html`<p class="eyebrow">${idText(this, 'kpi', main.key, 'Label')}</p>` : nothing}
          <h3 id="hero-title" class="hero-title">${value || text(this, 'uiNoValueText')}</h3>
          ${renderTrend(trend)}
          ${main ? html`<p class="muted small">${idText(this, 'kpi', main.key, 'Description')}</p>` : nothing}
          <esatto-seo-report-period-bar
            .periods=${this.periods}
            .period=${this.period}
            .fetchedUtc=${this.fetchedUtc}
            ?refreshing=${this.refreshing}
            ?busy=${this.busy}
          ></esatto-seo-report-period-bar>
        </div>
      </section>
    `;
  }

  // The ring is a share of a whole (0 to 100), drawn as an arc. It is a picture of the figure beside it, so it has one text alternative
  // that says the value in words, and its parts are hidden from the accessibility tree.
  #ring(kpi: Kpi) {
    const c = culture(this);
    const radius = (RING.size - RING.stroke) / 2;
    const circumference = 2 * Math.PI * radius;
    const share = Math.max(0, Math.min(100, kpi.value ?? 0));
    const centre = RING.size / 2;
    const label = idText(this, 'kpi', kpi.key, 'Label');
    const written = formatKpiValue(kpi.value, kpi.format, c);

    return html`
      <figure class="ring" role="img" aria-label=${text(this, 'uiRingAltText', { label, value: written })}>
        <svg width=${RING.size} height=${RING.size} viewBox="0 0 ${RING.size} ${RING.size}" aria-hidden="true">
          <defs>
            <linearGradient id="ring-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" class="stop-a"></stop>
              <stop offset="100%" class="stop-b"></stop>
            </linearGradient>
          </defs>
          <circle class="track" cx=${centre} cy=${centre} r=${radius} stroke-width=${RING.stroke} fill="none"></circle>
          <circle
            class="fill ${share ? '' : 'empty'}"
            cx=${centre}
            cy=${centre}
            r=${radius}
            stroke-width=${RING.stroke}
            fill="none"
            stroke="url(#ring-gradient)"
            stroke-dasharray="${(share / 100) * circumference} ${circumference}"
            stroke-linecap="round"
            transform="rotate(-90 ${centre} ${centre})"
          ></circle>
        </svg>
        <div class="ring-text" aria-hidden="true">
          <span class="ring-value">${written}</span>
          <span class="muted small">${text(this, 'uiRingOfVisitsText')}</span>
        </div>
        <figcaption aria-hidden="true"><span class="tag tone-notice">${label}</span></figcaption>
      </figure>
    `;
  }

  // ---- Notes about the data (how far each source has got) ---------------------------------------------------------------------

  #notes() {
    const notes = (this.report?.sources ?? [])
      .map((source) => ({
        source: idText(this, 'source', source.id, 'Label'),
        note: refText(this, 'sourceNote', source.note),
      }))
      .filter((item) => item.note);

    if (notes.length === 0) return nothing;

    return html`
      <ul class="notes muted small" aria-label=${text(this, 'uiDataNotesLabel')}>
        ${notes.map((item) => html`<li>${text(this, 'uiSourceNoteText', item)}</li>`)}
      </ul>
    `;
  }

  static override styles = [
    shared,
    trendStyles,
    css`
      :host { display: grid; gap: var(--seo-gap); }
      .busy { opacity: 0.5; transition: opacity var(--seo-ease); }

      /* The card is tinted by the main figure's trend, as SEO Health tints its score card. */
      .hero {
        display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; padding: var(--uui-size-space-6);
        background: radial-gradient(120% 160% at 0% 0%, color-mix(in srgb, var(--tone) 16%, var(--uui-color-surface)) 0%, var(--uui-color-surface) 62%);
        border-color: color-mix(in srgb, var(--tone) 30%, var(--seo-line));
      }
      .hero-body { flex: 1; min-width: 18rem; display: grid; gap: var(--uui-size-space-3); justify-items: start; }
      .hero-badge { width: 96px; height: 96px; font-size: 44px; }
      .eyebrow { margin: 0; font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--seo-muted); }
      .hero-title { margin: -6px 0 0; font-size: 44px; font-weight: 800; letter-spacing: -0.02em; line-height: 1.15; font-variant-numeric: tabular-nums; }

      /* The ring is a share of a whole, in blue: it is not a grade. */
      .ring {
        --tone: var(--seo-notice); --ring-a: #60a5fa; --ring-b: #6d4ce0;
        position: relative; margin: 0; width: 168px; display: grid; justify-items: center; gap: var(--uui-size-space-3);
      }
      .ring svg { display: block; filter: drop-shadow(0 8px 18px color-mix(in srgb, var(--tone) 30%, transparent)); }
      .ring .track { stroke: color-mix(in srgb, var(--tone) 18%, var(--uui-color-surface)); }
      .ring .fill.empty { display: none; }
      .stop-a { stop-color: var(--ring-a); }
      .stop-b { stop-color: var(--ring-b); }
      .ring-text { position: absolute; top: 0; height: 168px; display: grid; place-content: center; text-align: center; gap: 2px; }
      .ring-value { font-size: 36px; font-weight: 800; line-height: 1; letter-spacing: -0.03em; font-variant-numeric: tabular-nums; }

      .section-label { margin: 0 0 var(--uui-size-space-3); font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--seo-muted); }
      .tiles { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--seo-gap); grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); }
      .notes { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-1); }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-overview': EsattoSeoReportOverviewElement;
  }
}
