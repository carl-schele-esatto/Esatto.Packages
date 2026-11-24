// The To fix tab: what the report found that is worth acting on. The findings are the warnings among the search block's annotations, in the order the
// report lists them (tab-views.ts, toFixItems), each with its title, its text and a severity icon AND word. It reads the report the shell holds and
// fetches nothing.
//
// The tab only exists while there are findings (visibleTabs asks hasWarnings), and the server withholds the warnings from a user outside the findings
// groups, so for that user there is no tab. If the list becomes empty while the tab is the active one (another month, say), the shell falls back to
// the first tab (activeTab); the text here is only a safety net for the moment in between.

import { customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { notesStyles, renderNotes } from '../components/notes';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import type { Report } from '../types';
import { toFixItems } from './tab-views';

@customElement('esatto-seo-report-to-fix')
export class EsattoSeoReportToFixElement extends UmbLitElement {
  @property({ attribute: false }) report: Report | null = null;

  /** The selected month, "YYYY-MM". */
  @property() period = '';

  override render() {
    if (!this.report) return nothing;

    const items = toFixItems(this, this.report);

    return html`
      <section class="card" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge tone-warning" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
          <h3 id="title">${text(this, 'uiTabToFixLabel')}</h3>
        </div>
        ${items.length > 0
          ? html`<p class="muted">${text(this, 'uiToFixHelpText')}</p>${renderNotes(items)}`
          : html`<p class="muted">${text(this, 'uiToFixEmptyText')}</p>`}
      </section>
    `;
  }

  static override styles = [shared, notesStyles];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-to-fix': EsattoSeoReportToFixElement;
  }
}
