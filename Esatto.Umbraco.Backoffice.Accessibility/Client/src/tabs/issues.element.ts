// Issues tab: every axe rule that failed (or needs review), grouped across pages, with filters and CSV
// export. Selecting a row opens the detail (issue-detail.element.ts) in place.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { getIssues, getIssuesCsv } from '../api';
import { shared } from '../dashboard.styles';
import { levelLabel } from '../format';
import type { IssueFilter, IssueGroup, Owner, SettingsResponse, WcagLevel } from '../types';
import './issue-detail.element';

const PRINCIPLES = ['Perceivable', 'Operable', 'Understandable', 'Robust'];
const IMPACTS = ['critical', 'serious', 'moderate', 'minor'];

const groupId = (g: IssueGroup) => `${g.ruleId}|${g.kind}`;

@customElement('esatto-a11y-issues')
export class EsattoA11yIssuesElement extends UmbLitElement {
  @property({ attribute: false }) siteKey = '';
  @property({ attribute: false }) filter: IssueFilter = {};
  @property({ attribute: false }) settings: SettingsResponse | null = null;

  @state() private _filter: IssueFilter = {};
  @state() private _groups: IssueGroup[] | null = null;
  @state() private _selected: string | null = null;
  /** The open group with its elements (the list itself carries counts only). */
  @state() private _detail: IssueGroup | null = null;
  @state() private _error: string | null = null;

  #returnFocusTo: string | null = null;

  override willUpdate(changed: Map<string, unknown>) {
    if (changed.has('filter')) this._filter = { ...this.filter };
  }

  override updated(changed: Map<string, unknown>) {
    if ((changed.has('siteKey') || changed.has('filter')) && this.siteKey) this.#load();
    if (changed.has('_selected') && this._selected === null && this.#returnFocusTo) {
      this.renderRoot.querySelector<HTMLElement>(`[data-group="${CSS.escape(this.#returnFocusTo)}"]`)?.focus();
      this.#returnFocusTo = null;
    }
  }

  async #load() {
    const { data, error } = await tryExecute(this, getIssues(this.siteKey, this._filter), { disableNotifications: true });
    if (error || !data) {
      this._error = 'Could not load issues.';
      return;
    }
    this._error = null;
    this._groups = data;
    // The open issue can vanish (fixed, re-checked, or marked as a false positive): back to the list.
    if (this._selected && !data.some((g) => groupId(g) === this._selected)) this._selected = null;
    if (this._selected) await this.#loadDetail();
  }

  async #loadDetail() {
    const [ruleId, kind] = this._selected!.split('|') as [string, 'violation' | 'incomplete'];
    const { data } = await tryExecute(this, getIssues(this.siteKey, { ...this._filter, ruleId, kind }), { disableNotifications: true });
    this._detail = data?.[0] ?? null;
    if (!this._detail) this._selected = null;
  }

  #setFilter(patch: Partial<IssueFilter>) {
    this._filter = { ...this._filter, ...patch };
    this._selected = null;
    this.#load();
  }

  async #exportCsv() {
    const { data, error } = await tryExecute(this, getIssuesCsv(this.siteKey, this._filter));
    if (error || !data) return;
    const url = URL.createObjectURL(data as Blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `accessibility-issues-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  async #open(g: IssueGroup) {
    this._selected = groupId(g);
    this._detail = null;
    await this.#loadDetail();
  }

  #back() {
    this.#returnFocusTo = this._selected;
    this._selected = null;
    this._detail = null;
  }

  override render() {
    if (this._error) return html`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (this._selected && !this._detail) return html`<uui-loader></uui-loader>`;
    const selected = this._detail;
    if (this._selected && selected) {
      return html`<esatto-a11y-issue-detail .group=${selected} .siteKey=${this.siteKey} .settings=${this.settings}
        @back=${this.#back} @changed=${() => this.#load()}></esatto-a11y-issue-detail>`;
    }
    return html`
      <div class="stack">
        ${this.#filters()}
        ${this._groups === null ? html`<uui-loader></uui-loader>` : this.#list(this._groups)}
      </div>`;
  }

  #filters() {
    const f = this._filter;
    const select = (id: string, label: string, value: string | undefined, options: [string, string][], onChange: (v: string) => void) => html`
      <div class="field">
        <label for=${id}>${label}</label>
        <select id=${id} @change=${(e: Event) => onChange((e.target as HTMLSelectElement).value)}>
          <option value="" ?selected=${!value}>All</option>
          ${options.map(([v, text]) => html`<option value=${v} ?selected=${value === v}>${text}</option>`)}
        </select>
      </div>`;
    return html`
      <form class="filters" @submit=${(e: Event) => e.preventDefault()} aria-label="Filter issues">
        ${select('f-principle', 'Principle', f.principle ? String(f.principle) : undefined,
          PRINCIPLES.map((p, i) => [String(i + 1), `${i + 1}. ${p}`]), (v) => this.#setFilter({ principle: v ? Number(v) : undefined }))}
        ${select('f-level', 'Level', f.level, [['A', 'A'], ['AA', 'AA'], ['AAA', 'AAA']], (v) => this.#setFilter({ level: (v || undefined) as WcagLevel | undefined }))}
        ${select('f-owner', 'Who fixes it', f.owner, [['Editor', 'Editors'], ['Developer', 'Developers'], ['Both', 'Both']], (v) => this.#setFilter({ owner: (v || undefined) as Owner | undefined }))}
        ${select('f-impact', 'Impact', f.impact, IMPACTS.map((i) => [i, i]), (v) => this.#setFilter({ impact: v || undefined }))}
        <div class="field checkbox">
          <input id="f-fp" type="checkbox" ?checked=${!!f.falsePositives} @change=${(e: Event) => this.#setFilter({ falsePositives: (e.target as HTMLInputElement).checked })}>
          <label for="f-fp">Show false positives only</label>
        </div>
        ${f.criterion ? html`<div class="field"><span class="tag">Criterion ${f.criterion}</span>
          <uui-button compact look="secondary" label="Clear criterion filter" @click=${() => this.#setFilter({ criterion: undefined })}></uui-button></div>` : nothing}
        <uui-button look="secondary" label="Export CSV" @click=${this.#exportCsv}></uui-button>
      </form>`;
  }

  #list(groups: IssueGroup[]) {
    if (!groups.length) {
      return html`<uui-box><p>${this._filter.falsePositives ? 'No false positives marked.' : 'No issues match. Either the site passes the automated checks for these filters, or it hasn\'t been scanned yet.'}</p></uui-box>`;
    }
    return html`
      <p class="muted" role="status">${groups.length} ${groups.length === 1 ? 'issue' : 'issues'}</p>
      <table>
        <caption class="visually-hidden">Issues, most severe first</caption>
        <thead><tr>
          <th scope="col">Issue</th><th scope="col">Criteria</th><th scope="col">Level</th>
          <th scope="col" class="num">Pages</th><th scope="col" class="num">Elements</th><th scope="col">Who fixes</th><th scope="col">Impact</th>
        </tr></thead>
        <tbody>
          ${groups.map((g) => html`<tr>
            <td>
              <button class="link" data-group=${groupId(g)} @click=${() => this.#open(g)}>${g.title}</button>
              ${g.kind === 'incomplete' ? html` <span class="tag">Needs review</span>` : nothing}
              ${g.bestPractice ? html` <span class="tag">Best practice</span>` : nothing}
            </td>
            <td>${g.criteria.join(', ') || '–'}</td>
            <td>${levelLabel(g.level)}</td>
            <td class="num">${g.pageCount}</td>
            <td class="num">${g.elementCount}</td>
            <td>${g.owner}</td>
            <td>${g.impact ?? '–'}</td>
          </tr>`)}
        </tbody>
      </table>`;
  }

  static override styles = [shared, css`
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: end; }
    .field { display: grid; gap: 4px; }
    .field.checkbox { display: flex; align-items: center; gap: var(--uui-size-space-2); min-height: 36px; } /* = the selects, so they share a centre line */
    .field.checkbox input { width: 20px; height: 20px; }
    .filters > uui-button { height: 36px; }
    select { min-height: 36px; padding: 0 var(--uui-size-space-3); border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); font: inherit; }
    button.link { background: none; border: 0; padding: 4px 0; font: inherit; color: var(--uui-color-interactive); text-decoration: underline; cursor: pointer; text-align: left; min-height: 24px; }
    button.link:hover { color: var(--uui-color-interactive-emphasis); }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-issues': EsattoA11yIssuesElement;
  }
}
