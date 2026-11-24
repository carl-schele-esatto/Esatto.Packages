// The "Most clicked" tab: the top search queries and the top landing pages of the month, as two tables, and the note that they do not add up to the
// total. A top list is cut (tab-views.ts, table-rows.ts) and is not sortable: it is a ranking, most clicked first, as the report has it. A page is
// shown as its shortened path in plain text with the whole address as its title (this is a report, not a way out of it). It reads the report the
// shell holds and fetches nothing.

import { customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { renderNotes, notesStyles } from '../components/notes';
import { renderTableCard, tableCardStyles } from '../components/table-card';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import type { Report } from '../types';
import { topListsView } from './tab-views';

@customElement('esatto-seo-report-top-pages')
export class EsattoSeoReportTopPagesElement extends UmbLitElement {
  @property({ attribute: false }) report: Report | null = null;

  /** The selected month, "YYYY-MM". */
  @property() period = '';

  override render() {
    if (!this.report) return nothing;

    const view = topListsView(this, this.report);
    if (view.state === 'empty') return html`<section class="card"><p class="muted">${text(this, 'uiNoDataText')}</p></section>`;

    return html`
      <div class="page">
        ${view.queries ? renderTableCard({ ...view.queries }) : nothing} ${view.pages ? renderTableCard({ ...view.pages }) : nothing}
        ${renderNotes(view.note ? [view.note] : [])}
      </div>
    `;
  }

  static override styles = [shared, notesStyles, tableCardStyles];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-top-pages': EsattoSeoReportTopPagesElement;
  }
}
