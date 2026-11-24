// The card shown while the package is not connected to Google: no key, no property, or a saved key this environment cannot read.
//
// It says what is wrong (the same title and text the error codes have: notConfigured or keyUnreadable), lists what is still needed, and
// points to where the cure is: a "Settings" button for a user who may open Settings, and for anyone else a plain "ask an
// administrator". While the dashboard does not yet know whether the user may (the current user loads asynchronously) it shows neither
// the button nor the blunt refusal, but a hint that covers both.

import { css, customElement, html, nothing, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from './dashboard.styles';
import { errorTexts, text } from './localize';
import { TAB_LABEL_KEYS, navigate } from './navigate';
import type { MissingSetting, StatusResponse } from './types';

// The key of each thing that can be missing, in the order the status lists them.
const MISSING_LABEL_KEYS: Record<MissingSetting, string> = {
  key: 'uiMissingKeyLabel',
  ga4PropertyId: 'uiMissingGa4PropertyIdLabel',
  searchConsoleSite: 'uiMissingSearchConsoleSiteLabel',
};

@customElement('esatto-seo-report-not-connected')
export class EsattoSeoReportNotConnectedElement extends UmbLitElement {
  @property({ attribute: false }) status: StatusResponse | null = null;

  /** Whether the user may open Settings: true, false, or undefined while that is not known yet. */
  @property({ attribute: false }) canOpenSettings: boolean | undefined;

  override render() {
    if (!this.status) return nothing;

    const { title, text: body } = errorTexts(this, this.status.setup === 'keyUnreadable' ? 'keyUnreadable' : 'notConfigured');
    const missing = this.status.missing.filter((setting) => setting in MISSING_LABEL_KEYS);

    return html`
      <section class="card tone-notice" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-link"></uui-icon></span>
          <h3 id="title">${title}</h3>
        </div>
        <div class="stack">
          <p>${body}</p>
          ${missing.length > 0
            ? html`
                <div>
                  <p id="missing" class="muted small">${text(this, 'uiMissingLabel')}</p>
                  <ul aria-labelledby="missing">
                    ${missing.map((setting) => html`<li>${text(this, MISSING_LABEL_KEYS[setting])}</li>`)}
                  </ul>
                </div>
              `
            : nothing}
          ${this.#where()}
        </div>
      </section>
    `;
  }

  #where() {
    if (this.canOpenSettings === true) {
      return html`<uui-button look="primary" label=${text(this, TAB_LABEL_KEYS.settings)} @click=${() => navigate(this, 'settings')}>
        ${text(this, TAB_LABEL_KEYS.settings)}
      </uui-button>`;
    }

    return html`<p class="muted">${text(this, this.canOpenSettings === false ? 'uiAskAdministratorText' : 'uiFindSettingsText')}</p>`;
  }

  static override styles = [shared, css`ul { margin: var(--uui-size-space-2) 0 0; padding-left: var(--uui-size-space-5); }`];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-not-connected': EsattoSeoReportNotConnectedElement;
  }
}
