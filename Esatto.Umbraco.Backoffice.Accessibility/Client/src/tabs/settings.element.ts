// Settings tab (users with the Settings section only): target, scan options, and the visitor features
// on the public site. Anything set in appsettings (Esatto:Accessibility) wins and is shown read-only.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { putSettings } from '../api';
import { shared } from '../dashboard.styles';
import type { AccessibilitySettings, PanelSettings, SettingsResponse, WcagLevel, WcagVersion } from '../types';

/** The panel's text keys (Resources/PanelText.resx) with their English defaults, for the override table. */
export const PANEL_TEXT_KEYS: Record<string, string> = {
  ButtonLabel: 'Accessibility',
  DialogTitle: 'Accessibility preferences',
  LargerText: 'Larger text',
  Spacing: 'Bigger buttons and more spacing',
  HighContrast: 'High contrast',
  ReduceMotion: 'Reduce motion',
  ReadableFont: 'Readable font',
  HighlightLinks: 'Highlight links',
  ReadingGuide: 'Reading guide',
  Reset: 'Reset',
  Close: 'Close',
  Note: 'These settings are saved in this browser only.',
};

const PANEL_OPTIONS: { key: keyof PanelSettings; label: string }[] = [
  { key: 'largerText', label: 'Larger text' },
  { key: 'spacing', label: 'Bigger buttons and more spacing' },
  { key: 'highContrast', label: 'High contrast' },
  { key: 'reduceMotion', label: 'Reduce motion' },
  { key: 'readableFont', label: 'Readable font' },
  { key: 'highlightLinks', label: 'Highlight links' },
  { key: 'readingGuide', label: 'Reading guide' },
];

const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v));

@customElement('esatto-a11y-settings')
export class EsattoA11ySettingsElement extends UmbLitElement {
  @property({ attribute: false }) settings: SettingsResponse | null = null;

  @state() private _draft: AccessibilitySettings | null = null;
  @state() private _saving = false;
  @state() private _cultures: string[] = ['en', 'sv'];
  @state() private _newCulture = '';

  #notifications?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => (this.#notifications = ctx));
  }

  override willUpdate(changed: Map<string, unknown>) {
    if (changed.has('settings') && this.settings) {
      this._draft = clone(this.settings.settings);
      this._cultures = [...new Set(['en', 'sv', ...Object.keys(this._draft.visitor.panel.texts ?? {})])];
    }
  }

  #locked(path: string): boolean {
    const p = path.toLowerCase();
    return (this.settings?.locked ?? []).some((l) => l.toLowerCase() === p);
  }

  #update(fn: (d: AccessibilitySettings) => void) {
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
    this.#notifications?.peek('positive', { data: { headline: 'Settings saved', message: 'The public site picks them up within a minute.' } });
    this.dispatchEvent(new CustomEvent<SettingsResponse>('saved', { detail: data }));
  }

  override render() {
    const d = this._draft;
    if (!d) return html`<uui-loader></uui-loader>`;
    const panel = d.visitor.panel;
    return html`
      <form class="stack" @submit=${this.#save}>
        ${this.settings?.locked.length ? html`<p class="muted">Fields marked “Set in configuration” come from appsettings (<code>Esatto:Accessibility</code>) and can't be changed here.</p>` : nothing}

        <uui-box headline="Target">
          <div class="row">
            ${this.#select('version', 'WCAG version', d.version, [['V20', '2.0'], ['V21', '2.1'], ['V22', '2.2']], (v) => this.#update((x) => (x.version = v as WcagVersion)))}
            ${this.#select('level', 'Level', d.level, [['A', 'A'], ['AA', 'AA'], ['AAA', 'AAA']], (v) => this.#update((x) => (x.level = v as WcagLevel)))}
          </div>
          <p class="muted">The EU legal requirement today (European Accessibility Act, EN 301 549) is WCAG 2.1 AA. 2.2 AA adds six criteria, including minimum target size.</p>
          ${d.level === 'AAA' ? html`<p class="warning" role="note"><uui-icon name="icon-alert"></uui-icon> W3C does not recommend AAA as a requirement for entire sites: some AAA criteria cannot be met for all content.</p>` : nothing}
        </uui-box>

        <uui-box headline="Scan">
          <div class="stack">
            <div class="field">
              <label for="exclude">Never scan these paths (one per line, e.g. /search)</label>
              <textarea id="exclude" rows="3" ?disabled=${this.#locked('scan.excludePaths')} .value=${d.scan.excludePaths.join('\n')}
                @change=${(e: Event) => this.#update((x) => (x.scan.excludePaths = (e.target as HTMLTextAreaElement).value.split('\n').map((s) => s.trim()).filter(Boolean)))}></textarea>
              ${this.#lockNote('scan.excludePaths')}
            </div>
            ${this.#check('scan.mobileWidth', 'Also scan at mobile width (320 px)', d.scan.mobileWidth, (v) => this.#update((x) => (x.scan.mobileWidth = v)), 'Doubles the scan time.')}
            ${this.#check('scan.bestPractice', 'Include axe best-practice rules', d.scan.bestPractice, (v) => this.#update((x) => (x.scan.bestPractice = v)), 'Shown separately in Issues; never counted in the score.')}
            <div class="field">
              <label for="recheck">Ask to re-check manual results after (months)</label>
              <input id="recheck" type="number" min="1" max="24" ?disabled=${this.#locked('scan.recheckMonths')} .value=${String(d.scan.recheckMonths)}
                @change=${(e: Event) => this.#update((x) => (x.scan.recheckMonths = Number((e.target as HTMLInputElement).value) || 6))}>
              ${this.#lockNote('scan.recheckMonths')}
            </div>
          </div>
        </uui-box>

        <uui-box headline="Visitor features (public website)">
          <div class="stack">
            <p class="notice"><uui-icon name="icon-info"></uui-icon> The preferences button doesn't make the site compliant. It's an extra on top of a site that already passes.</p>
            <fieldset>
              <legend>Follow visitors' browser and OS settings</legend>
              ${this.#check('visitor.followOsMotion', 'Reduced motion', d.visitor.followOsMotion, (v) => this.#update((x) => (x.visitor.followOsMotion = v)))}
              ${this.#check('visitor.followOsContrast', 'More contrast', d.visitor.followOsContrast, (v) => this.#update((x) => (x.visitor.followOsContrast = v)),
                'Off by default: generic contrast CSS is what most often clashes with a design. Test the site with it on before enabling.')}
            </fieldset>
            <fieldset>
              <legend>Accessibility preferences button</legend>
              ${this.#check('visitor.panel.enabled', 'Show the preferences button', panel.enabled, (v) => this.#update((x) => (x.visitor.panel.enabled = v)))}
              <div class="indent">
                ${PANEL_OPTIONS.map((o) => this.#check(`visitor.panel.${o.key}`, o.label, panel[o.key] as boolean,
                  (v) => this.#update((x) => ((x.visitor.panel[o.key] as boolean) = v)), undefined, !panel.enabled))}
                ${this.#select('visitor.panel.position', 'Button position', panel.position, [['bottom-right', 'Bottom right'], ['bottom-left', 'Bottom left']],
                  (v) => this.#update((x) => (x.visitor.panel.position = v as PanelSettings['position'])), !panel.enabled)}
              </div>
            </fieldset>
            <details>
              <summary>Panel texts per language</summary>
              <p class="muted">Leave a field empty to use the built-in text (English and Swedish ship with the package).</p>
              ${this._cultures.map((c) => this.#textsTable(c))}
              <div class="row">
                <div class="field">
                  <label for="new-culture">Add a language (e.g. da, de)</label>
                  <input id="new-culture" .value=${this._newCulture} @input=${(e: Event) => (this._newCulture = (e.target as HTMLInputElement).value.trim().toLowerCase())}>
                </div>
                <uui-button look="secondary" label="Add language" ?disabled=${!/^[a-z]{2}(-[a-z]{2})?$/.test(this._newCulture)}
                  @click=${() => { this._cultures = [...new Set([...this._cultures, this._newCulture])]; this._newCulture = ''; }}></uui-button>
              </div>
            </details>
          </div>
        </uui-box>

        <div class="row">
          <uui-button type="submit" look="primary" color="positive" label="Save settings" .state=${this._saving ? 'waiting' : undefined}></uui-button>
        </div>
      </form>`;
  }

  #textsTable(culture: string) {
    const texts = this._draft!.visitor.panel.texts?.[culture] ?? {};
    return html`
      <table>
        <caption>${culture}</caption>
        <thead><tr><th scope="col">Text</th><th scope="col">Override</th></tr></thead>
        <tbody>
          ${Object.entries(PANEL_TEXT_KEYS).map(([key, fallback]) => {
            const id = `t-${culture}-${key}`;
            const path = `visitor.panel.texts.${culture}.${key}`;
            return html`<tr>
              <th scope="row"><label for=${id}>${key}</label></th>
              <td>
                <input id=${id} placeholder=${fallback} ?disabled=${this.#locked(path)} .value=${texts[key] ?? ''}
                  @change=${(e: Event) => this.#update((x) => {
                    const value = (e.target as HTMLInputElement).value.trim();
                    x.visitor.panel.texts ??= {};
                    const t = (x.visitor.panel.texts[culture] ??= {});
                    if (value) t[key] = value;
                    else delete t[key];
                  })}>
                ${this.#lockNote(path)}
              </td>
            </tr>`;
          })}
        </tbody>
      </table>`;
  }

  #lockNote(path: string) {
    return this.#locked(path) ? html`<span class="lock"><uui-icon name="icon-lock"></uui-icon> Set in configuration</span>` : nothing;
  }

  #check(path: string, label: string, checked: boolean, onChange: (v: boolean) => void, hint?: string, disabled = false) {
    const id = `c-${path.split('.').join('-')}`;
    const hintId = hint ? `${id}-hint` : undefined;
    return html`
      <div class="check">
        <input id=${id} type="checkbox" .checked=${checked} ?disabled=${disabled || this.#locked(path)} aria-describedby=${hintId ?? nothing}
          @change=${(e: Event) => onChange((e.target as HTMLInputElement).checked)}>
        <label for=${id}>${label}</label>
        ${this.#lockNote(path)}
        ${hint ? html`<p id=${hintId} class="muted hint">${hint}</p>` : nothing}
      </div>`;
  }

  #select(path: string, label: string, value: string, options: [string, string][], onChange: (v: string) => void, disabled = false) {
    const id = `s-${path.split('.').join('-')}`;
    return html`
      <div class="field">
        <label for=${id}>${label}</label>
        <select id=${id} ?disabled=${disabled || this.#locked(path)} @change=${(e: Event) => onChange((e.target as HTMLSelectElement).value)}>
          ${options.map(([v, text]) => html`<option value=${v} ?selected=${v === value}>${text}</option>`)}
        </select>
        ${this.#lockNote(path)}
      </div>`;
  }

  static override styles = [shared, css`
    .field { display: grid; gap: 4px; }
    .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); align-items: center; min-height: 44px; }
    .check input { width: 20px; height: 20px; margin: 0; }
    .check .hint, .check .lock { grid-column: 2; margin: 0; }
    .indent { padding-left: var(--uui-size-space-6); display: grid; }
    fieldset { border: 0; padding: 0; margin: 0; }
    legend { font-weight: 600; margin-bottom: var(--uui-size-space-2); }
    select, input:not([type='checkbox']), textarea {
      font: inherit; min-height: 36px; padding: 0 var(--uui-size-space-3); max-width: 32rem;
      border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    textarea { padding: var(--uui-size-space-2); }
    input:disabled, select:disabled, textarea:disabled { opacity: 0.7; }
    .lock { color: var(--uui-color-text-alt); font-size: 0.85rem; display: inline-flex; gap: 4px; align-items: center; }
    .warning, .notice { display: flex; gap: var(--uui-size-space-2); align-items: flex-start; padding: var(--uui-size-space-3); background: var(--uui-color-surface-alt); border-radius: var(--uui-border-radius); margin: 0; }
    .warning { border-left: 4px solid var(--a11y-warning); }
    /* Informational: Umbraco's blue, as a tint behind dark text (contrast unchanged) and on the edge and icon. */
    .notice { --a11y-info: var(--uui-color-selected, #3544b1); border-left: 4px solid var(--a11y-info); background: color-mix(in srgb, var(--a11y-info) 8%, var(--uui-color-surface)); }
    .notice uui-icon { color: var(--a11y-info); flex: none; margin-top: 2px; }
    caption { text-align: left; font-weight: 600; padding: var(--uui-size-space-3) 0; }
    table input { width: 100%; }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-settings': EsattoA11ySettingsElement;
  }
}
