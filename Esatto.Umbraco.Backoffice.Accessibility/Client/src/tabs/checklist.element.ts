// Manual checklist: the criteria of the target a machine can't fully check. Each has plain "how to
// test" steps and Pass / Fail / Not applicable buttons; results are saved with who and when.

import { css, customElement, html, nothing, property, repeat, state, styleMap } from '@umbraco-cms/backoffice/external/lit';
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

/** A recorded result as the card's edge colour. */
const STATUS_TONE: Record<ManualStatus, string> = { pass: 'tone-good', fail: 'tone-critical', na: 'tone-none' };

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
      <div class="page">
        <section class="card overview" aria-label="Manual checklist">
          <div class="progress-head">
            <span class="icon-badge tone-good" aria-hidden="true"><uui-icon name="icon-checkbox"></uui-icon></span>
            <div class="grow">
              <p class="summary" tabindex="-1"><strong>${checked} of ${items.length}</strong> checked</p>
              <div class="progress-bar" aria-hidden="true"><span style=${styleMap({ width: `${items.length ? Math.round((checked / items.length) * 100) : 0}%` })}></span></div>
            </div>
          </div>
          <fieldset class="views">
            <legend class="visually-hidden">Show</legend>
            ${VIEWS.map((v) => html`
              <label class="view"><input type="radio" name="view" .checked=${this._view === v.id} @change=${() => (this._view = v.id)}>
                ${v.label} <span class="count">${items.filter(v.test).length}</span></label>`)}
          </fieldset>
          <p class="muted">These criteria need a person to judge them. Follow the steps, then record the result. “Checked by the scanner” means its automated rules found nothing wrong, so those are usually quick to confirm. Results become “needs re-check” after the interval set in Settings.</p>
        </section>
        <p role="status" class="visually-hidden">${this._saved ?? ''}</p>
        ${visible.length
          ? html`<ul class="items">${repeat(visible, (i) => i.criterion.id, (i) => this.#item(i))}</ul>`
          : html`<div class="card empty"><span class="icon-badge solid round tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span><p>Nothing here.</p></div>`}
      </div>`;
  }

  #item(i: ChecklistItem) {
    const c = i.criterion;
    const unsaved = c.id in this._drafts;
    const hintId = `${headingId(i)}-note-hint`;
    return html`
      <li>
        <article class="card item ${i.status ? STATUS_TONE[i.status] : ''}">
          <div class="title-row">
            <h3 id=${headingId(i)} tabindex="-1">${c.id} ${c.title}</h3>
            <span class="tag">Level ${c.level}</span>
            ${c.since !== 'V20' ? html`<span class="tag tone-notice">New in ${versionLabel(c.since)}</span>` : nothing}
            ${i.needsRecheck ? html`<span class="tag tone-warning">Needs re-check</span>` : nothing}
            ${i.scannerChecked ? html`<span class="chip"><uui-icon name="icon-wand" aria-hidden="true"></uui-icon>Checked by the scanner</span>` : nothing}
          </div>
          <p><strong>How to test:</strong> ${c.howToTest}</p>
          ${i.automatedFindings ? html`<p>
            <a href="#" @click=${(e: Event) => { e.preventDefault(); navigate(this, 'issues', { criterion: c.id }); }}>
              The scanner found ${i.automatedFindings} ${i.automatedFindings === 1 ? 'thing' : 'things'} to look at</a></p>` : nothing}
          ${i.documentLinks ? html`<p class="muted">This site links ${i.documentLinks} ${i.documentLinks === 1 ? 'document' : 'documents'} (PDF/Office); they must be checked separately.</p>` : nothing}
          <details class="disclosure">
            <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>Why it matters and how to fix</summary>
            <div class="why">
              <p>${c.why}</p><p>${c.howToFix}</p><p class="muted">${c.caveats}</p>
              <p><a href=${c.understandingUrl} target="_blank" rel="noopener">Understanding ${c.id} (W3C)<span class="visually-hidden"> (opens in a new tab)</span></a></p>
            </div>
          </details>
          <div class="row" role="group" aria-labelledby=${headingId(i)}>
            ${STATUSES.map((s) => html`
              <button class="status ${s.id}" aria-pressed=${i.status === s.id ? 'true' : 'false'}
                @click=${() => this.#save(i, s.id, unsaved ? this._drafts[c.id] || null : i.note)}>${i.status === s.id ? html`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>` : nothing}${s.label}</button>`)}
            ${i.status ? html`<button class="clear" @click=${() => this.#clear(i)}>Clear result</button>` : nothing}
            ${i.updatedUtc ? html`<span class="muted small">${i.userName}, ${formatDate(i.updatedUtc, false)}</span>` : nothing}
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
          ${unsaved ? html`
            <div id=${hintId} class="banner compact tone-warning unsaved">
              <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
              <div><p>${i.status ? 'Not saved yet. It saves when you leave the note field.' : 'Not saved yet. Choose Pass, Fail or Not applicable to save it with the note.'}</p></div>
            </div>` : nothing}
        </article>
      </li>`;
  }

  static override styles = [shared, css`
    .overview { display: grid; gap: var(--uui-size-space-4); }
    .progress-head { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    .progress-head .grow { flex: 1; display: grid; gap: var(--uui-size-space-2); max-width: 32rem; }
    .summary { font-size: 16px; }
    .summary strong { font-size: 22px; font-weight: 800; letter-spacing: -0.01em; font-variant-numeric: tabular-nums; }
    h3:focus, .summary:focus { outline: none; }
    h3:focus-visible, .summary:focus-visible { outline: 2px solid var(--uui-color-focus); }

    .views { border: 0; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
    .view {
      display: inline-flex; align-items: center; gap: 8px; min-height: 40px; padding: 0 var(--uui-size-space-3) 0 var(--uui-size-space-3);
      border: 1px solid var(--a11y-line); border-radius: 999px; background: var(--uui-color-surface); cursor: pointer; font-weight: 600;
      transition: border-color var(--a11y-ease), background-color var(--a11y-ease);
    }
    .view:hover { border-color: var(--uui-color-border-emphasis); }
    .view:has(input:checked) { border-color: var(--uui-color-selected); background: color-mix(in srgb, var(--uui-color-selected) 10%, var(--uui-color-surface)); }
    .view:has(input:focus-visible) { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    .view input { width: 16px; height: 16px; margin: 0; }
    .count { min-width: 1.5em; padding: 0 6px; border-radius: 999px; background: var(--a11y-wash); border: 1px solid var(--a11y-line); font-size: 12px; text-align: center; font-variant-numeric: tabular-nums; }

    .empty { display: flex; gap: var(--uui-size-space-3); align-items: center; }
    .items { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
    /* The recorded result as the card's edge: green pass, red fail, grey not applicable; none while unchecked. */
    .item { display: grid; gap: var(--uui-size-space-3); box-shadow: inset 4px 0 0 var(--tone, transparent), var(--a11y-shadow); }
    .title-row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); align-items: center; }
    .title-row h3 { font-size: 16px; font-weight: 700; margin-right: var(--uui-size-space-2); }
    .why { display: grid; gap: var(--uui-size-space-2); margin-top: var(--uui-size-space-3); max-width: 60rem; padding: var(--uui-size-space-4); background: var(--a11y-wash); border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius-sm); }

    button.status {
      display: inline-flex; gap: 6px; align-items: center; min-height: 44px; min-width: 44px; padding: 0 var(--uui-size-space-5);
      font: inherit; font-weight: 600; cursor: pointer; border: 1px solid var(--uui-color-border-standalone); border-radius: 999px;
      background: var(--uui-color-surface); color: var(--uui-color-text);
      transition: background-color var(--a11y-ease), border-color var(--a11y-ease), box-shadow var(--a11y-ease);
    }
    button.status.pass { --tone: var(--a11y-good); --tone-on: var(--a11y-good-on); }
    button.status.fail { --tone: var(--a11y-critical); --tone-on: var(--a11y-critical-on); }
    button.status.na { --tone: var(--a11y-neutral); --tone-on: var(--a11y-neutral-on); }
    button.status:hover { border-color: var(--tone); background: color-mix(in srgb, var(--tone) 8%, var(--uui-color-surface)); }
    button.status[aria-pressed='true'] { background: var(--tone); color: var(--tone-on); border-color: var(--tone); box-shadow: 0 4px 12px -6px var(--tone); }
    button.clear {
      min-height: 44px; padding: 0 var(--uui-size-space-3); font: inherit; cursor: pointer; background: none; border: 0;
      color: var(--uui-color-interactive); text-decoration: underline; text-underline-offset: 2px;
    }
    button.clear:hover { color: var(--uui-color-interactive-emphasis); }
    .confirmed { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; }
    .confirmed uui-icon { color: var(--a11y-good); }
    .note { display: grid; gap: 6px; max-width: 40rem; }
    .note > span { font-size: 13px; font-weight: 600; }
    .unsaved { max-width: 40rem; }
  `];
}
declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-checklist': EsattoA11yChecklistElement;
  }
}
