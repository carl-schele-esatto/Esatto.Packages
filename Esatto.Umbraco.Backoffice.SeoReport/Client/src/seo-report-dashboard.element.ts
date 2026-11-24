// Content → SEO Report dashboard. Registered in public/umbraco-package.json.
//
// THE SHELL. It draws what dashboard-model.ts holds (the status, the selected month's report with its note and fetch time, what is loading,
// what failed) and passes clicks to it; the model is the only place that talks to the API, and what to show is decided by the pure
// functions of view-state.ts. The shell itself holds only what is the element's own: the selected tab and whether the user may open Settings.
//
// OPENING THE DASHBOARD NEVER FETCHES FROM GOOGLE: loading reads the status and the newest archived report, nothing else. What goes to Google
// is a person's click: "Refresh" (one month) and "Fetch now" (the missing months, in the background, followed by polling).
//
// THE TABS. Analysis, Traffic, Queries, Most clicked, To fix, Analytics vs Search Console and Settings are elements of their own (tabs/). The
// overview card stays above the tab panel on every tab. The shell holds the selected month's note; the Analysis tab saves it and says so
// (`note-saved`), and the shell keeps what it holds in step. The Settings tab calls back when settings were saved (`.onSaved`: the status is read
// again, without the loading screen; a plain callback, because a save can be accepted after the tab has left) and says when its Fetch now
// started a run (`fetch-started`: the shell's one poller follows it).
//
// THE OPEN TAB STAYS THE SAME ELEMENT WHEN THE STATE CHANGES. The tab strip and its panel are always the last part of ONE template (#content),
// whatever comes above them, so a save that makes the package ready or a backfill that ends does not rebuild the open tab. And once the person
// works on the Settings tab (`.onEngaged`), Settings is PINNED (view-state.ts, TabChoice): it stays the open tab when the strip grows (the report
// arrives) or a removal makes the package not connected, until the person chooses another tab. Settings that is only the fallback (the one tab
// during a first backfill) still gives way to the report.

import { css, customElement, html, nothing, state } from '@umbraco-cms/backoffice/external/lit';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { hasSettingsAccess } from './access';
import { getFetchProgress, getReport, getStatus, refreshReport, startFetch } from './api';
import { shared } from './dashboard.styles';
import { DashboardModel, type Outcome } from './dashboard-model';
import { errorTexts, refText, text } from './localize';
import { NOTE_SAVED_EVENT, type NoteSavedDetail } from './analysis-model';
import { NAVIGATE_EVENT, TAB_LABEL_KEYS, type NavigateDetail, type TabId } from './navigate';
import { FETCH_STARTED_EVENT, type FetchStartedDetail } from './settings-model';
import type { Report, StatusResponse } from './types';
import {
  activeTab,
  chooseTab,
  hasContent,
  lastErrorOf,
  periodsNewestFirst,
  pinSettings,
  selectedPeriod,
  tabsFor,
  viewStateOf,
  type TabChoice,
  type ViewState,
} from './view-state';
import './fetch-card.element';
import './not-connected.element';
import './overview.element';
import './period-bar.element';
import './problem.element';
import './tabs/analysis.element';
import './tabs/comparison.element';
import './tabs/queries.element';
import './tabs/settings.element';
import './tabs/to-fix.element';
import './tabs/top-pages.element';
import './tabs/traffic.element';

@customElement('esatto-seo-report-dashboard')
export class EsattoSeoReportDashboardElement extends UmbLitElement {
  /** The tab the person chose, and whether Settings is pinned open (view-state.ts). */
  @state() private _choice: TabChoice = { tab: 'analysis', pinned: false };
  /** Whether the user may open Settings: undefined until the current user has loaded. */
  @state() private _settingsAccess: boolean | undefined;

  readonly #model = new DashboardModel(
    { getStatus, getReport, refreshReport, startFetch, getFetchProgress },
    () => this.requestUpdate(),
  );

  constructor() {
    super();

    this.addEventListener(NAVIGATE_EVENT, (event) => this.#choose((event as CustomEvent<NavigateDetail>).detail.tab));

    // The Analysis tab saved a month's note: the note held here follows (for the month shown; a note of another month is the server's, and the
    // next read of that month brings it).
    this.addEventListener(NOTE_SAVED_EVENT, (event) => {
      event.stopPropagation();
      const { period, note } = (event as CustomEvent<NoteSavedDetail>).detail;
      this.#model.setNote(period, note);
    });

    // The Settings tab's Fetch now started a run, or found one going: the shell's poller follows it, as it follows its own.
    this.addEventListener(FETCH_STARTED_EVENT, (event) => {
      event.stopPropagation();
      void this.#model.followRun((event as CustomEvent<FetchStartedDetail>).detail.progress);
    });

    // Which sections the user may open: the same fact the server's Settings policy checks. It loads after the dashboard does.
    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(context?.allowedSections, (sections) => (this._settingsAccess = hasSettingsAccess(sections)), 'allowedSections');
    });
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#model.connect();
  }

  override disconnectedCallback() {
    // A poller left running would keep asking the server, and an answer that arrives later must not touch an element that has left.
    this.#model.disconnect();
    super.disconnectedCallback();
  }

  // ---- Showing ------------------------------------------------------------------------------------------------------------------

  override render() {
    const { status, loading } = this.#model.state;

    return html`
      <header class="header">
        <span class="logo" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
        <div>
          <h2>${text(this, 'dashboardTitle')}</h2>
          <p class="muted">${status?.displayName || text(this, 'uiSubtitleText')}</p>
        </div>
      </header>
      <div class="page">${loading ? this.#loading() : this.#content(status)}</div>
    `;
  }

  #loading() {
    return html`<div class="loading" role="status"><uui-loader></uui-loader><span class="visually-hidden">${text(this, 'uiLoadingLabel')}</span></div>`;
  }

  #content(status: StatusResponse | null) {
    // Nothing could be read at all: the failure is all there is to show.
    if (!status) return this.#problem();

    const { report, readFailed } = this.#model.state;
    const state = viewStateOf(status, report, readFailed);
    // ONE template, with the tabs last whatever the state (see the header): only what comes above them changes with it.
    return html`${this.#problem()} ${this.#notices()} ${this.#lead(state, status)} ${this.#tabs(state)}`;
  }

  // What comes above the tabs in each state.
  #lead(state: ViewState, status: StatusResponse) {
    const { report, starting } = this.#model.state;

    switch (state) {
      case 'notConnected':
        return html`<esatto-seo-report-not-connected .status=${status} .canOpenSettings=${this._settingsAccess}></esatto-seo-report-not-connected>`;
      case 'progress':
        // The card leads; a report that is already archived stays below it.
        return html`
          <esatto-seo-report-fetch-card .fetch=${status.fetch} ?busy=${starting} @fetch-now=${() => void this.#model.fetchNow()}></esatto-seo-report-fetch-card>
          ${report ? this.#report(status, report) : nothing}
        `;
      case 'error':
        // Only the problem (above) and the controls that can mend it: never "no report" or "nothing fetched yet".
        return this.#controls(status);
      default:
        return this.#report(status, report);
    }
  }

  #report(status: StatusResponse, report: Report | null) {
    const { period, pendingPeriod, fetchedUtc, refreshing, switching } = this.#model.state;
    const behind = report !== null && !status.upToDate && !status.fetch.running;

    return html`
      ${hasContent(report)
        ? html`<esatto-seo-report-overview
            .report=${report}
            .period=${pendingPeriod ?? period}
            .periods=${periodsNewestFirst(status.periods)}
            .fetchedUtc=${fetchedUtc}
            ?refreshing=${refreshing}
            ?busy=${switching}
            @period-change=${(event: CustomEvent<string>) => this.#onPeriod(event)}
            @refresh=${(event: Event) => this.#onRefresh(event)}
          ></esatto-seo-report-overview>`
        : this.#empty(report, status)}
      ${behind ? this.#behind(status) : nothing}
    `;
  }

  // The month controls on their own (the month picker, when it was fetched, Refresh), for the states with no overview card to carry them.
  #controls(status: StatusResponse) {
    const { period, pendingPeriod, fetchedUtc, refreshing, switching } = this.#model.state;
    const selected = pendingPeriod ?? selectedPeriod(period, status.periods);

    return selected
      ? html`<esatto-seo-report-period-bar
          .periods=${periodsNewestFirst(status.periods)}
          .period=${selected}
          .fetchedUtc=${fetchedUtc}
          ?refreshing=${refreshing}
          ?busy=${switching}
          @period-change=${(event: CustomEvent<string>) => this.#onPeriod(event)}
          @refresh=${(event: Event) => this.#onRefresh(event)}
        ></esatto-seo-report-period-bar>`
      : nothing;
  }

  // Connected, but nothing to chart: the report says neither source returned anything, or the month has no report. The month controls
  // stay, so another month can be picked or this one fetched.
  #empty(report: Report | null, status: StatusResponse) {
    const { period } = this.#model.state;
    const note = refText(this, 'dataStatusNote', report?.data_status_note);

    return html`
      <section class="card" aria-labelledby="empty-title">
        <div class="card-head">
          <span class="icon-badge solid round tone-none" aria-hidden="true"><uui-icon name="icon-chart"></uui-icon></span>
          <h3 id="empty-title">${text(this, 'uiEmptyMonthTitle')}</h3>
        </div>
        <div class="stack">
          <p>${report ? html`${note} ${text(this, 'uiEmptyMonthText')}` : text(this, 'uiNoReportText', { period: period || null })}</p>
          ${period ? this.#controls(status) : nothing}
        </div>
      </section>
    `;
  }

  // The archive lacks the latest month and nothing is fetching it: say so, with the way to fetch it, and why the last run stopped.
  #behind(status: StatusResponse) {
    const failure = lastErrorOf(status.fetch);
    const reason = failure ? errorTexts(this, failure) : null;

    return html`
      <div class="banner tone-warning" role="status">
        <div>
          <p>${text(this, 'uiArchiveBehindText', { period: status.expectedLatestPeriod })}</p>
          ${reason ? html`<p class="muted small"><strong>${reason.title}</strong> ${reason.text}</p>` : nothing}
          <div class="actions">
            <uui-button look="primary" label=${text(this, 'uiFetchNowLabel')} ?disabled=${this.#model.state.starting} @click=${() => void this.#model.fetchNow()}>
              ${text(this, 'uiFetchNowLabel')}
            </uui-button>
          </div>
        </div>
      </div>
    `;
  }

  // What failed, and how the last backfill ended.
  #problem() {
    const { problem } = this.#model.state;
    return problem ? html`<esatto-seo-report-problem .error=${problem} @retry=${() => this.#model.retry()}></esatto-seo-report-problem>` : nothing;
  }

  #notices() {
    const { lost, outcome } = this.#model.state;

    return html`
      ${lost
        ? html`<div class="banner tone-warning" role="alert">
            <div>
              <h3>${text(this, 'uiFetchLostTitle')}</h3>
              <p>${text(this, 'uiFetchLostText')}</p>
              <div class="actions">
                <uui-button look="outline" label=${text(this, 'uiTryAgainLabel')} @click=${() => this.#model.followAgain()}>${text(this, 'uiTryAgainLabel')}</uui-button>
              </div>
            </div>
          </div>`
        : nothing}
      ${outcome ? this.#outcomeNotice(outcome) : nothing}
    `;
  }

  #outcomeNotice(outcome: Outcome) {
    const failed = outcome.kind === 'failed';
    const reason = failed ? errorTexts(this, outcome.errorCode) : null;
    const tone = failed ? 'critical' : outcome.kind === 'fetched' ? 'good' : 'notice';

    return html`
      <div class="banner tone-${tone}" role=${failed ? 'alert' : 'status'}>
        <div>
          ${reason
            ? html`<h3>${text(this, 'uiFetchFailedTitle')}</h3><p><strong>${reason.title}</strong> ${reason.text}</p>`
            : html`<p>${outcome.kind === 'fetched' ? text(this, 'uiFetchedMonthsText', { done: outcome.done }) : text(this, 'uiNothingToFetchText')}</p>`}
          <div class="actions">
            <uui-button look="outline" label=${text(this, 'uiDismissLabel')} @click=${() => this.#model.dismissOutcome()}>${text(this, 'uiDismissLabel')}</uui-button>
          </div>
        </div>
      </div>
    `;
  }

  #onPeriod(event: CustomEvent<string>) {
    event.stopPropagation();
    void this.#model.selectPeriod(event.detail);
  }

  #onRefresh(event: Event) {
    event.stopPropagation();
    void this.#model.refresh();
  }

  // ---- The tabs -----------------------------------------------------------------------------------------------------------------

  // The person chose a tab (the strip, or a button that leads to one): choosing any but Settings releases a pin.
  #choose(tab: TabId) {
    this._choice = chooseTab(this._choice, tab);
  }

  // The person works on the Settings tab: it stays open whatever the strip does next. Called often; changes the state once.
  #settingsEngaged = () => {
    const next = pinSettings(this._choice);
    if (next !== this._choice) this._choice = next;
  };

  // Settings were saved (possibly after the tab left): the status is read again, without the loading screen. Carries nothing.
  #settingsSaved = () => void this.#model.reloadStatus();

  #tabs(state: ViewState) {
    const tabs = tabsFor(this.#model.state.report, this._settingsAccess === true);
    if (tabs.length === 0) return nothing;

    const active = activeTab(this._choice.tab, tabs, state, this._choice.pinned);
    return html`
      <uui-tab-group aria-label=${text(this, 'uiTabsLabel')}>
        ${tabs.map(
          (tab) => html`<uui-tab
            id="tab-${tab}"
            aria-controls="tabpanel"
            label=${text(this, TAB_LABEL_KEYS[tab])}
            ?active=${active === tab}
            @click=${() => this.#choose(tab)}
            >${text(this, TAB_LABEL_KEYS[tab])}</uui-tab
          >`,
        )}
      </uui-tab-group>
      ${active ? html`<div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${active}">${this.#panel(active)}</div>` : nothing}
    `;
  }

  // THE SEAM. One case per tab. The report tabs read the report the shell holds (.report) and the selected month (.period) and fetch nothing of
  // their own, except that the Traffic tab hosts the daily card, which reads GA4 when it is opened, and the Analysis tab, which reads the archive
  // (GET analysis, never Google) and gets the month's note (.note) to edit. The Settings tab reads and saves the settings itself and gets the
  // status (.status: where its guide starts, how a run goes). A tab that loads its own data shows a failure with <esatto-seo-report-problem> and
  // reads through api.ts.
  #panel(tab: TabId) {
    const { status, report, note, period, archiveRevision } = this.#model.state;

    switch (tab) {
      case 'analysis':
        return html`<esatto-seo-report-analysis .period=${period} .note=${note} .revision=${archiveRevision}></esatto-seo-report-analysis>`;
      case 'traffic':
        return html`<esatto-seo-report-traffic .report=${report} .period=${period}></esatto-seo-report-traffic>`;
      case 'queries':
        return html`<esatto-seo-report-queries .report=${report} .period=${period}></esatto-seo-report-queries>`;
      case 'topPages':
        return html`<esatto-seo-report-top-pages .report=${report} .period=${period}></esatto-seo-report-top-pages>`;
      case 'toFix':
        return html`<esatto-seo-report-to-fix .report=${report} .period=${period}></esatto-seo-report-to-fix>`;
      case 'comparison':
        return html`<esatto-seo-report-comparison .report=${report} .period=${period}></esatto-seo-report-comparison>`;
      case 'settings':
        return html`<esatto-seo-report-settings .status=${status} .onSaved=${this.#settingsSaved} .onEngaged=${this.#settingsEngaged}></esatto-seo-report-settings>`;
    }
  }

  static override styles = [
    shared,
    css`
      :host { padding: var(--uui-size-layout-1); max-width: 1440px; }
      .header { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; margin-bottom: var(--uui-size-space-5); }
      .header h2 { font-size: 24px; font-weight: 700; letter-spacing: -0.01em; line-height: 1.2; }
      .logo {
        display: grid; place-items: center; width: 44px; height: 44px; border-radius: 12px; font-size: 22px; color: #fff;
        background: linear-gradient(135deg, #2f6fde 0%, #6d4ce0 100%); box-shadow: 0 6px 16px -6px rgb(77 79 224 / 0.6);
      }
      .loading { display: grid; place-items: center; padding: var(--uui-size-layout-2); }
      uui-tab-group { border-bottom: 1px solid var(--seo-line); }
      .panel { padding-top: var(--uui-size-space-6); }
    `,
  ];
}

export default EsattoSeoReportDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-dashboard': EsattoSeoReportDashboardElement;
  }
}
