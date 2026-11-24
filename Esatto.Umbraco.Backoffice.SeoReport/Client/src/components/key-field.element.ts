// The service-account key on the Settings tab. Write-only: a saved key is never shown, only whose it is ("Key saved · service account ...").
//
// WHAT IT SHOWS (`mode`, settings-view.ts keyModeOf): a configured key read-only ("Set in configuration"); a configured key that cannot be used,
// with the developer as the cure (no box: a key pasted here would lose to the configuration); a saved key with Replace and Remove; the box and the
// file chooser while replacing, when the saved key cannot be read (with why), and when there is none.
//
// WHERE THE TEXT GOES. What is typed, pasted or read from a file lives in the text box (the DOM's own value; no property of this element holds
// it) and is handed to the model through `onDraft`, a callback, so it never travels in an event. A file is read in the browser (key-check.ts,
// readKeyFile: a file that is too big is refused before it is read), put in the box like a paste, and the file input is reset so the same file can
// be chosen again. The box is emptied whenever `draftRevision` changes (the model forgot the key: it was saved, removed, or Cancel or Discard
// changes was pressed) and goes with the element. Nothing is logged, and the box asks the browser not to fill in, correct or remember it.
//
// FOCUS. Every button stays focusable (aria-disabled, never `disabled`), and when a button goes away because it was used, the focus goes where the
// person is headed: Replace to the box, Cancel back to Replace, Remove to its confirmation, the confirmation's Cancel back to Remove, a removal
// that succeeded to the box.
//
//   replace, cancel-replace, ask-remove, cancel-remove, confirm-remove   the buttons; the model decides
//   retry-remove                                                         "Try again" after a removal that failed

import { css, customElement, html, nothing, property, query, state, type PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import { MAX_KEY_BYTES, readKeyFile, type KeyProblem } from '../key-check';
import { errorTexts, text } from '../localize';
import type { ActionState } from '../settings-model';
import { acceptsDraft, type KeyMode } from '../settings-view';
import { editorStyles } from './editor-styles';
import { settingsStyles } from './settings-styles';
import '../problem.element';

const IDLE: ActionState = { busy: false, done: false, problem: null };

// The words for what is wrong with a pasted key, by the code key-check.ts gives.
const PROBLEM_KEYS: Record<KeyProblem, string> = {
  notJson: 'uiKeyNotJsonText',
  notServiceAccount: 'uiKeyNotServiceAccountText',
  noClientEmail: 'uiKeyNoClientEmailText',
  noPrivateKey: 'uiKeyNoPrivateKeyText',
  tooLarge: 'uiKeyTooLargeText',
};

type FocusTarget = 'box' | 'replace' | 'remove' | 'confirm' | 'afterRemove';

@customElement('esatto-seo-report-key-field')
export class EsattoSeoReportKeyFieldElement extends UmbLitElement {
  @property() mode: KeyMode = 'none';

  /** The service account of the key in force, or null. */
  @property({ attribute: false }) email: string | null = null;

  @property({ type: Boolean }) hasDraft = false;
  @property({ attribute: false }) draftEmail: string | null = null;
  @property({ attribute: false }) draftProblem: KeyProblem | null = null;

  /** Counts the times the model forgot the key: the box is emptied when it changes. */
  @property({ type: Number }) draftRevision = 0;

  @property({ type: Boolean }) confirmingRemove = false;
  @property({ attribute: false }) removal: ActionState = IDLE;

  /** A save is on its way (see also `removal`): the box is read-only and the buttons ignore clicks. */
  @property({ type: Boolean }) busy = false;

  /** Hands what is in the box to the model. A callback, not an event: the key is not to travel through the event system. */
  @property({ attribute: false }) onDraft: (text: string) => void = () => {};

  /** A chosen file that was not read: too big, or it could not be read. Never anything of its content. */
  @state() private _fileProblem: 'tooLarge' | 'unreadable' | null = null;

  @query('#key') private _box?: HTMLTextAreaElement;
  @query('#key-file') private _file?: HTMLInputElement;

  #focusNext: FocusTarget | null = null;

  // A save or a removal is on its way: the model takes no key meanwhile, so the box and the file chooser take none either (what was put in the box
  // would show but not be held, and a retried save would send no key).
  get #writing(): boolean {
    return this.busy || this.removal.busy;
  }

  override updated(changed: PropertyValues) {
    if (changed.has('draftRevision') && changed.get('draftRevision') !== undefined) {
      if (this._box) this._box.value = '';
      this._fileProblem = null;
    }

    this.#moveFocus(changed);
  }

  #moveFocus(changed: PropertyValues) {
    const target = this.#focusNext;
    if (!target) return;

    // A removal is done when the box is back (the mode changed); until then, and after one that failed, the focus stays on the confirmation.
    if (target === 'afterRemove') {
      if (changed.has('mode') && acceptsDraft(this.mode)) {
        this.#focusNext = null;
        this._box?.focus();
      }
      return;
    }

    const element = this.renderRoot.querySelector<HTMLElement>(target === 'box' ? '#key' : `#key-${target}`);
    if (element) {
      this.#focusNext = null;
      element.focus();
    }
  }

  // A click on a button that is busy, or while a save is on its way, does nothing; the button stays focusable.
  #act(name: string, focus: FocusTarget | null, ignore = this.#writing) {
    return () => {
      if (ignore) return;
      this.#focusNext = focus;
      this.dispatchEvent(new CustomEvent(name));
    };
  }

  #input = (event: Event) => {
    this._fileProblem = null;
    this.onDraft((event.target as HTMLTextAreaElement).value);
  };

  #chooseFile = () => {
    if (!this.#writing) this._file?.click();
  };

  #fileChosen = async (event: Event) => {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // the same file can be chosen again
    if (!file) return;

    const read = await readKeyFile(file);
    // The element may have left, or the box gone, while the file was read: then the text goes nowhere.
    if (!this.isConnected || !this._box || this.#writing || !acceptsDraft(this.mode)) return;

    if (!read.ok) {
      this._fileProblem = read.problem;
      return;
    }

    this._fileProblem = null;
    this._box.value = read.text;
    this.onDraft(read.text);
  };

  // "Key removed" is said in a polite line that is in the page whatever the mode, so it is announced when it is filled: after a removal the mode
  // is a different one.
  override render() {
    return html`
      ${this.#body()}
      <span class="status-line" role="status" aria-live="polite">
        ${this.removal.done ? html`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${text(this, 'uiKeyRemovedText')}` : nothing}
      </span>
    `;
  }

  #body() {
    switch (this.mode) {
      case 'configured':
        return html`
          <p class="status-line"><uui-icon name="icon-lock" aria-hidden="true"></uui-icon><strong>${text(this, 'uiKeyConfiguredText', { email: this.email })}</strong></p>
          <p class="help muted">${text(this, 'uiKeyConfiguredHelpText')}</p>
        `;
      case 'configuredBroken':
        return this.#banner(text(this, 'uiKeyConfiguredBrokenTitle'), text(this, 'uiKeyConfiguredBrokenText'));
      case 'saved':
        return html`${this.#saved()} ${this.#removal()}`;
      case 'replacing':
        return html`
          ${this.#savedLine()}
          <p class="help muted">${text(this, 'uiKeyReplaceHelpText')}</p>
          ${this.#box()}
          <div class="buttons">
            <button type="button" class="secondary" id="key-cancel" aria-disabled=${this.#writing ? 'true' : 'false'} @click=${this.#act('cancel-replace', 'replace')}>
              ${text(this, 'uiCancelLabel')}
            </button>
          </div>
        `;
      case 'unreadable': {
        const { title, text: body } = errorTexts(this, 'keyUnreadable');
        return html`${this.#banner(title, body)} ${this.#box()} ${this.#removal()}`;
      }
      default:
        return this.#box();
    }
  }

  #banner(title: string, body: string) {
    return html`
      <div class="banner tone-warning">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
        <div><h4>${title}</h4><p>${body}</p></div>
      </div>
    `;
  }

  #savedLine() {
    return html`<p class="status-line"><uui-icon name="icon-key" aria-hidden="true"></uui-icon><strong>${text(this, 'uiKeySavedText', { email: this.email })}</strong></p>`;
  }

  #saved() {
    const busy = this.#writing;
    return html`
      ${this.#savedLine()}
      <div class="buttons">
        <button type="button" class="secondary" id="key-replace" aria-disabled=${busy ? 'true' : 'false'} @click=${this.#act('replace', 'box')}>
          ${text(this, 'uiKeyReplaceLabel')}
        </button>
        ${this.confirmingRemove ? nothing : this.#removeButton()}
      </div>
    `;
  }

  #removeButton() {
    const busy = this.#writing;
    return html`<button type="button" class="secondary" id="key-remove" aria-disabled=${busy ? 'true' : 'false'} @click=${this.#act('ask-remove', 'confirm')}>
      ${text(this, 'uiKeyRemoveLabel')}
    </button>`;
  }

  // Remove, its confirmation, and a removal that failed.
  #removal() {
    const { busy, problem } = this.removal;

    return html`
      ${this.mode === 'unreadable' && !this.confirmingRemove ? html`<div class="buttons">${this.#removeButton()}</div>` : nothing}
      ${this.confirmingRemove
        ? html`<div class="confirm" role="group" aria-labelledby="key-remove-question">
            <p id="key-remove-question">${text(this, 'uiKeyRemoveConfirmText')}</p>
            <div class="buttons">
              <button
                type="button"
                class="secondary danger"
                id="key-confirm"
                aria-disabled=${busy || this.busy ? 'true' : 'false'}
                aria-busy=${busy ? 'true' : 'false'}
                @click=${this.#act('confirm-remove', 'afterRemove')}
              >
                ${busy ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : nothing}${text(this, 'uiKeyRemoveConfirmLabel')}
              </button>
              <button type="button" class="secondary" id="key-cancel-remove" aria-disabled=${busy ? 'true' : 'false'} @click=${this.#act('cancel-remove', 'remove')}>
                ${text(this, 'uiCancelLabel')}
              </button>
            </div>
          </div>`
        : nothing}
      ${problem
        ? html`<esatto-seo-report-problem .error=${problem} @retry=${() => this.dispatchEvent(new CustomEvent('retry-remove'))}></esatto-seo-report-problem>`
        : nothing}
    `;
  }

  // The box, the file chooser, and the early word on what is in the box. The early word is a polite status, not an alert: it is a warning while
  // the person pastes (the server decides), and it is always in the page so a change of it is announced.
  #box() {
    const problemKey = this.draftProblem ? PROBLEM_KEYS[this.draftProblem] : null;
    const fileProblem = this._fileProblem === 'tooLarge' ? 'uiKeyTooLargeText' : this._fileProblem === 'unreadable' ? 'uiKeyFileUnreadableText' : null;
    const warning = fileProblem ?? problemKey;

    return html`
      <div class="editor">
        <label for="key">${text(this, 'uiKeyPasteLabel')}</label>
        <textarea
          id="key"
          rows="6"
          autocomplete="off"
          autocapitalize="off"
          autocorrect="off"
          spellcheck="false"
          aria-describedby="key-help key-check"
          aria-invalid=${warning ? 'true' : 'false'}
          ?readonly=${this.#writing}
          @input=${this.#input}
        ></textarea>
        <div class="buttons">
          <button type="button" class="secondary" id="key-choose" aria-disabled=${this.#writing ? 'true' : 'false'} @click=${this.#chooseFile}>
            <uui-icon name="icon-document" aria-hidden="true"></uui-icon>${text(this, 'uiKeyChooseFileLabel')}
          </button>
          <input type="file" id="key-file" class="visually-hidden" accept=".json,application/json" tabindex="-1" aria-hidden="true" @change=${this.#fileChosen} />
        </div>
        <p class="help muted" id="key-help">${text(this, 'uiKeyHelpText')}</p>
        <div id="key-check" role="status" aria-live="polite">
          ${warning
            ? html`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, warning, { max: MAX_KEY_BYTES / 1024 })}</p>
                <p class="help muted">${text(this, 'uiKeyCheckHintText')}</p>`
            : this.hasDraft
              ? html`<p class="status-line"><uui-icon name="icon-check" aria-hidden="true"></uui-icon>${text(this, 'uiKeyDraftText', { email: this.draftEmail })}</p>`
              : nothing}
        </div>
      </div>
    `;
  }

  static override styles = [
    shared,
    editorStyles,
    settingsStyles,
    css`
      :host { display: grid; gap: var(--uui-size-space-3); }
      h4 { font-size: 14px; font-weight: 700; }
      .confirm {
        display: grid; gap: var(--uui-size-space-3); max-width: 70ch; padding: var(--uui-size-space-4);
        border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm); background: var(--seo-wash);
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-key-field': EsattoSeoReportKeyFieldElement;
  }
}
