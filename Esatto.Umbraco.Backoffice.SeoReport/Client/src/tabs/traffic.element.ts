// The Traffic tab: the daily card (live from GA4, its own element and its own read), then the monthly charts by year, visits from search engines and
// all visits. It reads the report the shell holds and fetches nothing; what is shown is decided in tab-views.ts.

import { customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import type { Report } from '../types';
import { trafficView } from './tab-views';
import '../components/chart-card.element';
import './daily-card.element';

@customElement('esatto-seo-report-traffic')
export class EsattoSeoReportTrafficElement extends UmbLitElement {
  @property({ attribute: false }) report: Report | null = null;

  /** The selected month, "YYYY-MM". The daily card does not follow it: it has a window and a month of its own. */
  @property() period = '';

  override render() {
    if (!this.report) return nothing;

    return html`
      <div class="page">
        <esatto-seo-report-daily-card></esatto-seo-report-daily-card>
        ${trafficView(this.report).blocks.map((block) => html`<esatto-seo-report-chart-card .block=${block}></esatto-seo-report-chart-card>`)}
      </div>
    `;
  }

  static override styles = [shared];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-traffic': EsattoSeoReportTrafficElement;
  }
}
