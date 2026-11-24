// The Queries tab: how the searches that led to the site split into brand and non-brand. A table of the two segments and the unfiltered total
// (which they never add up to), the two charts of clicks and impressions per month, and the notes about the data. It reads the report the shell
// holds and fetches nothing; what is shown, and when, is decided in tab-views.ts.
//
// Without brand terms there is no split: the heading about it and the two charts are left out (not drawn empty), the unfiltered total is shown on
// its own, and a line says why and where the terms are set.

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { renderNotes, notesStyles } from '../components/notes';
import { renderTableCard, tableCardStyles } from '../components/table-card';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import type { Report } from '../types';
import { queriesView } from './tab-views';
import '../components/chart-card.element';

@customElement('esatto-seo-report-queries')
export class EsattoSeoReportQueriesElement extends UmbLitElement {
  @property({ attribute: false }) report: Report | null = null;

  /** The selected month, "YYYY-MM". */
  @property() period = '';

  override render() {
    if (!this.report) return nothing;

    const view = queriesView(this, this.report);
    if (view.state === 'empty') return html`<section class="card"><p class="muted">${text(this, 'uiNoDataText')}</p></section>`;

    return html`
      <div class="page">
        ${view.table ? renderTableCard({ ...view.table }) : nothing}
        ${view.hint ? html`<p class="hint muted">${view.hint}</p>` : nothing}
        ${view.charts.map((block) => html`<esatto-seo-report-chart-card .block=${block}></esatto-seo-report-chart-card>`)} ${renderNotes(view.notes)}
      </div>
    `;
  }

  static override styles = [shared, notesStyles, tableCardStyles, css`.hint { font-size: 0.9rem; max-width: 70ch; }`];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-queries': EsattoSeoReportQueriesElement;
  }
}
