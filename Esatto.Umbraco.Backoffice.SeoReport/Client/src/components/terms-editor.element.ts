// The editor of the target queries: a labelled text box (one target query per line), counters, Save, and what the last save came to. It only draws
// and says what was done; the model (analysis-model.ts) decides what a save does.
//
//   save   the person pressed Save: detail { text }, the text as typed (the SERVER normalizes it: it is authoritative)
//   edit   the text changed: what was said about the last save no longer applies
//   retry  "Try again" after a save that failed
//
// WHAT IS ABOUT TO BE LOST IS SAID BEFORE IT IS LOST. The server keeps at most 50 target queries of at most 100 characters and drops the rest without
// a word, so the counters repeat those limits (target-terms.ts) and a warning says when a target query is too long or the list too long. They are
// warnings and not refusals: the rules are the server's, and after a save the text box shows the list as the server kept it, which is the proof.
//
// The text box starts from the list it is given and starts over when that list changes or a save succeeds. The Save button is never disabled (see
// editor-styles.ts); the text box is read-only while a save is on its way (it stays focusable), so that what is typed meanwhile is not replaced by what
// the server kept. "Saved" is announced politely.

import { customElement, html, live, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { SaveState } from '../analysis-model';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import { MAX_TERMS, MAX_TERM_LENGTH, termsCheck, textFromTerms } from '../target-terms';
import { editorStyles, renderSaveRow } from './editor-styles';
import '../problem.element';

const IDLE: SaveState = { saving: false, saved: false, revision: 0, problem: null, refused: false };

@customElement('esatto-seo-report-terms-editor')
export class EsattoSeoReportTermsEditorElement extends UmbLitElement {
  /** The target queries as they are kept, in the editor's order. */
  @property({ attribute: false }) terms: string[] = [];

  @property({ attribute: false }) saveState: SaveState = IDLE;

  @state() private _draft = '';

  // The list is compared by what it says: a new array with the same terms (every render of the tab makes one) must not wipe what is being typed.
  #shown: string | null = null;
  #revision = -1;

  override willUpdate() {
    const shown = textFromTerms(this.terms);
    if (shown !== this.#shown || this.saveState.revision !== this.#revision) {
      this.#shown = shown;
      this.#revision = this.saveState.revision;
      this._draft = shown;
    }
  }

  #input = (event: Event) => {
    this._draft = (event.target as HTMLTextAreaElement).value;
    this.dispatchEvent(new CustomEvent('edit'));
  };

  #save = () => {
    if (this.saveState.saving) return;
    this.dispatchEvent(new CustomEvent<{ text: string }>('save', { detail: { text: this._draft } }));
  };

  override render() {
    const check = termsCheck(this._draft);
    const { problem } = this.saveState;

    return html`
      <div class="editor">
        <label for="terms">${text(this, 'uiTargetsEditorLabel')}</label>
        <textarea
          id="terms"
          rows="6"
          aria-describedby="terms-help terms-counter terms-warnings"
          ?readonly=${this.saveState.saving}
          .value=${live(this._draft)}
          @input=${this.#input}
        ></textarea>
        <div class="counters">
          <span class="counter ${check.tooMany ? 'over' : ''}" id="terms-counter">
            ${check.tooMany ? html`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>` : nothing}${text(this, 'uiTermsCounterText', { count: check.count, max: MAX_TERMS })}
          </span>
        </div>
        <div id="terms-warnings">
          ${check.tooMany
            ? html`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiTermsTooManyText', { max: MAX_TERMS })}</p>`
            : nothing}
          ${check.tooLong > 0
            ? html`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiTermsTooLongText', { count: check.tooLong, max: MAX_TERM_LENGTH })}</p>`
            : nothing}
        </div>
        <p class="help muted" id="terms-help">${text(this, 'uiTargetsEditorHelpText')}</p>
        ${renderSaveRow(this, this.saveState, this.#save)}
        ${problem ? html`<esatto-seo-report-problem .error=${problem} @retry=${() => this.dispatchEvent(new CustomEvent('retry'))}></esatto-seo-report-problem>` : nothing}
      </div>
    `;
  }

  static override styles = [shared, editorStyles];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-terms-editor': EsattoSeoReportTermsEditorElement;
  }
}
