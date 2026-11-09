// Issues tab: every open (or ignored) finding grouped by check, most important first. Each group explains
// why it matters and how to fix it, and lists the pages with Edit in Umbraco, View page, Re-check and Ignore.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { addIgnore, getIssues, getIssuesCsv, recheck, removeIgnore } from '../api';
import { shared } from '../dashboard.styles';
import { editContentUrl, ownerLabel, plural, safeHref, severityIcon, severityLabel } from '../format';
import { Latest } from '../latest';
import type { IssueFilter, IssueGroup, IssueOccurrence, Knowledge, Owner, Severity } from '../types';

interface IgnoreTarget { checkId: string; contentKey: string | null; culture: string | null; label: string }

@customElement('esatto-seo-issues')
export class EsattoSeoIssuesElement extends UmbLitElement {
  @property({ attribute: false }) siteKey = '';
  @property({ attribute: false }) filter: IssueFilter = {};
  @property({ attribute: false }) knowledge: Knowledge | null = null;

  @state() private _filter: IssueFilter = {};
  @state() private _groups: IssueGroup[] | null = null;
  @state() private _error: string | null = null;
  @state() private _cultures: string[] = [];
  @state() private _ignoring: IgnoreTarget | null = null;
  @state() private _note = '';
  @state() private _busy = new Set<string>();
  @state() private _status = '';

  override willUpdate(changed: Map<string, unknown>) {
    if (changed.has('filter')) this._filter = { ...this.filter };
  }

  override updated(changed: Map<string, unknown>) {
    if ((changed.has('siteKey') || changed.has('filter')) && this.siteKey) this.#load();
  }

  readonly #latest = new Latest();

  async #load() {
    const siteKey = this.siteKey;
    const filter = this._filter;
    const response = await this.#latest.run(() => tryExecute(this, getIssues(siteKey, filter), { disableNotifications: true }));
    if (!response) return; // a newer filter was asked for meanwhile; its answer is the one to show
    const { data, error } = response;
    if (error || !data) {
      this._error = 'Could not load the issues.';
      return;
    }
    this._error = null;
    this._groups = data;
    const seen = new Set(this._cultures);
    data.forEach((g) => g.occurrences.forEach((o) => o.culture && seen.add(o.culture)));
    this._cultures = [...seen].sort();
  }

  #set(patch: Partial<IssueFilter>) {
    this._filter = { ...this._filter, ...patch };
    this.#load();
  }

  async #csv() {
    const { data } = await tryExecute(this, getIssuesCsv(this.siteKey, this._filter));
    if (!data) return;
    const url = URL.createObjectURL(data as Blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `seo-issues-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  async #recheck(o: IssueOccurrence) {
    if (!o.contentKey) return;
    const id = `${o.contentKey}|${o.culture}`;
    this._busy = new Set([...this._busy, id]);
    this._status = `Re-checking ${o.name}…`;
    const { error } = await tryExecute(this, recheck(this.siteKey, o.contentKey, o.culture));
    this._busy = new Set([...this._busy].filter((b) => b !== id));
    this._status = error ? `Re-check of ${o.name} failed.` : `${o.name} re-checked.`;
    if (!error) await this.#load();
  }

  async #saveIgnore(e: Event) {
    e.preventDefault();
    const t = this._ignoring;
    if (!t || !this._note.trim()) return;
    const { error } = await tryExecute(this, addIgnore(this.siteKey, { checkId: t.checkId, contentKey: t.contentKey, culture: t.culture, note: this._note.trim() }));
    if (error) return;
    this._status = `Ignored: ${t.label}.`;
    this._ignoring = null;
    this._note = '';
    await this.#load();
  }

  async #unignore(id: number) {
    const { error } = await tryExecute(this, removeIgnore(id));
    if (error) return;
    this._status = 'No longer ignored.';
    await this.#load();
  }

  override render() {
    return html`
      <div class="stack">
        ${this.#filters()}
        <p class="visually-hidden" role="status" aria-live="polite">${this._status}</p>
        ${this._error ? html`<p class="error">${this._error}</p>` : nothing}
        ${this._groups === null ? html`<uui-loader></uui-loader>` : this.#list(this._groups)}
      </div>`;
  }

  #filters() {
    const f = this._filter;
    const select = (id: string, label: string, value: string, options: [string, string][], onChange: (v: string) => void) => html`
      <div class="field">
        <label for=${id}>${label}</label>
        <select id=${id} @change=${(e: Event) => onChange((e.target as HTMLSelectElement).value)}>
          ${options.map(([v, t]) => html`<option value=${v} ?selected=${v === value}>${t}</option>`)}
        </select>
      </div>`;
    return html`
      <form class="filters" role="search" @submit=${(e: Event) => { e.preventDefault(); this.#load(); }}>
        ${select('f-sev', 'Severity', f.severity ?? '', [['', 'All'], ['critical', 'Critical'], ['warning', 'Warning'], ['notice', 'Notice']],
          (v) => this.#set({ severity: (v || undefined) as Severity | undefined }))}
        ${select('f-owner', 'Who fixes it', f.owner ?? '', [['', 'Everyone'], ['editor', 'Editors'], ['developer', 'Developers']],
          (v) => this.#set({ owner: (v || undefined) as Owner | undefined }))}
        ${this._cultures.length > 1 ? select('f-culture', 'Language', f.culture ?? '', [['', 'All'], ...this._cultures.map((c) => [c, c] as [string, string])],
          (v) => this.#set({ culture: v || undefined })) : nothing}
        ${select('f-status', 'Show', f.ignored ? 'ignored' : 'open', [['open', 'Open'], ['ignored', 'Ignored']], (v) => this.#set({ ignored: v === 'ignored' }))}
        <div class="field">
          <label for="f-search">Page or address contains</label>
          <input id="f-search" type="search" .value=${f.search ?? ''} @change=${(e: Event) => this.#set({ search: (e.target as HTMLInputElement).value })}>
        </div>
        ${f.checkId ? html`<uui-button look="secondary" label="Show all checks" @click=${() => this.#set({ checkId: undefined })}></uui-button>` : nothing}
        <uui-button look="secondary" label="Export CSV" @click=${this.#csv}></uui-button>
      </form>`;
  }

  #list(groups: IssueGroup[]) {
    if (!groups.length) {
      return html`<uui-box><p>${this._filter.ignored ? 'Nothing is ignored.' : 'No open issues match these filters.'}</p></uui-box>`;
    }
    return html`<ul class="groups">${groups.map((g) => html`<li>${this.#group(g)}</li>`)}</ul>`;
  }

  #group(g: IssueGroup) {
    const s = g.summary;
    const text = this.knowledge?.texts[s.checkId];
    const title = text?.title ?? s.checkId;
    const site = s.pages === 0;
    return html`
      <details class="group" ?open=${this._filter.checkId === s.checkId}>
        <summary>
          <span class="tag sev-${s.severity}"><uui-icon name=${severityIcon[s.severity]}></uui-icon>${severityLabel[s.severity]}</span>
          <span class="group-title">${title}</span>
          <span class="muted">${site ? 'whole site' : plural(s.pages, 'page', 'pages')} · ${ownerLabel[s.owner]}</span>
        </summary>
        <div class="group-body stack">
          ${text ? html`
            <div class="explain">
              <h4>Why it matters</h4><p>${text.why}</p>
              <h4>How to fix it</h4><p>${text.fix}</p>
              ${text.caveats ? html`<h4>Good to know</h4><p>${text.caveats}</p>` : nothing}
              ${safeHref(text.learnMore) ? html`<p><a href=${safeHref(text.learnMore)!} target="_blank" rel="noopener">Learn more<span class="visually-hidden"> about ${title} (opens in a new tab)</span></a></p>` : nothing}
            </div>` : nothing}
          ${this.#occurrences(g, title)}
          ${!this._filter.ignored ? html`<div class="row">
            <uui-button look="secondary" compact label="Ignore this check on the whole site"
              @click=${() => { this._ignoring = { checkId: s.checkId, contentKey: null, culture: null, label: `${title} (whole site)` }; this._note = ''; }}></uui-button>
          </div>` : nothing}
          ${this._ignoring?.checkId === s.checkId ? this.#ignoreForm(this._ignoring) : nothing}
        </div>
      </details>`;
  }

  #occurrences(g: IssueGroup, title: string) {
    return html`
      <table>
        <caption class="visually-hidden">Pages with: ${title}</caption>
        <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Found</th><th scope="col"><span class="visually-hidden">Actions</span></th></tr></thead>
        <tbody>
          ${g.occurrences.map((o) => {
            const href = safeHref(o.url);
            const busy = this._busy.has(`${o.contentKey}|${o.culture}`);
            return html`<tr>
              <td>
                ${o.contentKey ? html`<a href=${editContentUrl(o.contentKey, o.culture)}>${o.name}</a>` : html`<span>${o.name}</span>`}
                ${href ? html`<br><a class="muted url" href=${href} target="_blank" rel="noopener">${o.url}<span class="visually-hidden"> (opens in a new tab)</span></a>` : nothing}
              </td>
              <td>${o.culture || '–'}</td>
              <td>${o.value ? html`<pre>${o.value}</pre>` : html`<span class="muted">–</span>`}</td>
              <td class="actions">
                ${o.ignored && o.ignoreId !== null
                  ? html`<uui-button look="secondary" compact label="Stop ignoring" @click=${() => this.#unignore(o.ignoreId!)}></uui-button>`
                  : o.contentKey ? html`
                    <uui-button look="secondary" compact label="Re-check" .state=${busy ? 'waiting' : undefined} ?disabled=${busy}
                      @click=${() => this.#recheck(o)}></uui-button>
                    <uui-button look="secondary" compact label="Ignore"
                      @click=${() => { this._ignoring = { checkId: g.summary.checkId, contentKey: o.contentKey, culture: o.culture || null, label: `${title} on ${o.name}` }; this._note = ''; }}></uui-button>` : nothing}
              </td>
            </tr>`;
          })}
        </tbody>
      </table>`;
  }

  #ignoreForm(t: IgnoreTarget) {
    return html`
      <form class="ignore stack" @submit=${this.#saveIgnore}>
        <p><strong>Ignore: ${t.label}</strong></p>
        <div class="field">
          <label for="ignore-note">Why? (required, shown to others)</label>
          <textarea id="ignore-note" rows="2" required .value=${this._note} @input=${(e: Event) => (this._note = (e.target as HTMLTextAreaElement).value)}></textarea>
        </div>
        <div class="row">
          <uui-button type="submit" look="primary" label="Ignore" ?disabled=${!this._note.trim()}></uui-button>
          <uui-button look="secondary" label="Cancel" @click=${() => (this._ignoring = null)}></uui-button>
        </div>
      </form>`;
  }

  static override styles = [shared, css`
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: flex-end; }
    .field { display: grid; gap: 4px; }
    select, input, textarea {
      font: inherit; min-height: 36px; padding: 0 var(--uui-size-space-3);
      border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    textarea { padding: var(--uui-size-space-2); width: min(40rem, 100%); }
    .groups { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
    .group { background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); }
    .group > summary { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; padding: var(--uui-size-space-3) var(--uui-size-space-4); min-height: 44px; }
    .group-title { font-weight: 600; flex: 1; min-width: 12rem; }
    .group-body { padding: 0 var(--uui-size-space-4) var(--uui-size-space-4); }
    .explain { display: grid; gap: var(--uui-size-space-2); max-width: 48rem; }
    .explain h4 { margin-top: var(--uui-size-space-2); }
    .url { word-break: break-all; font-size: 0.9em; }
    .actions { white-space: nowrap; text-align: right; }
    .ignore { padding: var(--uui-size-space-4); background: var(--uui-color-surface-alt); border-radius: var(--uui-border-radius); }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-issues': EsattoSeoIssuesElement;
  }
}
