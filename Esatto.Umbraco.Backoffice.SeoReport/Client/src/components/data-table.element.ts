// A table of already-written cells (table-rows.ts builds them): the search queries, the landing pages, the brand and non-brand figures, and where a
// day's visits came from. It only draws. It is a real <table>: a caption (the title of the card around it, read out and not shown), column
// headers with `scope="col"`, and the first cell of each row as its row header, so a screen reader announces each figure with its column and
// its row. The tables are rankings and are not sortable, so there are no sort controls to operate.
//
// A cell can carry a glyph before its text (a status: decoration, the text says the same) and a second, muted line under it (where a position came from).
// A column of running text (advice) is marked `wide`: it gets room and wraps.
//
// A figure that is missing is written as words and muted ("No data"), never a dash and never a zero. A table with no rows says so ("No data
// yet.") instead of showing headers over nothing. The numeric columns are right-aligned with tabular figures, so they line up. A wide table
// scrolls inside its own region, which can be reached and scrolled with the keyboard.

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import type { TableColumn, TableRow } from '../table-rows';
import { renderTrend, trendStyles } from './trend';

@customElement('esatto-seo-report-data-table')
export class EsattoSeoReportDataTableElement extends UmbLitElement {
  @property() caption = '';
  @property({ attribute: false }) columns: TableColumn[] = [];
  @property({ attribute: false }) rows: TableRow[] = [];

  override render() {
    if (this.rows.length === 0) return html`<p class="muted">${text(this, 'uiNoDataText')}</p>`;

    return html`
      <div class="scroll" role="region" aria-label=${this.caption} tabindex="0">
        <table>
          <caption class="visually-hidden">${this.caption}</caption>
          <thead>
            <tr>
              ${this.columns.map((column) => html`<th scope="col" class=${`${column.numeric ? 'num' : ''} ${column.wide ? 'wide' : ''}`.trim()}>${column.label}</th>`)}
            </tr>
          </thead>
          <tbody>
            ${this.rows.map(
              (row) => html`<tr>
                ${row.cells.map((cell, index) => {
                  const column = this.columns[index];
                  const classes = `${column?.numeric ? 'num' : ''} ${column?.wide ? 'wide' : ''} ${cell.missing ? 'muted' : ''}`.trim();
                  const written = cell.strong ? html`<strong class="name" title=${cell.title ?? nothing}>${cell.text}</strong>` : html`${cell.text}`;
                  // A status glyph is decoration: the text beside it says the same, and is what is read out.
                  const content = cell.icon ? html`<span class="with-icon"><uui-icon name=${cell.icon} aria-hidden="true"></uui-icon>${written}</span>` : written;
                  const detail = cell.detail ? html`<span class="detail muted">${cell.detail}</span>` : nothing;
                  return index === 0
                    ? html`<th scope="row" class=${classes}>${content}${detail}</th>`
                    : html`<td class=${classes}>${content}${detail}${cell.trend ? renderTrend(cell.trend) : nothing}</td>`;
                })}
              </tr>`,
            )}
          </tbody>
        </table>
      </div>
    `;
  }

  static override styles = [
    shared,
    trendStyles,
    css`
      .scroll { overflow-x: auto; }
      table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
      th, td { padding: var(--uui-size-space-3) var(--uui-size-space-4); text-align: left; border-bottom: 1px solid var(--seo-line); vertical-align: top; }
      thead th {
        font-size: 12px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; color: var(--seo-muted);
        border-bottom: 2px solid var(--seo-line); white-space: nowrap;
      }
      tbody th { font-weight: 400; }
      tbody tr:last-child th, tbody tr:last-child td { border-bottom: 0; }
      tbody tr:hover { background: var(--seo-wash); }
      .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
      /* Running text (advice) gets room and wraps, instead of being squeezed beside the figures. */
      .wide { min-width: 22rem; }
      td.num .detail { text-align: right; }
      .detail { display: block; margin-top: 2px; font-size: 12px; line-height: 1.4; white-space: normal; }
      .with-icon { display: inline-flex; gap: 6px; align-items: center; white-space: nowrap; }
      .with-icon uui-icon { flex: none; }
      /* A trend under a figure keeps to the figure's right edge. */
      td.num .trend { display: flex; justify-content: flex-end; margin-top: 2px; }
      /* A long name (a page's path, a search query) is cut with an ellipsis; the whole of it stays in the text and in the title. */
      .name { display: inline-block; max-width: 40ch; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; vertical-align: bottom; font-weight: 600; }
      .scroll:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-data-table': EsattoSeoReportDataTableElement;
  }
}
