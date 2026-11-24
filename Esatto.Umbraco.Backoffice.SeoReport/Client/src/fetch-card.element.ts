// The card for the backfill: how far a running fetch has got, or, when nothing runs and there is nothing to show, that nothing has been
// fetched yet with the explicit "Fetch now" button. Opening the dashboard never fetches: the button is the only way from here.
//
// A run that is "starting" (the server has accepted it but not counted its months yet) is shown as such, never as "nothing to fetch".
// The progress text is a polite live region, so a screen reader hears the count change without being interrupted.

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from './dashboard.styles';
import { errorTexts, text } from './localize';
import type { FetchStatus } from './types';
import { lastErrorOf, progressOf } from './view-state';

@customElement('esatto-seo-report-fetch-card')
export class EsattoSeoReportFetchCardElement extends UmbLitElement {
  @property({ attribute: false }) fetch: FetchStatus | null = null;

  /** Disables "Fetch now" while a request to start one is on its way. */
  @property({ type: Boolean }) busy = false;

  #fetchNow = () => this.dispatchEvent(new CustomEvent('fetch-now'));

  override render() {
    if (!this.fetch) return nothing;

    const progress = progressOf(this.fetch);
    return progress.phase === 'idle' ? this.#idle(this.fetch) : this.#running(progress);
  }

  #running(progress: ReturnType<typeof progressOf>) {
    const starting = progress.phase === 'starting';

    return html`
      <section class="card tone-notice" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-sync"></uui-icon></span>
          <h3 id="title">${text(this, 'uiProgressTitle')}</h3>
        </div>
        <div class="stack" role="status" aria-live="polite">
          <p>
            ${starting
              ? text(this, 'uiProgressStartingText')
              : text(this, 'uiProgressText', { done: progress.done, total: progress.total, period: progress.currentPeriod })}
          </p>
          <progress
            max="100"
            value=${progress.percent ?? nothing}
            aria-label=${text(this, 'uiProgressTitle')}
          ></progress>
          <p class="muted small">${text(this, 'uiProgressHintText')}</p>
        </div>
      </section>
    `;
  }

  #idle(fetch: FetchStatus) {
    // Why the last run stopped, when it did not end well: the latest run's code, else the last failure on record that nothing has put right.
    const failure = lastErrorOf(fetch);
    const reason = failure ? errorTexts(this, failure) : null;

    return html`
      <section class="card" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge solid round tone-none" aria-hidden="true"><uui-icon name="icon-cloud-download"></uui-icon></span>
          <h3 id="title">${text(this, 'uiNotFetchedTitle')}</h3>
        </div>
        <div class="stack">
          <p>${text(this, 'uiNotFetchedText')}</p>
          ${reason
            ? html`<div class="banner tone-warning" role="status">
                <div>
                  <h4>${text(this, 'uiFetchFailedTitle')}</h4>
                  <p><strong>${reason.title}</strong> ${reason.text}</p>
                </div>
              </div>`
            : nothing}
          <uui-button look="primary" label=${text(this, 'uiFetchNowLabel')} ?disabled=${this.busy} @click=${this.#fetchNow}>
            ${text(this, 'uiFetchNowLabel')}
          </uui-button>
        </div>
      </section>
    `;
  }

  static override styles = [
    shared,
    css`
      progress { width: 100%; height: 10px; accent-color: var(--uui-color-interactive); }
      h4 { font-size: 14px; font-weight: 700; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-fetch-card': EsattoSeoReportFetchCardElement;
  }
}
