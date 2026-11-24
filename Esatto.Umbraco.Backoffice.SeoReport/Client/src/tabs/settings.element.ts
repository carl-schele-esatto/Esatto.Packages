// The Settings tab (users with the Settings section only; the shell offers it to nobody else, and the server's endpoints check it again): the
// connection to Google (the GA4 property, the Search Console property, the service-account key, Test connection), the "How to connect" guide, the
// report's own settings (brand terms, display name, who sees "To fix"), fetching (automatic, and Fetch now), and Save.
//
// The state machine is settings-model.ts (what is read, how Save, Test connection, Remove and Fetch now go, where the key may and may not be); what
// is shown and sent is settings-view.ts; the key, the test and the guide are elements of their own (components/). This draws them.
//
// A FIELD CONFIGURATION SETS (the package's section of the site's configuration) is read-only, with "Set in configuration" beside it, and its value is
// sent as it is (the server ignores it). While a save is on its way every field is read-only as well: the answer replaces the form. Nothing is ever
// `disabled`: a read-only text field stays focusable and readable, and a checkbox or radio that cannot change says so (aria-readonly) and ignores
// the click.
//
// WHAT THE SHELL HANDS AND HEARS:
//   .status          the dashboard's status: where the guide starts (open until the package is set up) and how a run is going
//   .onSaved         called when settings (or the key) were saved: the shell reads the status again (and the poller follows a run the save
//                    started). A callback and not an event, because a save the server accepts after the tab has left must still reach the shell
//   .onEngaged       called when the person works here (focus or typing inside the tab, or the model is engaged: something on its way, unsaved
//                    changes, an entered key, Replace, a removal to confirm): the shell pins Settings open
//   fetch-started    Fetch now started a run: detail { progress } (null: one was going already); the shell follows it with its one poller
// Neither callback nor the event carries anything of the key. The tab element stays on the page while the shell reads the status again, so what
// is being typed is never lost to that; leaving the tab takes it off the page, and the model forgets the key then.

import { css, customElement, html, live, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { getSettings, getUserGroups, putSettings, startFetch, testConnection } from '../api';
import { renderCardHead } from '../components/analysis-cards';
import { editorStyles } from '../components/editor-styles';
import { settingsStyles } from '../components/settings-styles';
import { shared } from '../dashboard.styles';
import { errorTexts, text } from '../localize';
import { searchConsoleView, type SearchConsoleKind } from '../search-console';
import { FETCH_STARTED_EVENT, SettingsModel, type FetchStartedDetail, type SettingsModelState } from '../settings-model';
import { controlOf, groupOptions, type SettingsField, type SettingsForm } from '../settings-view';
import { MAX_TERM_LENGTH, MAX_TERMS, brandTermsCheck } from '../target-terms';
import type { SettingsResponse, StatusResponse } from '../types';
import { progressOf } from '../view-state';
import '../components/connection-test.element';
import '../components/key-field.element';
import '../components/setup-guide.element';
import '../problem.element';

type Control = ReturnType<typeof controlOf>;

@customElement('esatto-seo-report-settings')
export class EsattoSeoReportSettingsElement extends UmbLitElement {
  /** The dashboard's status, as the shell holds it. */
  @property({ attribute: false }) status: StatusResponse | null = null;

  /** Called when settings (or the key) were saved, also after the tab has left. Takes nothing and gets nothing. */
  @property({ attribute: false }) onSaved: () => void = () => {};

  /** Called when the person works on the tab, so the shell keeps it open. Called often; the shell acts once. */
  @property({ attribute: false }) onEngaged: () => void = () => {};

  // Not used for drawing: reading the model's state is what a render does. Declared so that a change of the model is a change of the element.
  @state() private _version = 0;

  // Where the guide starts, decided once, from the first status: a guide the person opened or closed stays so.
  #guideOpen: boolean | undefined;

  // fetch-started bubbles out of the shell's shadow root to the shell, which listens on itself (as for note-saved). Nothing here carries the key.
  readonly #model = new SettingsModel({ getSettings, putSettings, testConnection, startFetch, getUserGroups }, () => this.#changed(), {
    saved: () => this.onSaved(),
    fetchStarted: (progress) =>
      this.dispatchEvent(new CustomEvent<FetchStartedDetail>(FETCH_STARTED_EVENT, { detail: { progress }, bubbles: true, composed: true })),
  });

  constructor() {
    super();
    // Focus or typing anywhere in the tab (focusin and input cross the shadow boundaries of the tab's own elements): the person works here.
    this.addEventListener('focusin', () => this.onEngaged());
    this.addEventListener('input', () => this.onEngaged());
  }

  #changed() {
    this._version += 1;
    if (this.#model.engaged) this.onEngaged();
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#model.connect();
  }

  override disconnectedCallback() {
    // The key is forgotten, and an answer that arrives later must not touch an element that has left.
    this.#model.disconnect();
    super.disconnectedCallback();
  }

  override willUpdate() {
    if (this.#guideOpen === undefined && this.status) this.#guideOpen = this.status.setup !== 'ready';
  }

  // An edit the model ignores (a locked field, a save on its way) is drawn again, so the control shows the value that stands.
  #edit(patch: Partial<SettingsForm>) {
    this.#model.edit(patch);
    this.requestUpdate();
  }

  #onDraft = (value: string) => this.#model.setDraftKey(value);

  // A checkbox or radio that cannot change ignores the click (the Space key clicks too); the browser then leaves it as it was.
  #guard = (readonly: boolean) => (event: Event) => {
    if (readonly) event.preventDefault();
  };

  // ---- Showing ------------------------------------------------------------------------------------------------------------------

  override render() {
    const model = this.#model.state;

    if (model.loadProblem) {
      return html`<esatto-seo-report-problem .error=${model.loadProblem} @retry=${() => void this.#model.retryLoad()}></esatto-seo-report-problem>`;
    }
    if (!model.saved || !model.form) {
      return html`<section class="card"><p class="muted loading" role="status"><uui-loader-circle aria-hidden="true"></uui-loader-circle>${text(this, 'uiSettingsLoadingText')}</p></section>`;
    }

    const email = model.draftEmail ?? model.test.result?.serviceAccountEmail ?? model.saved.key.serviceAccountEmail;

    return html`
      <div class="page">
        <p class="intro">${text(this, 'uiSettingsIntroText')}</p>
        ${model.saved.locked.length > 0
          ? html`<p class="lock"><uui-icon name="icon-lock" aria-hidden="true"></uui-icon>${text(this, 'uiLockedFieldsText')}</p>`
          : nothing}
        <esatto-seo-report-setup-guide ?open=${this.#guideOpen ?? true} .email=${email}></esatto-seo-report-setup-guide>
        ${this.#connection(model, model.saved, model.form, email)} ${this.#report(model, model.saved, model.form)} ${this.#fetching(model, model.saved, model.form)}
        ${this.#saveBar(model, email)}
      </div>
    `;
  }

  #control(saved: SettingsResponse, field: SettingsField, model: Readonly<SettingsModelState>): Control {
    return controlOf(saved.locked, field, model.save.busy);
  }

  // The ids that describe a field: its help, and "Set in configuration" when configuration sets it.
  #describedBy(id: string, control: Control, ...more: string[]) {
    return [...more, `${id}-help`, ...(control.locked ? [`${id}-lock`] : [])].join(' ');
  }

  #lockNote(id: string, control: Control) {
    return control.locked
      ? html`<span class="lock" id="${id}-lock"><uui-icon name="icon-lock" aria-hidden="true"></uui-icon>${text(this, 'uiSetInConfigurationLabel')}</span>`
      : nothing;
  }

  #textField(id: string, labelKey: string, helpKey: string, value: string, control: Control, onInput: (value: string) => void) {
    return html`
      <div class="field">
        <label for=${id}>${text(this, labelKey)}</label>
        <input
          type="text"
          id=${id}
          autocomplete="off"
          spellcheck="false"
          .value=${live(value)}
          ?readonly=${control.readonly}
          aria-readonly=${control.readonly ? 'true' : 'false'}
          aria-describedby=${this.#describedBy(id, control)}
          @input=${(event: Event) => onInput((event.target as HTMLInputElement).value)}
        />
        ${this.#lockNote(id, control)}
        <p class="help muted" id="${id}-help">${text(this, helpKey)}</p>
      </div>
    `;
  }

  // ---- The connection -------------------------------------------------------------------------------------------------------------

  #connection(model: Readonly<SettingsModelState>, saved: SettingsResponse, form: SettingsForm, email: string | null) {
    return html`
      <section class="card" aria-labelledby="connection-title">
        ${renderCardHead('icon-link', 'connection-title', text(this, 'uiConnectionTitle'))}
        <div class="fields">
          ${this.#textField('ga4', 'uiGa4PropertyLabel', 'uiGa4PropertyHelpText', form.ga4PropertyId, this.#control(saved, 'ga4PropertyId', model), (value) =>
            this.#edit({ ga4PropertyId: value }),
          )}
          ${this.#searchConsole(model, saved, form)}
          <div class="field" role="group" aria-labelledby="key-legend">
            <p class="legend" id="key-legend">${text(this, 'uiKeyLegendLabel')}</p>
            <esatto-seo-report-key-field
              .mode=${this.#model.keyMode}
              .email=${saved.key.serviceAccountEmail}
              ?hasDraft=${model.hasDraftKey}
              .draftEmail=${model.draftEmail}
              .draftProblem=${model.draftProblem}
              .draftRevision=${model.draftRevision}
              ?confirmingRemove=${model.confirmingRemove}
              .removal=${model.remove}
              ?busy=${model.save.busy}
              .onDraft=${this.#onDraft}
              @replace=${() => this.#model.replaceKey()}
              @cancel-replace=${() => this.#model.cancelReplace()}
              @ask-remove=${() => this.#model.askRemove()}
              @cancel-remove=${() => this.#model.cancelRemove()}
              @confirm-remove=${() => void this.#model.removeKey()}
              @retry-remove=${() => void this.#model.removeKey()}
            ></esatto-seo-report-key-field>
          </div>
          <esatto-seo-report-connection-test
            .test=${model.test}
            .email=${email}
            .savedEmail=${saved.key.serviceAccountEmail}
            @test=${() => void this.#model.test()}
          ></esatto-seo-report-connection-test>
        </div>
      </section>
    `;
  }

  // The Search Console property: the kind (a radio group) and the text, and ONE line under it (search-console.ts, searchConsoleView): what will be
  // saved, exactly as the server will store it; or, instead, a warning (kept, but not as the kind chosen); or that the server will refuse it, with
  // the reason when there is one, and the field marked invalid. The line is described-by the field; the warning and the refusal are a polite live
  // region rather than an alert, because they change as the person types (the first letter of any address is not yet one). Set in configuration,
  // the property is the value in force, read-only, and no choice.
  #searchConsole(model: Readonly<SettingsModelState>, saved: SettingsResponse, form: SettingsForm) {
    const control = this.#control(saved, 'searchConsoleSite', model);

    if (control.locked) {
      return html`
        <fieldset>
          <legend id="sc-legend">${text(this, 'uiSearchConsoleLegendLabel')}</legend>
          <input type="text" id="sc" readonly aria-readonly="true" aria-labelledby="sc-legend" aria-describedby="sc-lock" .value=${saved.settings.searchConsoleSite ?? ''} />
          ${this.#lockNote('sc', control)}
        </fieldset>
      `;
    }

    const kind = form.searchConsoleKind;
    const view = searchConsoleView(kind, form.searchConsoleText);
    const choose = (next: SearchConsoleKind) => () => this.#edit({ searchConsoleKind: next });
    const reason = view.warning === 'looksLikeUrl' ? 'uiSearchConsoleLooksLikeUrlText' : view.warning === 'missingScheme' ? 'uiSearchConsoleMissingSchemeText' : null;

    return html`
      <fieldset aria-describedby="sc-help">
        <legend>${text(this, 'uiSearchConsoleLegendLabel')}</legend>
        <div class="choices">
          ${(['domain', 'prefix'] as const).map(
            (option) => html`<div class="check">
              <input
                type="radio"
                id="sc-${option}"
                name="sc-kind"
                .checked=${live(kind === option)}
                @click=${this.#guard(control.readonly)}
                @change=${choose(option)}
              />
              <label for="sc-${option}">${text(this, option === 'domain' ? 'uiSearchConsoleDomainLabel' : 'uiSearchConsolePrefixLabel')}</label>
            </div>`,
          )}
        </div>
        <div class="field">
          <label for="sc">${text(this, kind === 'domain' ? 'uiSearchConsoleDomainInputLabel' : 'uiSearchConsolePrefixInputLabel')}</label>
          <input
            type="text"
            id="sc"
            autocomplete="off"
            spellcheck="false"
            .value=${live(form.searchConsoleText)}
            ?readonly=${control.readonly}
            aria-readonly=${control.readonly ? 'true' : 'false'}
            aria-invalid=${view.line === 'refused' ? 'true' : 'false'}
            aria-describedby="sc-sent sc-warning"
            @input=${(event: Event) => this.#edit({ searchConsoleText: (event.target as HTMLInputElement).value })}
          />
          <p class="preview" id="sc-sent">${view.line === 'preview' ? text(this, 'uiSearchConsoleSentText', { value: view.stored }) : nothing}</p>
          <div id="sc-warning" role="status" aria-live="polite">
            ${view.line === 'refused'
              ? html`<p class="error"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiSearchConsoleRefusedText')}</p>
                  ${reason ? html`<p class="help">${text(this, reason)}</p>` : nothing}`
              : view.line === 'warning' && reason
                ? html`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, reason)} ${text(this, 'uiSearchConsoleSavedAsDomainText')}</p>`
                : nothing}
          </div>
        </div>
        <p class="help muted" id="sc-help">${text(this, 'uiSearchConsoleHelpText')}</p>
      </fieldset>
    `;
  }

  // ---- The report -----------------------------------------------------------------------------------------------------------------

  #report(model: Readonly<SettingsModelState>, saved: SettingsResponse, form: SettingsForm) {
    const brand = this.#control(saved, 'brandTerms', model);
    // The server keeps at most MAX_TERMS terms of at most MAX_TERM_LENGTH characters and drops the rest silently: the counter says so first.
    const terms = brandTermsCheck(form.brandTerms);

    return html`
      <section class="card" aria-labelledby="report-title">
        ${renderCardHead('icon-chart-curve', 'report-title', text(this, 'uiReportSettingsTitle'))}
        <div class="fields">
          <div class="editor">
            <label for="brand">${text(this, 'uiBrandTermsLabel')}</label>
            <textarea
              id="brand"
              rows="4"
              spellcheck="false"
              .value=${live(form.brandTerms)}
              ?readonly=${brand.readonly}
              aria-readonly=${brand.readonly ? 'true' : 'false'}
              aria-describedby=${this.#describedBy('brand', brand, 'brand-counter', 'brand-warnings', 'brand-change')}
              @input=${(event: Event) => this.#edit({ brandTerms: (event.target as HTMLTextAreaElement).value })}
            ></textarea>
            <div class="counters">
              <span class="counter ${terms.tooMany ? 'over' : ''}" id="brand-counter">
                ${terms.tooMany ? html`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>` : nothing}${text(this, 'uiBrandTermsCounterText', {
                  count: terms.count,
                  max: MAX_TERMS,
                  maxLength: MAX_TERM_LENGTH,
                })}
              </span>
            </div>
            <div id="brand-warnings">
              ${terms.tooMany
                ? html`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiTermsTooManyText', { max: MAX_TERMS })}</p>`
                : nothing}
              ${terms.tooLong > 0
                ? html`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${text(this, 'uiBrandTermsTooLongText', {
                    count: terms.tooLong,
                    max: MAX_TERM_LENGTH,
                  })}</p>`
                : nothing}
            </div>
            ${this.#lockNote('brand', brand)}
            <p class="help muted" id="brand-help">${text(this, 'uiBrandTermsHelpText')}</p>
            <p class="help muted" id="brand-change"><uui-icon name="icon-info" aria-hidden="true"></uui-icon> ${text(this, 'uiBrandTermsChangeText')}</p>
          </div>
          ${this.#textField('display-name', 'uiDisplayNameLabel', 'uiDisplayNameHelpText', form.displayName, this.#control(saved, 'displayName', model), (value) =>
            this.#edit({ displayName: value }),
          )}
          ${this.#groups(model, saved, form)}
        </div>
      </section>
    `;
  }

  // Who sees "To fix": a checkbox per user group (the alias is what is kept). None chosen is everyone, which the help says.
  #groups(model: Readonly<SettingsModelState>, saved: SettingsResponse, form: SettingsForm) {
    const control = this.#control(saved, 'findingsUserGroups', model);
    const toggle = (alias: string, on: boolean) => {
      const others = form.findingsUserGroups.filter((chosen) => chosen.toLowerCase() !== alias.toLowerCase());
      this.#edit({ findingsUserGroups: on ? [...others, alias] : others });
    };

    let choices;
    if (model.groupsProblem) {
      choices = html`<esatto-seo-report-problem .error=${model.groupsProblem} @retry=${() => void this.#model.retryGroups()}></esatto-seo-report-problem>`;
    } else if (!model.groups) {
      choices = html`<p class="muted loading" role="status"><uui-loader-circle aria-hidden="true"></uui-loader-circle>${text(this, 'uiGroupsLoadingText')}</p>`;
    } else {
      const options = groupOptions(model.groups, form.findingsUserGroups);
      choices =
        options.length === 0
          ? html`<p class="muted">${text(this, 'uiGroupsEmptyText')}</p>`
          : html`<div class="choices">
              ${options.map(
                (option, index) => html`<div class="check">
                  <input
                    type="checkbox"
                    id="group-${index}"
                    .checked=${live(option.selected)}
                    aria-readonly=${control.readonly ? 'true' : 'false'}
                    @click=${this.#guard(control.readonly)}
                    @change=${(event: Event) => toggle(option.alias, (event.target as HTMLInputElement).checked)}
                  />
                  <label for="group-${index}">${option.known ? option.name : text(this, 'uiGroupUnknownText', { alias: option.alias })}</label>
                </div>`,
              )}
            </div>`;
    }

    return html`
      <fieldset aria-describedby=${this.#describedBy('groups', control)}>
        <legend>${text(this, 'uiFindingsGroupsLegendLabel')}</legend>
        ${choices} ${this.#lockNote('groups', control)}
        <p class="help muted" id="groups-help">${text(this, 'uiFindingsGroupsHelpText')}</p>
      </fieldset>
    `;
  }

  // ---- Fetching -------------------------------------------------------------------------------------------------------------------

  #fetching(model: Readonly<SettingsModelState>, saved: SettingsResponse, form: SettingsForm) {
    const control = this.#control(saved, 'autoFetch', model);
    const { busy, alreadyRunning, problem } = model.fetch;

    return html`
      <section class="card" aria-labelledby="fetching-title">
        ${renderCardHead('icon-sync', 'fetching-title', text(this, 'uiFetchingTitle'))}
        <div class="fields">
          <div class="check">
            <input
              type="checkbox"
              id="auto-fetch"
              .checked=${live(form.autoFetch)}
              aria-readonly=${control.readonly ? 'true' : 'false'}
              aria-describedby=${this.#describedBy('auto-fetch', control)}
              @click=${this.#guard(control.readonly)}
              @change=${(event: Event) => this.#edit({ autoFetch: (event.target as HTMLInputElement).checked })}
            />
            <label for="auto-fetch">${text(this, 'uiAutoFetchLabel')}</label>
            ${this.#lockNote('auto-fetch', control)}
            <p class="help muted" id="auto-fetch-help">${text(this, 'uiAutoFetchHelpText')}</p>
          </div>
          <div class="editor">
            <div class="buttons">
              <button
                type="button"
                class="secondary"
                aria-disabled=${busy ? 'true' : 'false'}
                aria-busy=${busy ? 'true' : 'false'}
                aria-describedby="fetch-help"
                @click=${() => void this.#model.fetchNow()}
              >
                ${busy ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : html`<uui-icon name="icon-cloud-download" aria-hidden="true"></uui-icon>`}${text(this, 'uiFetchNowLabel')}
              </button>
            </div>
            <p class="help muted" id="fetch-help">${text(this, 'uiFetchNowHelpText')}</p>
            <div role="status" aria-live="polite">
              <p class="status-line">${this.#runText()}</p>
              ${alreadyRunning ? this.#alreadyRunning() : nothing}
            </div>
            ${problem ? html`<esatto-seo-report-problem .error=${problem} @retry=${() => void this.#model.fetchNow()}></esatto-seo-report-problem>` : nothing}
          </div>
        </div>
      </section>
    `;
  }

  // How the backfill is going, from the shell's status (the shell's poller keeps it current).
  #runText() {
    const fetch = this.status?.fetch;
    if (!fetch) return nothing;

    const progress = progressOf(fetch);
    if (progress.phase === 'starting') return text(this, 'uiProgressStartingText');
    if (progress.phase === 'running') return text(this, 'uiProgressText', { done: progress.done, total: progress.total, period: progress.currentPeriod });
    return text(this, 'uiFetchIdleText');
  }

  #alreadyRunning() {
    const { title, text: body } = errorTexts(this, 'alreadyRunning');
    return html`
      <div class="banner tone-notice">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-info"></uui-icon></span>
        <div><h4>${title}</h4><p>${body}</p></div>
      </div>
    `;
  }

  // ---- Saving ---------------------------------------------------------------------------------------------------------------------

  #saveBar(model: Readonly<SettingsModelState>, email: string | null) {
    const { busy, done, problem } = model.save;
    const dirty = this.#model.dirty;
    const writing = busy || model.remove.busy;

    return html`
      <section class="card save-bar" aria-label=${text(this, 'uiSaveSettingsLabel')}>
        <div class="buttons">
          <button
            type="button"
            class="save"
            aria-disabled=${writing ? 'true' : 'false'}
            aria-busy=${busy ? 'true' : 'false'}
            aria-describedby="save-help"
            @click=${() => void this.#model.save()}
          >
            ${busy ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : nothing}${text(this, 'uiSaveSettingsLabel')}
          </button>
          <button
            type="button"
            class="secondary"
            aria-disabled=${!dirty || writing ? 'true' : 'false'}
            @click=${() => (dirty ? this.#model.revert() : undefined)}
          >
            ${text(this, 'uiDiscardLabel')}
          </button>
          ${dirty ? html`<span class="status-line"><uui-icon name="icon-edit" aria-hidden="true"></uui-icon>${text(this, 'uiUnsavedText')}</span>` : nothing}
        </div>
        <p class="help muted" id="save-help">${text(this, 'uiSaveHelpText')}</p>
        <p class="status-line" role="status" aria-live="polite">
          ${busy
            ? html`<uui-loader-circle aria-hidden="true"></uui-loader-circle>${text(this, 'uiSavingText')}`
            : done
              ? html`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${text(this, 'uiSettingsSavedText')}`
              : nothing}
        </p>
        ${problem
          ? html`<esatto-seo-report-problem .error=${problem} .extraArgs=${{ email }} @retry=${() => void this.#model.save()}></esatto-seo-report-problem>`
          : nothing}
      </section>
    `;
  }

  static override styles = [
    shared,
    editorStyles,
    settingsStyles,
    css`
      .intro { max-width: 80ch; line-height: 1.55; }
      .legend { font-size: 13px; font-weight: 600; line-height: 1.5; }
      .loading { display: flex; align-items: center; gap: var(--uui-size-space-3); }
      h4 { font-size: 14px; font-weight: 700; }
      .save-bar { display: grid; gap: var(--uui-size-space-3); }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-settings': EsattoSeoReportSettingsElement;
  }
}
