// Test connection on the Settings tab, and the service account's address to add in Google.
//
// The button tests the values in the form before they are saved (the model builds the request: a blank key box tests the key in force). The
// result is three rows, the key, Google Analytics and Search Console (settings-view.ts, testRows), each with an icon AND a word for how it went
// and, for anything but a pass, what happened and what to do, in the words of its code; never anything the server or Google said. A test that
// could not be run at all is the problem card with Try again.
//
// THE ADDRESS is shown prominently, because it is what has to be added in Google Analytics and in Search Console: as text that can be selected,
// and with a Copy button (the clipboard when the browser allows it; otherwise it says to select and copy it by hand). It is public, not a secret.
//
// "Testing..." and "finished" are said in a polite line that is always in the page; the button stays focusable while the test runs (aria-disabled
// and aria-busy, never `disabled`) and ignores the click.
//
//   test   the button was pressed (also "Try again")

import { css, customElement, html, nothing, property, state, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import type { TestState } from '../settings-model';
import { testRows, type TestRow } from '../settings-view';
import { editorStyles } from './editor-styles';
import { settingsStyles } from './settings-styles';
import '../problem.element';

const IDLE: TestState = { busy: false, result: null, problem: null };

@customElement('esatto-seo-report-connection-test')
export class EsattoSeoReportConnectionTestElement extends UmbLitElement {
  @property({ attribute: false }) test: TestState = IDLE;

  /** The service account's address to show: the key in the box's, else the tested key's, else the saved key's. Null: none is known. */
  @property({ attribute: false }) email: string | null = null;

  /** The saved key's address, for a failure that says "add this address" when the test itself had none. */
  @property({ attribute: false }) savedEmail: string | null = null;

  @state() private _copy: 'copied' | 'failed' | null = null;

  override willUpdate(changed: PropertyValues) {
    if (changed.has('email')) this._copy = null;
  }

  #test = () => {
    if (!this.test.busy) this.dispatchEvent(new CustomEvent('test'));
  };

  #copy = async () => {
    const email = this.email;
    if (!email) return;

    try {
      await navigator.clipboard.writeText(email);
      this._copy = 'copied';
    } catch {
      // No clipboard (an insecure page, a browser that refuses): the address is there to be selected.
      this._copy = 'failed';
    }
  };

  override render() {
    const { busy, result, problem } = this.test;

    return html`
      ${this.email ? this.#account(this.email) : nothing}
      <div class="buttons">
        <button type="button" class="save" aria-disabled=${busy ? 'true' : 'false'} aria-busy=${busy ? 'true' : 'false'} aria-describedby="test-help" @click=${this.#test}>
          ${busy ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : nothing}${text(this, 'uiTestConnectionLabel')}
        </button>
      </div>
      <p class="help muted" id="test-help">${text(this, 'uiTestHelpText')}</p>
      <p class="status-line" role="status" aria-live="polite">
        ${busy ? text(this, 'uiTestingText') : result ? text(this, 'uiTestDoneText') : nothing}
      </p>
      ${problem ? html`<esatto-seo-report-problem .error=${problem} @retry=${this.#test}></esatto-seo-report-problem>` : nothing}
      ${result ? this.#result(testRows(this, result, this.savedEmail)) : nothing}
    `;
  }

  #account(email: string) {
    return html`
      <div class="account">
        <p class="account-label" id="account-label">${text(this, 'uiServiceAccountLabel')}</p>
        <div class="buttons">
          <code class="address" id="account-address" aria-labelledby="account-label account-address">${email}</code>
          <button type="button" class="secondary" aria-describedby="account-address" @click=${this.#copy}>
            <uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${text(this, 'uiCopyAddressLabel')}
          </button>
          <span class="status-line" role="status" aria-live="polite">
            ${this._copy === 'copied'
              ? html`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${text(this, 'uiCopiedText')}`
              : this._copy === 'failed'
                ? html`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiCopyFailedText')}`
                : nothing}
          </span>
        </div>
        <p class="help muted">${text(this, 'uiServiceAccountHelpText')}</p>
      </div>
    `;
  }

  #result(rows: TestRow[]) {
    return html`
      <section class="result" aria-labelledby="result-title">
        <h4 id="result-title">${text(this, 'uiTestResultTitle')}</h4>
        <ul>
          ${rows.map(
            (row) => html`<li class="tone-${row.tone}">
              <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${row.icon}></uui-icon></span>
              <div>
                <p><strong>${row.label}:</strong> ${row.status}</p>
                ${row.title ? html`<p class="row-title">${row.title}</p>` : nothing} ${row.text ? html`<p class="row-text">${row.text}</p>` : nothing}
              </div>
            </li>`,
          )}
        </ul>
      </section>
    `;
  }

  static override styles = [
    shared,
    editorStyles,
    settingsStyles,
    css`
      :host { display: grid; gap: var(--uui-size-space-3); }
      .account { display: grid; gap: var(--uui-size-space-2); padding: var(--uui-size-space-4); border-radius: var(--seo-radius-sm); background: var(--seo-wash); border: 1px solid var(--seo-line); }
      .account-label { font-size: 13px; font-weight: 600; }
      .address { font-size: 14px; padding: 4px 8px; user-select: all; overflow-wrap: anywhere; }
      h4 { font-size: 14px; font-weight: 700; margin-bottom: var(--uui-size-space-3); }
      ul { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
      li { display: flex; gap: var(--uui-size-space-3); align-items: flex-start; }
      li > div { display: grid; gap: 4px; padding-top: 6px; max-width: 80ch; line-height: 1.5; }
      .row-title { font-weight: 700; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-connection-test': EsattoSeoReportConnectionTestElement;
  }
}
