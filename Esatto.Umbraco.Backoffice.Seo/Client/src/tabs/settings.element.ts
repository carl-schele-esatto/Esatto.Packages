// Settings tab (users with the Settings section only): nightly schedule, what to scan and how hard.
// Anything set in appsettings (Esatto:Seo) wins and is shown read-only.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { putSettings } from '../api';
import { shared } from '../dashboard.styles';
import type { SeoSettings, SettingsResponse } from '../types';

const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v));
const HOURS = Array.from({ length: 24 }, (_, h) => h);

@customElement('esatto-seo-health-settings')
export class EsattoSeoSettingsElement extends UmbLitElement {
  @property({ attribute: false }) settings: SettingsResponse | null = null;

  @state() private _draft: SeoSettings | null = null;
  @state() private _saving = false;

  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this.#notifications = ctx));
  }

  override willUpdate(changed: Map<string, unknown>) {
    if (changed.has('settings') && this.settings) this._draft = clone(this.settings.settings);
  }

  #locked(path: string): boolean {
    return (this.settings?.locked ?? []).some((l) => l.toLowerCase() === path.toLowerCase());
  }

  #update(fn: (d: SeoSettings) => void) {
    const next = clone(this._draft!);
    fn(next);
    this._draft = next;
  }

  async #save(e: Event) {
    e.preventDefault();
    if (!this._draft) return;
    this._saving = true;
    const { data, error } = await tryExecute(this, putSettings(this._draft));
    this._saving = false;
    if (error || !data) return;
    this.#notifications?.peek('positive', { data: { headline: 'Settings saved', message: 'They apply from the next scan.' } });
    this.dispatchEvent(new CustomEvent<SettingsResponse>('saved', { detail: data }));
  }

  override render() {
    const d = this._draft;
    if (!d) return html`<uui-loader></uui-loader>`;
    return html`
      <form class="page" @submit=${this.#save}>
        ${this.settings?.locked.length ? html`<p class="muted">Fields marked “Set in configuration” come from appsettings (<code>Esatto:Seo</code>) and can't be changed here.</p>` : nothing}

        <div class="cards">
        <section class="card" aria-labelledby="h-nightly">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-time"></uui-icon></span><h3 id="h-nightly">Nightly scan</h3></div>
          <div class="stack">
            ${this.#check('schedule.enabled', 'Scan every night', d.schedule.enabled, (v) => this.#update((x) => (x.schedule.enabled = v)),
              'Keeps the score and trend up to date without anyone pressing Scan now.')}
            <div class="field">
              <label for="hour">Start time (server time)</label>
              <select id="hour" ?disabled=${!d.schedule.enabled || this.#locked('schedule.hour')}
                @change=${(e: Event) => this.#update((x) => (x.schedule.hour = Number((e.target as HTMLSelectElement).value)))}>
                ${HOURS.map((h) => html`<option value=${h} ?selected=${h === d.schedule.hour}>${String(h).padStart(2, '0')}:00</option>`)}
              </select>
              ${this.#lockNote('schedule.hour')}
            </div>
          </div>
        </section>

        <section class="card" aria-labelledby="h-what">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-sitemap"></uui-icon></span><h3 id="h-what">What to scan</h3></div>
          <div class="stack">
            <div class="field">
              <label for="exclude">Never scan these paths (one per line, e.g. /search)</label>
              <textarea id="exclude" rows="3" ?disabled=${this.#locked('excludePaths')} .value=${d.excludePaths.join('\n')}
                @change=${(e: Event) => this.#update((x) => (x.excludePaths = (e.target as HTMLTextAreaElement).value.split('\n').map((s) => s.trim()).filter(Boolean)))}></textarea>
              ${this.#lockNote('excludePaths')}
            </div>
            <div class="field">
              <label for="base">Base URL for pages without a domain <span class="muted">(normally empty)</span></label>
              <input id="base" type="url" placeholder="https://www.example.com" ?disabled=${this.#locked('baseUrl')} .value=${d.baseUrl ?? ''}
                aria-describedby="base-hint" @change=${(e: Event) => this.#update((x) => (x.baseUrl = (e.target as HTMLInputElement).value.trim() || null))}>
              <p id="base-hint" class="muted hint">Only needed when no domain is bound in Culture and Hostnames. Otherwise pages are fetched at their own addresses.</p>
              ${this.#lockNote('baseUrl')}
            </div>
          </div>
        </section>

        <section class="card" aria-labelledby="h-load">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-speed-gauge"></uui-icon></span><h3 id="h-load">Load on the site</h3></div>
          <div class="row">
            ${this.#number('parallelism', 'Pages fetched at the same time', d.parallelism, 1, 8, (v) => this.#update((x) => (x.parallelism = v)))}
            ${this.#number('delayMs', 'Pause between requests (ms)', d.delayMs, 0, 5000, (v) => this.#update((x) => (x.delayMs = v)))}
          </div>
        </section>

        <section class="card" aria-labelledby="h-env">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-server"></uui-icon></span><h3 id="h-env">This environment</h3></div>
          <p>${this.settings?.intentionallyHidden
            ? html`<uui-icon name="icon-lock"></uui-icon> Marked as hidden from search engines on purpose (<code>Esatto:Seo:IntentionallyHidden</code>).`
            : html`Not marked as hidden on purpose. On a Test or Dev environment that blocks search engines, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.`}</p>
        </section>
        </div>

        <div class="row">
          <uui-button type="submit" look="primary" color="positive" label="Save settings" .state=${this._saving ? 'waiting' : undefined}></uui-button>
        </div>
      </form>`;
  }

  #lockNote(path: string) {
    return this.#locked(path) ? html`<span class="lock"><uui-icon name="icon-lock"></uui-icon> Set in configuration</span>` : nothing;
  }

  #check(path: string, label: string, checked: boolean, onChange: (v: boolean) => void, hint?: string) {
    const id = `c-${path.split('.').join('-')}`;
    return html`
      <div class="check">
        <input id=${id} type="checkbox" .checked=${checked} ?disabled=${this.#locked(path)} aria-describedby=${hint ? `${id}-hint` : nothing}
          @change=${(e: Event) => onChange((e.target as HTMLInputElement).checked)}>
        <label for=${id}>${label}</label>
        ${this.#lockNote(path)}
        ${hint ? html`<p id="${id}-hint" class="muted hint">${hint}</p>` : nothing}
      </div>`;
  }

  #number(path: string, label: string, value: number, min: number, max: number, onChange: (v: number) => void) {
    return html`
      <div class="field">
        <label for="n-${path}">${label}</label>
        <input id="n-${path}" type="number" min=${min} max=${max} ?disabled=${this.#locked(path)} .value=${String(value)}
          @change=${(e: Event) => onChange(Math.min(max, Math.max(min, Number((e.target as HTMLInputElement).value) || min)))}>
        ${this.#lockNote(path)}
      </div>`;
  }

  static override styles = [shared, css`
    .cards { display: grid; gap: var(--seo-gap); grid-template-columns: repeat(auto-fit, minmax(min(100%, 26rem), 1fr)); align-items: stretch; }
    .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); align-items: center; min-height: 44px; }
    .check input { width: 20px; height: 20px; margin: 0; accent-color: var(--uui-color-selected); }
    .check > label { font-weight: 600; }
    .check .hint, .check .lock { grid-column: 2; margin: 0; }
    select, input:not([type='checkbox']), textarea { width: 100%; max-width: 32rem; }
    .hint { font-size: 13px; }
    .lock { color: var(--seo-muted); font-size: 13px; display: inline-flex; gap: 4px; align-items: center; }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-health-settings': EsattoSeoSettingsElement;
  }
}
