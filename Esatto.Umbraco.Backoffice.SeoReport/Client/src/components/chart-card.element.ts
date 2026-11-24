// A chart of the report as a card: its heading, what it covers, a legend of names, the plot, and the notes about it. It is glue; every decision is in
// a pure module: the words and the data table (chart-view.ts), the Chart.js configuration (chart-config.ts), the colours (chart-colors.ts).
//
// THE PLOT HAS A DATA TABLE. A canvas is not text, and `role="img"` with a label gives it only a name; so the visually hidden table that follows
// it spells out every value (WCAG 1.1.1). A gap reads "No data". It is hidden the way a screen-reader-only text is (clipped to a pixel), never
// with display:none, which would hide it from the very people it is for.
//
// A BLOCK WITH NOTHING TO DRAW IS NOT DRAWN. No series, or only gaps, shows "No data yet." under the heading instead of an empty plot.
//
// COLOUR IS NEVER THE ONLY CARRIER. Lines differ by width and dash as well as colour, and the legend is text: a button per series with its name,
// underlined with the line's own colour, width and dash, that shows or hides the series (aria-pressed says which; a hidden one is struck through).
//
// THE CHART FOLLOWS THE PAGE. Its colours are read from the page at the moment it is drawn and it is drawn again when they change (light, dark,
// high contrast), it does not animate when the person asked for less motion, it is destroyed when its data changes and when the card leaves the
// page (no instance is left behind), and it is not created until its container has a size.

import { css, customElement, html, nothing, property, state, styleMap, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { Chart } from 'chart.js';
import type { ChartTheme } from '../chart-colors';
import { chartConfig, legendItems } from '../chart-config';
import { chartAriaLabel, chartCardState, chartHeight, chartRangeText, chartTableRows, chartText } from '../chart-view';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import { noteViews } from '../notes-view';
import type { ChartBlock, TextArgs } from '../types';
import { renderNotes, notesStyles } from './notes';
import { loadChart, prefersReducedMotion, readTheme, watchTheme, whenSized } from './chart-runtime';

/** Fired when a point of a clickable chart is chosen: the index of its label. */
export interface PointSelectDetail {
  index: number;
}

const sameArgs = (a: TextArgs | null | undefined, b: TextArgs | null | undefined): boolean => JSON.stringify(a ?? null) === JSON.stringify(b ?? null);

@customElement('esatto-seo-report-chart-card')
export class EsattoSeoReportChartCardElement extends UmbLitElement {
  @property({ attribute: false }) block: ChartBlock | null = null;

  /** The arguments the chart's texts are written with: the daily chart's come beside it. Compared by value, so a new object with the same figures redraws nothing. */
  @property({ attribute: false, hasChanged: (value, old) => !sameArgs(value as TextArgs | null, old as TextArgs | null) }) args: TextArgs | null = null;

  /** A click on a point is a choice (the daily chart's days): `point-select` is fired with its index. */
  @property({ type: Boolean }) selectable = false;

  /** The chart sits inside a card of its own (the daily card): no frame, and the heading is read out but not shown. */
  @property({ type: Boolean }) embedded = false;

  @state() private _theme: ChartTheme | null = null;
  @state() private _hidden: boolean[] = [];
  @state() private _failed = false;

  #chart: Chart | null = null;
  // Bumped by every draw and by leaving: a draw that is waiting (for a size, for the library) is dropped when it returns under an older one.
  #epoch = 0;
  #cancelSize: (() => void) | null = null;
  #unwatch: (() => void) | null = null;

  override connectedCallback() {
    super.connectedCallback();
    this.#unwatch = watchTheme(() => this.#themeChanged());
    // Moved in the page: the canvas is the same one, but the chart was destroyed on the way out.
    if (this.hasUpdated) void this.#draw();
  }

  override disconnectedCallback() {
    this.#unwatch?.();
    this.#unwatch = null;
    this.#destroy();
    super.disconnectedCallback();
  }

  override updated(changed: PropertyValues) {
    // The first render has no theme to colour the legend with: read it, which renders again.
    if (this._theme === null) {
      this._theme = readTheme(this.renderRoot);
      return;
    }

    if (changed.has('block') || changed.has('args') || changed.has('selectable') || changed.has('_theme')) void this.#draw();
  }

  // ---- The chart ---------------------------------------------------------------------------------------------------------------

  #themeChanged() {
    const theme = readTheme(this.renderRoot);
    if (JSON.stringify(theme) !== JSON.stringify(this._theme)) this._theme = theme;
  }

  #destroy() {
    this.#epoch++;
    this.#cancelSize?.();
    this.#cancelSize = null;
    this.#chart?.destroy();
    this.#chart = null;
  }

  async #draw() {
    this.#destroy();
    const epoch = this.#epoch;
    const cancelled = () => epoch !== this.#epoch;

    this._hidden = [];
    this._failed = false;

    const canvas = this.renderRoot.querySelector('canvas');
    const wrap = this.renderRoot.querySelector<HTMLElement>('.wrap');
    if (!canvas || !wrap || !this.block || chartCardState(this.block) === 'empty' || !this._theme) return;

    if (!(await whenSized(wrap, cancelled, (cancel) => (this.#cancelSize = cancel)))) return;

    let module: Awaited<ReturnType<typeof loadChart>>;
    try {
      module = await loadChart();
    } catch {
      if (!cancelled()) this._failed = true;
      return;
    }
    if (cancelled() || !canvas.isConnected) return;

    const config = chartConfig(this.block, {
      host: this,
      theme: this._theme,
      reducedMotion: prefersReducedMotion(),
      onPoint: this.selectable ? (index) => this.dispatchEvent(new CustomEvent<PointSelectDetail>('point-select', { detail: { index } })) : undefined,
    });
    this.#chart = new module.Chart(canvas, config);
  }

  #toggle(index: number) {
    const chart = this.#chart;
    if (!chart) return;

    chart.setDatasetVisibility(index, !chart.isDatasetVisible(index));
    chart.update();
    this._hidden = (this.block?.series ?? []).map((_, i) => !chart.isDatasetVisible(i));
  }

  // ---- Showing -----------------------------------------------------------------------------------------------------------------

  override render() {
    const block = this.block;
    if (!block) return nothing;

    const empty = chartCardState(block) === 'empty';
    const title = chartText(this, block, 'Title', this.args);
    const subtitle = empty ? '' : chartText(this, block, 'Subtitle', this.args);
    const range = empty ? '' : chartRangeText(this, block);
    const description = chartText(this, block, 'Description', this.args);

    return html`
      <figure class="chart ${this.embedded ? 'embedded' : 'card'}">
        <figcaption class="card-head ${this.embedded ? 'visually-hidden' : ''}">
          <span class="icon-badge" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
          <span class="titles">
            <h3>${title}</h3>
            ${subtitle ? html`<span class="muted">${subtitle}</span>` : nothing}
            ${range ? html`<span class="muted small">${range}</span>` : nothing}
          </span>
        </figcaption>
        ${description ? html`<p class="description muted">${description}</p>` : nothing}
        ${empty ? html`<p class="empty" role="status">${text(this, 'uiNoDataText')}</p>` : this.#plot(block, title)}
        ${renderNotes(noteViews(this, block.annotations, block.insights))}
      </figure>
    `;
  }

  #plot(block: ChartBlock, title: string) {
    const table = chartTableRows(this, block);
    const legend = this._theme ? legendItems(block, { host: this, theme: this._theme }) : [];

    return html`
      ${legend.length > 0
        ? html`<ul class="legend" aria-label=${text(this, 'uiChartLegendLabel')}>
            ${legend.map(
              (item, index) => html`<li>
                <button type="button" aria-pressed=${this._hidden[index] ? 'false' : 'true'} class=${this._hidden[index] ? 'off' : ''} @click=${() => this.#toggle(index)}>
                  <span
                    class="swatch"
                    style=${styleMap({ borderBottomWidth: `${item.width}px`, borderBottomStyle: item.dashed ? 'dashed' : 'solid', borderBottomColor: item.color })}
                    >${item.label}</span
                  >
                </button>
              </li>`,
            )}
          </ul>`
        : nothing}
      ${this._failed ? html`<p class="muted" role="status">${text(this, 'uiChartFailedText')}</p>` : nothing}
      <div class="wrap ${this.selectable ? 'selectable' : ''}" style=${styleMap({ height: `${chartHeight(block)}px` })}>
        <canvas role="img" aria-label=${chartAriaLabel(this, block, this.args)}></canvas>
      </div>
      <table class="visually-hidden">
        <caption>${text(this, 'uiChartTableCaption', { title })}</caption>
        <thead>
          <tr>
            <th scope="col">${table.periodHeader}</th>
            ${table.seriesHeaders.map((header) => html`<th scope="col">${header}</th>`)}
          </tr>
        </thead>
        <tbody>
          ${table.rows.map((row) => html`<tr><th scope="row">${row.label}</th>${row.cells.map((cell) => html`<td>${cell}</td>`)}</tr>`)}
        </tbody>
      </table>
    `;
  }

  static override styles = [
    shared,
    notesStyles,
    css`
      :host { display: block; }
      .chart { margin: 0; }
      .chart.embedded { padding: 0; }
      .titles { display: grid; gap: 2px; }
      .titles h3 { font-size: 16px; font-weight: 700; }
      .description { margin-bottom: var(--uui-size-space-4); max-width: 70ch; font-size: 0.85rem; line-height: 1.5; }
      .empty { padding: var(--uui-size-space-5); border-radius: var(--seo-radius-sm); background: var(--seo-wash); color: var(--seo-muted); }
      .wrap { position: relative; }
      .wrap.selectable { cursor: pointer; }

      /* The legend is text. Each entry is underlined with the line it names: its colour, its width and its dash. */
      .legend { display: flex; flex-wrap: wrap; gap: var(--uui-size-1) var(--uui-size-4); list-style: none; margin: 0 0 var(--uui-size-2); padding: 0; }
      .legend button {
        font: inherit; font-size: 0.85rem; color: var(--uui-color-text); background: none; border: 0; padding: var(--uui-size-1) 0; cursor: pointer;
      }
      .legend button:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
      .swatch { padding-bottom: 2px; }
      .legend button.off .swatch { text-decoration: line-through; opacity: 0.45; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-chart-card': EsattoSeoReportChartCardElement;
  }
}
