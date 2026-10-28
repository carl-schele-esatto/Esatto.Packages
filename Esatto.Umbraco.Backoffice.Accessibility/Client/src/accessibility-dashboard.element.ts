// Content → Accessibility dashboard. Registered in public/umbraco-package.json.
//
// The shell loads what every tab needs (knowledge base, sites, settings), holds the selected site and
// tab, and switches tabs when a child asks (navigate.ts). Each tab loads its own data.

import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { getKnowledge, getSettings, getSites } from './api';
import { shared } from './dashboard.styles';
import { NAVIGATE_EVENT, type NavigateDetail, type TabId } from './navigate';
import type { IssueFilter, Knowledge, SettingsResponse, SiteInfo } from './types';
import './tabs/overview.element';
import './tabs/issues.element';
import './tabs/checklist.element';
import './tabs/settings.element';

const TABS: { id: TabId; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'issues', label: 'Issues' },
  { id: 'checklist', label: 'Manual checklist' },
  { id: 'settings', label: 'Settings' },
];

@customElement('esatto-accessibility-dashboard')
export class EsattoAccessibilityDashboardElement extends UmbLitElement {
  @state() private _knowledge: Knowledge | null = null;
  @state() private _sites: SiteInfo[] = [];
  @state() private _siteKey = '';
  @state() private _settings: SettingsResponse | null = null;
  @state() private _tab: TabId = 'overview';
  @state() private _filter: IssueFilter = {};
  @state() private _error: string | null = null;

  constructor() {
    super();
    this.addEventListener(NAVIGATE_EVENT, (e) => {
      const { tab, filter } = (e as CustomEvent<NavigateDetail>).detail;
      this._filter = filter ?? {};
      this._tab = tab;
    });
  }

  override async connectedCallback() {
    super.connectedCallback();
    const [knowledge, sites, settings] = await Promise.all([
      tryExecute(this, getKnowledge(), { disableNotifications: true }),
      tryExecute(this, getSites(), { disableNotifications: true }),
      tryExecute(this, getSettings(), { disableNotifications: true }),
    ]);
    if (knowledge.error || sites.error || settings.error || !knowledge.data || !sites.data || !settings.data) {
      this._error = 'Could not load the accessibility dashboard.';
      return;
    }
    this._knowledge = knowledge.data;
    this._sites = sites.data;
    this._siteKey = sites.data[0]?.key ?? '';
    this._settings = settings.data;
  }

  #settingsSaved(e: CustomEvent<SettingsResponse>) {
    this._settings = e.detail;
  }

  override render() {
    if (this._error) return html`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._settings || !this._knowledge) return html`<uui-loader></uui-loader>`;
    if (!this._sites.length) {
      return html`<uui-box headline="Accessibility"><p>There is no published site to scan yet. Publish a root page with a template first.</p></uui-box>`;
    }
    const tabs = TABS.filter((t) => t.id !== 'settings' || this._settings!.canEdit);
    return html`
      <div class="header">
        <h2>Accessibility</h2>
        ${this._sites.length > 1 ? html`
          <uui-select label="Site" .options=${this._sites.map((s) => ({ name: s.name, value: s.key, selected: s.key === this._siteKey }))}
            @change=${(e: Event) => (this._siteKey = (e.target as HTMLSelectElement).value)}></uui-select>` : nothing}
      </div>
      <uui-tab-group>
        ${tabs.map((t) => html`<uui-tab id="tab-${t.id}" aria-controls="tabpanel" label=${t.label} ?active=${this._tab === t.id} @click=${() => { this._filter = {}; this._tab = t.id; }}>${t.label}</uui-tab>`)}
      </uui-tab-group>
      <div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${this._tab}">${this.#panel()}</div>`;
  }

  #panel() {
    switch (this._tab) {
      case 'issues':
        return html`<esatto-a11y-issues .siteKey=${this._siteKey} .filter=${this._filter} .settings=${this._settings}></esatto-a11y-issues>`;
      case 'checklist':
        return html`<esatto-a11y-checklist .siteKey=${this._siteKey}></esatto-a11y-checklist>`;
      case 'settings':
        return html`<esatto-a11y-settings .settings=${this._settings} @saved=${this.#settingsSaved}></esatto-a11y-settings>`;
      default:
        return html`<esatto-a11y-overview .siteKey=${this._siteKey} .settings=${this._settings}></esatto-a11y-overview>`;
    }
  }

  static override styles = [shared, css`
    :host { padding: var(--uui-size-layout-1); }
    .header { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; justify-content: space-between; margin-bottom: var(--uui-size-space-4); }
    uui-tab-group { border-bottom: 1px solid var(--uui-color-divider); }
    .panel { padding-top: var(--uui-size-space-5); }
  `];
}

export default EsattoAccessibilityDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    'esatto-accessibility-dashboard': EsattoAccessibilityDashboardElement;
  }
}
