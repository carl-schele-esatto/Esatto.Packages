// The editor of the month's note: a labelled text box, a counter, Save, and what the last save came to. It only draws and says what was done: the
// text box and its draft are its own, and the model (analysis-model.ts) decides what a save does (a note over the limit is refused there, not sent).
//
//   save   the person pressed Save: detail { text }, the draft as typed (the model trims it)
//   edit   the draft changed: what was said about the last save no longer applies
//   retry  "Try again" after a save that failed
//
// THE LIMIT IS COUNTED AS THE SERVER COUNTS IT (noteLimit): the trimmed text, in UTF-16 code units. The text box has no `maxlength`: that would stop
// a pasted text without a word; instead the counter says how much is used, and a text that is too long is refused at Save with the reason.
//
// ACCESSIBILITY. The label is visible and the text box is described by the help and the counter; the refusal is a role=alert region that is always
// in the page (a message inserted together with its live region is not announced, one that is filled is); "Saved" is a polite one. The Save button is
// never disabled (see editor-styles.ts), so the keyboard's place survives a save; the text box is read-only meanwhile (it stays focusable), so that
// what is typed during a save is not replaced by what the server kept.

import { customElement, html, live, nothing, property, state, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import type { SaveState } from '../analysis-model';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import { NOTE_MAX_LENGTH, noteLimit } from '../note-text';
import { editorStyles, renderSaveRow } from './editor-styles';
import '../problem.element';

const IDLE: SaveState = { saving: false, saved: false, revision: 0, problem: null, refused: false };

@customElement('esatto-seo-report-note-editor')
export class EsattoSeoReportNoteEditorElement extends UmbLitElement {
  /** The note of the month as it is saved, or null. */
  @property({ attribute: false }) note: string | null = null;

  /** The month the note belongs to, "YYYY-MM": a different month starts the text box over. */
  @property() period = '';

  @property({ attribute: false }) saveState: SaveState = IDLE;

  @state() private _draft = '';

  // The text box shows what is saved again when the month or the saved note changes, and after every save that succeeded (what the server kept
  // is trimmed, and may equal what was shown before).
  #revision = -1;

  override willUpdate(changed: PropertyValues) {
    if (changed.has('note') || changed.has('period') || this.saveState.revision !== this.#revision) {
      this.#revision = this.saveState.revision;
      this._draft = this.note ?? '';
    }
  }

  #input = (event: Event) => {
    this._draft = (event.target as HTMLTextAreaElement).value;
    this.dispatchEvent(new CustomEvent('edit'));
  };

  // A save that is on its way ignores the click here as well as in the model, so that nothing is sent twice.
  #save = () => {
    if (this.saveState.saving) return;
    this.dispatchEvent(new CustomEvent<{ text: string }>('save', { detail: { text: this._draft } }));
  };

  override render() {
    const limit = noteLimit(this._draft);
    const { refused, problem } = this.saveState;

    return html`
      <div class="editor">
        <label for="note">${text(this, 'uiMonthNoteLabel')}</label>
        <textarea
          id="note"
          rows="4"
          aria-describedby="note-help note-counter note-error"
          aria-invalid=${limit.over ? 'true' : 'false'}
          ?readonly=${this.saveState.saving}
          .value=${live(this._draft)}
          @input=${this.#input}
        ></textarea>
        <div class="counters">
          <span class="counter ${limit.over ? 'over' : ''}" id="note-counter">
            ${limit.over ? html`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>` : nothing}${text(this, 'uiNoteCounterText', { length: limit.length, max: NOTE_MAX_LENGTH })}
          </span>
        </div>
        <p class="help muted" id="note-help">${text(this, 'uiMonthNoteHelpText')}</p>
        <div id="note-error" role="alert">
          ${refused
            ? html`<p class="error"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiNoteTooLongText', { over: limit.excess, max: NOTE_MAX_LENGTH })}</p>`
            : nothing}
        </div>
        ${renderSaveRow(this, this.saveState, this.#save)}
        ${problem ? html`<esatto-seo-report-problem .error=${problem} @retry=${() => this.dispatchEvent(new CustomEvent('retry'))}></esatto-seo-report-problem>` : nothing}
      </div>
    `;
  }

  static override styles = [shared, editorStyles];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-note-editor': EsattoSeoReportNoteEditorElement;
  }
}
