// Manual checklist: the criteria of the target a machine can't fully check. Each has plain "how to
// test" steps and Pass / Fail / Not applicable buttons; results are saved with who and when.

import { css, customElement, html, nothing, property, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { clearChecklist, getChecklist, setChecklist } from '../api';
import { shared } from '../dashboard.styles';
import { formatDate, versionLabel } from '../format';
import { navigate } from '../navigate';
import type { ChecklistItem, ManualStatus } from '../types';

type View = 'all' | 'unchecked' | 'scanner' | 'failed' | 'recheck';

const VIEWS: { id: View; label: string; test: (i: ChecklistItem) => boolean }[] = [
  { id: 'all', label: 'All', test: () => true },
  { id: 'unchecked', label: 'Not checked', test: (i) => !i.status },
  { id: 'scanner', label: 'Not checked, scanner found nothing', test: (i) => !i.status && i.scannerChecked },
  { id: 'failed', label: 'Failed', test: (i) => i.status === 'fail' },
  { id: 'recheck', label: 'Needs re-check', test: (i) => i.needsRecheck },
];

/** The note's column length (ManualService.MaxNoteLength). */
const NOTE_MAX = 2048;

const headingId = (i: ChecklistItem) => `c-${i.criterion.id.split('.').join('-')}`;

const STATUSES: { id: ManualStatus; label: string }[] = [
  { id: 'pass', label: 'Pass' },
  { id: 'fail', label: 'Fail' },
  { id: 'na', label: 'Not applicable' },
];

@customElement('esatto-a11y-checklist')
export class EsattoA11yChecklistElement extends UmbLitElement {
  @property({ attribute: false }) siteKey = '';

  @state() private _items: ChecklistItem[] | null = null;
  @state() private _view: View = 'all';
  @state() private _error: string | null = null;
  @state() private _saved: string | null = null;
  /** The last item changed, with a visible confirmation ("Saved"), shown beside its buttons until the next change. */
  @state() private _confirmed: { id: string; text: string } | null = null;
  /** Notes typed but not saved yet, by criterion id (only while they differ from the saved note). */
  @state() private _drafts: Record<string, string> = {};

  override updated(changed: Map<string, unknown>) {
    if (changed.has('siteKey') && this.siteKey) this.#load();
  }

  async #load() {
    const { data, error } = await tryExecute(this, getChecklist(this.siteKey), { disableNotifications: true });
    if (error || !data) {
      this._error = 'Could not load the checklist.';
      return;
    }
    this._error = null;
    this._items = data;
  }

  async #save(item: ChecklistItem, status: ManualStatus | null, note: string | null) {
    if (!status) return; // a note is saved together with a result
    const { error } = await tryExecute(this, setChecklist(this.siteKey, item.criterion.id, status, note));
    if (error) return;
    this.#dropDraft(item.criterion.id);
    this._confirmed = { id: item.criterion.id, text: 'Saved' };
    await this.#reload(item, `${item.criterion.id} saved as ${STATUSES.find((s) => s.id === status)!.label}.`);
  }

  async #clear(item: ChecklistItem) {
    const { error } = await tryExecute(this, clearChecklist(this.siteKey, item.criterion.id));
    if (error) return;
    this._confirmed = { id: item.criterion.id, text: 'Cleared' };
    await this.#reload(item, `${item.criterion.id} cleared; it's not checked any more.`);
  }

  #setDraft(item: ChecklistItem, value: string) {
    const text = value.trim();
    if (text === (item.note ?? '')) this.#dropDraft(item.criterion.id);
    else this._drafts = { ...this._drafts, [item.criterion.id]: text };
  }

  #dropDraft(id: string) {
    if (!(id in this._drafts)) return;
    const { [id]: _, ...rest } = this._drafts;
    this._drafts = rest;
  }

  #visible(): ChecklistItem[] {
    return (this._items ?? []).filter(VIEWS.find((v) => v.id === this._view)!.test);
  }

  /**
   * Reloads after a change. In a filtered view the item can drop out of the list (a "Not checked" item that
   * was just checked), taking the focused button with it: focus then moves to the item that took its place,
   * so keyboard and screen reader users keep their position instead of landing at the top of the page.
   */
  async #reload(item: ChecklistItem, message: string) {
    const index = this.#visible().findIndex((i) => i.criterion.id === item.criterion.id);
    this._saved = message;
    await this.#load();
    await this.updateComplete;
    if ((this.renderRoot as ShadowRoot).activeElement) return; // the pressed button is still there
    const visible = this.#visible();
    const same = visible.find((i) => i.criterion.id === item.criterion.id);
    const next = same ?? visible[Math.min(index, visible.length - 1)];
    const target = same
      ? this.renderRoot.querySelector<HTMLElement>(`[aria-labelledby="${headingId(same)}"] button`)
      : next ? this.renderRoot.querySelector<HTMLElement>(`#${headingId(next)}`) : this.renderRoot.querySelector<HTMLElement>('.summary');
    target?.focus();
  }

  override render() {
    if (this._error) return html`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._items) return html`<uui-loader></uui-loader>`;
    const items = this._items;
    const checked = items.filter((i) => i.status).length;
    const visible = this.#visible();
    return html`
      <div class="stack">
        <div class="row header">
          <p class="summary" tabindex="-1"><strong>${checked} of ${items.length}</strong> checked</p>
          <fieldset class="views">
            <legend class="visually-hidden">Show</legend>
            ${VIEWS.map((v) => html`
              <label><input type="radio" name="view" .checked=${this._view === v.id} @change=${() => (this._view = v.id)}>
                ${v.label} <span class="muted">(${items.filter(v.test).length})</span></label>`)}
          </fieldset>
        </div>
        <p class="muted">These criteria need a person to judge them. Follow the steps, then record the result. “Checked by the scanner” means its automated rules found nothing wrong, so those are usually quick to confirm. Results become “needs re-check” after the interval set in Settings.</p>
        <p role="status" class="visually-hidden">${this._saved ?? ''}</p>
        ${visible.length ? html`<ul class="items">${repeat(visible, (i) => i.criterion.id, (i) => this.#item(i))}</ul>` : html`<uui-box><p>Nothing here.</p></uui-box>`}
      </div>`;
  }

  #item(i: ChecklistItem) {
    const c = i.criterion;
    const unsaved = c.id in this._drafts;
    const hintId = `${headingId(i)}-note-hint`;
    return html`
      <li>
        <uui-box>
          <div class="item">
            <div class="title-row">
              <h3 id=${headingId(i)} tabindex="-1">${c.id} ${c.title}</h3>
              <span class="tag">Level ${c.level}</span>
              ${c.since !== 'V20' ? html`<span class="tag">New in ${versionLabel(c.since)}</span>` : nothing}
              ${i.needsRecheck ? html`<span class="tag warn">Needs re-check</span>` : nothing}
              ${i.scannerChecked ? html`<span class="tag">Checked by the scanner</span>` : nothing}
            </div>
            <p><strong>How to test:</strong> ${c.howToTest}</p>
            ${i.automatedFindings ? html`<p>
              <a href="#" @click=${(e: Event) => { e.preventDefault(); navigate(this, 'issues', { criterion: c.id }); }}>
                The scanner found ${i.automatedFindings} ${i.automatedFindings === 1 ? 'thing' : 'things'} to look at</a></p>` : nothing}
            ${i.documentLinks ? html`<p class="muted">This site links ${i.documentLinks} ${i.documentLinks === 1 ? 'document' : 'documents'} (PDF/Office); they must be checked separately.</p>` : nothing}
            <details>
              <summary>Why it matters and how to fix</summary>
              <p>${c.why}</p><p>${c.howToFix}</p><p class="muted">${c.caveats}</p>
              <p><a href=${c.understandingUrl} target="_blank" rel="noopener">Understanding ${c.id} (W3C)<span class="visually-hidden"> (opens in a new tab)</span></a></p>
            </details>
            <div class="row" role="group" aria-labelledby=${headingId(i)}>
              ${STATUSES.map((s) => html`
                <button class="status ${s.id}" aria-pressed=${i.status === s.id ? 'true' : 'false'}
                  @click=${() => this.#save(i, s.id, unsaved ? this._drafts[c.id] || null : i.note)}>${s.label}</button>`)}
              ${i.status ? html`<button class="clear" @click=${() => this.#clear(i)}>Clear result</button>` : nothing}
              ${i.updatedUtc ? html`<span class="muted">${i.userName}, ${formatDate(i.updatedUtc, false)}</span>` : nothing}
              ${this._confirmed?.id === c.id
                ? html`<span class="confirmed" aria-hidden="true"><uui-icon name="icon-check"></uui-icon> ${this._confirmed.text}</span>`
                : nothing}
            </div>
            <label class="note">
              <span>Note${i.status ? '' : ' (saved with a result)'}</span>
              <textarea rows="2" maxlength=${NOTE_MAX} .value=${i.note ?? ''} aria-describedby=${unsaved ? hintId : nothing}
                @input=${(e: Event) => this.#setDraft(i, (e.target as HTMLTextAreaElement).value)}
                @blur=${() => { if (i.status && unsaved) this.#save(i, i.status, this._drafts[c.id] || null); }}></textarea>
            </label>
            ${unsaved ? html`<p id=${hintId} class="unsaved"><uui-icon name="icon-alert"></uui-icon>
              ${i.status ? 'Not saved yet. It saves when you leave the note field.' : 'Not saved yet. Choose Pass, Fail or Not applicable to save it with the note.'}</p>` : nothing}
          </div>
        </uui-box>
      </li>`;
  }

  static override styles = [shared, css`
    .header { justify-content: space-between; }
    .header p { margin: 0; }
    .views { border: 0; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); }
    .views label { display: flex; align-items: center; gap: 6px; min-height: 44px; cursor: pointer; }
    .views input { width: 18px; height: 18px; }
    .items { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); }
    .item { display: grid; gap: var(--uui-size-space-3); }
    .item p { margin: 0; }
    .title-row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
    .title-row h3 { font-size: 1rem; }
    h3:focus, .summary:focus { outline: none; }
    h3:focus-visible, .summary:focus-visible { outline: 2px solid var(--uui-color-focus); }
    button.clear { min-height: 44px; padding: 0 var(--uui-size-space-3); font: inherit; cursor: pointer; background: none; border: 0; color: var(--uui-color-interactive); text-decoration: underline; }
    button.clear:hover { color: var(--uui-color-interactive-emphasis); }
    .tag.warn { border-color: var(--a11y-warning); }
    details p { margin: var(--uui-size-space-2) 0 0; }
    button.status {
      min-height: 44px; min-width: 44px; padding: 0 var(--uui-size-space-5); font: inherit; cursor: pointer;
      border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    button.status[aria-pressed='true'] { font-weight: 600; border-width: 2px; }
    button.status.pass[aria-pressed='true'] { border-color: var(--a11y-good); }
    button.status.fail[aria-pressed='true'] { border-color: var(--a11y-critical); }
    button.status.na[aria-pressed='true'] { border-color: var(--uui-color-border-emphasis); }
    button.status[aria-pressed='true']::before { content: '✓ '; }
    .confirmed { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; }
    .confirmed uui-icon { color: var(--a11y-good); }
    .unsaved { display: flex; align-items: center; gap: var(--uui-size-space-2); max-width: 40rem; padding: var(--uui-size-space-2) var(--uui-size-space-3); border-left: 4px solid var(--a11y-warning); background: var(--uui-color-surface-alt); }
    .unsaved uui-icon { flex: none; }
    .note { display: grid; gap: 4px; max-width: 40rem; }
    textarea { font: inherit; padding: var(--uui-size-space-2); border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-checklist': EsattoA11yChecklistElement;
  }
}
