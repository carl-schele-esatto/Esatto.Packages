// The "Analytics vs Search Console" tab: one chart that sets visits (GA4) against clicks (Search Console), year by year. Clicks are normally more
// than visits; the chart's description says why. It reads the report the shell holds and fetches nothing.

import { customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import type { Report } from '../types';
import { comparisonView } from './tab-views';
import '../components/chart-card.element';

@customElement('esatto-seo-report-comparison')
export class EsattoSeoReportComparisonElement extends UmbLitElement {
  @property({ attribute: false }) report: Report | null = null;

  /** The selected month, "YYYY-MM". */
  @property() period = '';

  override render() {
    const { block } = comparisonView(this.report);
    if (!block) return nothing;

    return html`<div class="page"><esatto-seo-report-chart-card .block=${block}></esatto-seo-report-chart-card></div>`;
  }

  static override styles = [shared];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-comparison': EsattoSeoReportComparisonElement;
  }
}
