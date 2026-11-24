// A request that failed, shown where it happened: what happened (the code's title), what to do (its text), the server's reference for the
// log, and a "Try again" button. Used by the shell and by every tab, so a failure reads the same wherever it occurs.
//
// It shows the code's wording, never the server's: the server sends a code and arguments and no prose (ApiError.code is always one the
// client has words for, `unknown` otherwise).

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { ApiError } from './api-error';
import { shared } from './dashboard.styles';
import { errorTexts, text } from './localize';
import type { TextArgs } from './types';

@customElement('esatto-seo-report-problem')
export class EsattoSeoReportProblemElement extends UmbLitElement {
  @property({ attribute: false }) error: ApiError | null = null;

  /** Arguments the client knows and the server does not, such as the service account's address (`email`). Added to the server's. */
  @property({ attribute: false }) extraArgs: TextArgs | undefined;

  /** Whether to offer "Try again". On by default; the `retry` event says it was pressed. */
  @property({ type: Boolean }) retryable = true;

  #retry = () => this.dispatchEvent(new CustomEvent('retry'));

  override render() {
    if (!this.error) return nothing;

    const { title, text: body } = errorTexts(this, this.error.code, { ...this.error.args, ...this.extraArgs });

    // role=alert: a failure is announced at once, unlike the progress texts, which are polite.
    return html`
      <div class="banner tone-critical" role="alert">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
        <div>
          <h3>${title}</h3>
          <p>${body}</p>
          ${this.error.traceId ? html`<p class="muted small">${text(this, 'uiProblemReferenceText', { traceId: this.error.traceId })}</p>` : nothing}
          ${this.retryable
            ? html`<div class="actions">
                <uui-button look="outline" label=${text(this, 'uiTryAgainLabel')} @click=${this.#retry}>${text(this, 'uiTryAgainLabel')}</uui-button>
              </div>`
            : nothing}
        </div>
      </div>
    `;
  }

  static override styles = [shared, css`h3 { font-size: 15px; font-weight: 700; }`];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-problem': EsattoSeoReportProblemElement;
  }
}
