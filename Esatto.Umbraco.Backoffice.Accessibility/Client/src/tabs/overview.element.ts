// Overview tab (mockup A): the score first, then the four WCAG principles, then scan history.
// "Scan now" runs the scan here, in the editor's browser (scan/scan-controller.ts).

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { finishScan, getDone, getOverview, getPages, postPage, startScan } from '../api';
import { axeTags } from '../axe-tags';
import { shared } from '../dashboard.styles';
import { band, bandLabel, formatDate, notVerifiedParts, scanStatusLabel, targetLabel } from '../format';
import { runInFrame } from '../scan/frame-runner';
import type { Progress } from '../scan/queue';
import { otherOriginsMessage } from '../scan/partition';
import { ScanController, scanOptions, type ScanRunResult } from '../scan/scan-controller';
import type { Overview, ScanSummary, SettingsResponse } from '../types';
import { navigate } from '../navigate';

const AXE_URL = '/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js';
const RING = { size: 168, stroke: 14 };

/** Throws the API error so the scan controller can retry / pause on it. */
async function unwrap<T>(call: Promise<{ data?: T; error?: unknown }>): Promise<T> {
  const { data, error } = await call;
  if (error) throw error instanceof Error ? error : new Error(typeof error === 'string' ? error : 'Request failed');
  return data as T;
}

@customElement('esatto-a11y-overview')
export class EsattoA11yOverviewElement extends UmbLitElement {
  @property({ attribute: false }) siteKey = '';
  @property({ attribute: false }) settings: SettingsResponse | null = null;

  @state() private _overview: Overview | null = null;
  @state() private _error: string | null = null;
  @state() private _progress: Progress | null = null;
  @state() private _run: ScanRunResult | null = null;
  @state() private _scanError: string | null = null;

  #controller: ScanController | null = null;

  override disconnectedCallback() {
    super.disconnectedCallback();
    // The frames live in this element; once it's gone they can't load. Stop starting pages, so the scan
    // ends as "incomplete" (resumable) instead of a run of timeouts.
    this.#controller?.cancel();
  }

  override updated(changed: Map<string, unknown>) {
    if (changed.has('siteKey') && this.siteKey) this.#load();
  }

  async #load() {
    const { data, error } = await tryExecute(this, getOverview(this.siteKey), { disableNotifications: true });
    if (error || !data) {
      this._error = 'Could not load the accessibility overview.';
      return;
    }
    this._error = null;
    this._overview = data;
  }

  async #scan(resume?: ScanSummary) {
    if (!this.settings) return;
    const s = this.settings.settings;
    const tags = axeTags(s.version, s.level, s.scan.bestPractice);
    const host = this.renderRoot.querySelector<HTMLElement>('#frames')!;
    this.#controller = new ScanController({
      origin: location.origin,
      getPages: (site) => unwrap(getPages(site)),
      startScan: async (site, mobile, total) => (await unwrap(startScan(site, mobile, total))).scanId,
      getDone: (id) => unwrap(getDone(id)),
      postPage: async (id, result) => { await unwrap(postPage(id, result)); },
      finishScan: (id, complete) => unwrap(finishScan(id, complete)),
      runJob: (job) => runInFrame(job, host, { axeUrl: AXE_URL, tags, settleMs: 1000, timeoutMs: 30000 }),
    });
    this._run = null;
    this._scanError = null;
    this._progress = { done: 0, total: 0, current: [] };
    try {
      this._run = await this.#controller.start(this.siteKey, scanOptions(s.scan.mobileWidth, resume), (p) => (this._progress = p));
    } catch (error) {
      this._scanError = error instanceof Error ? error.message : 'The scan could not start.';
    } finally {
      this._progress = null;
      this.#controller = null;
      await this.#load();
    }
  }

  #cancel() {
    this.#controller?.cancel();
  }

  override render() {
    if (this._error) return html`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._overview) return html`<uui-loader></uui-loader>`;
    const o = this._overview;
    return html`
      <div class="stack">
        <uui-box>
          <div class="hero">
            ${this.#ring(o)}
            <div class="stack">
              ${this.#targetLine(o)}
              ${this.#lastScanLine(o)}
              ${this.#scanControls(o)}
            </div>
          </div>
          ${this.#progressView()}
          ${this.#runMessages()}
        </uui-box>

        <section aria-labelledby="principles-heading">
          <h3 id="principles-heading" class="section-title">By principle</h3>
          <ul class="tiles">
            ${o.principles.map((p) => html`
              <li>
                <button class="tile" @click=${() => navigate(this, 'issues', { principle: p.principle })}>
                  <span class="tile-label">${p.principle}. ${p.name}</span>
                  <span class="tile-value">${p.fail} failing</span>
                  <span class="muted">${p.toCheck} to check</span>
                </button>
              </li>`)}
          </ul>
          <p>
            Automated: ${o.score.automatedPass} criteria pass, ${o.score.automatedFail} fail ·
            Manual: ${o.manualChecked} of ${o.manualTotal} checked
            <uui-button look="secondary" compact label="Open manual checklist" @click=${() => navigate(this, 'checklist')}></uui-button>
          </p>
        </section>

        ${o.history.length ? html`
          <uui-box headline="Recent scans">
            <table>
              <caption class="visually-hidden">The last ${o.history.length} scans</caption>
              <thead><tr><th scope="col">Started</th><th scope="col">Target</th><th scope="col" class="num">Score</th><th scope="col" class="num">Pages</th><th scope="col">Status</th><th scope="col">By</th></tr></thead>
              <tbody>
                ${o.history.map((s) => html`<tr>
                  <td>${formatDate(s.startedUtc)}</td>
                  <td>${s.target}</td>
                  <td class="num">${s.score === null ? '–' : `${s.score} %`}</td>
                  <td class="num">${s.pagesDone}${s.pagesSkipped ? html` <span class="muted">(+${s.pagesSkipped} skipped)</span>` : nothing}</td>
                  <td>${scanStatusLabel(s.status)}</td>
                  <td>${s.userName}</td>
                </tr>`)}
              </tbody>
            </table>
          </uui-box>` : nothing}
        <div id="frames" aria-hidden="true"></div>
      </div>`;
  }

  #ring(o: Overview) {
    const percent = o.score.percent;
    const b = band(percent);
    const r = (RING.size - RING.stroke) / 2;
    const circumference = 2 * Math.PI * r;
    const filled = ((percent ?? 0) / 100) * circumference;
    const parts = notVerifiedParts(o.score);
    const label = percent === null ? 'No score yet' : `Score ${percent} percent, ${targetLabel(o.target)}, ${bandLabel[b!]}`;
    return html`
      <figure class="ring ${b ?? 'none'}" role="img" aria-label=${label}>
        <svg width=${RING.size} height=${RING.size} viewBox="0 0 ${RING.size} ${RING.size}" aria-hidden="true">
          <circle class="track" cx=${RING.size / 2} cy=${RING.size / 2} r=${r} stroke-width=${RING.stroke} fill="none"></circle>
          <circle class="fill" cx=${RING.size / 2} cy=${RING.size / 2} r=${r} stroke-width=${RING.stroke} fill="none"
            stroke-dasharray="${filled} ${circumference}" stroke-linecap="round"
            transform="rotate(-90 ${RING.size / 2} ${RING.size / 2})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true">
          <span class="ring-value">${percent === null ? '–' : `${percent}%`}</span>
          <span class="ring-target">${targetLabel(o.target)}</span>
        </div>
        <figcaption aria-hidden="true" class="ring-band">
          ${b ? html`<uui-icon name=${b === 'good' ? 'icon-check' : b === 'warning' ? 'icon-alert' : 'icon-wrong'}></uui-icon> ${bandLabel[b]}` : 'Not scanned yet'}
        </figcaption>
      </figure>
      <div class="ring-facts">
        <span><strong>${o.score.pass}</strong> pass</span>
        <span><strong>${o.score.fail}</strong> fail</span>
        <span><strong>${o.score.notVerified}</strong> not verified:</span>
        <span class="sub"><strong>${parts.toConfirm}</strong> checked by the scanner, confirm by hand</span>
        <span class="sub"><strong>${parts.personOnly}</strong> only a person can check</span>
        ${o.score.notApplicable ? html`<span><strong>${o.score.notApplicable}</strong> not applicable</span>` : nothing}
        <details>
          <summary>Why isn't this 100 %?</summary>
          <p>A criterion only counts as passed when someone has confirmed it in the manual checklist, or when the scanner can check it completely (very few can). Automated tests find only part of the problems, so a clean scan alone never scores 100 %.</p>
          <p>“Checked by the scanner” means its rules for the criterion found nothing wrong, but they test only the mechanical part (that an image has alt text, not that the text describes it). Start with those: they are usually quick to confirm. The rest, such as captions, keyboard use and reading order, only a person can check.</p>
        </details>
      </div>`;
  }

  #targetLine(o: Overview) {
    return html`<p>Target: <strong>${targetLabel(o.target)}</strong></p>`;
  }

  #lastScanLine(o: Overview) {
    const s = o.lastScan;
    if (!s) return html`<p class="muted">This site hasn't been scanned yet.</p>`;
    return html`<p>
      Last scan: ${formatDate(s.startedUtc)} · ${s.pagesDone} ${s.pagesDone === 1 ? 'page' : 'pages'}${s.pagesSkipped ? ` (${s.pagesSkipped} skipped)` : ''}${s.pagesFailed ? ` · ${s.pagesFailed} failed` : ''} · by ${s.userName}
      ${s.status !== 'complete' ? html` · <span class="tag">${scanStatusLabel(s.status)}</span>` : nothing}
    </p>`;
  }

  #scanControls(o: Overview) {
    const scanning = this._progress !== null;
    const resumable = o.lastScan && o.lastScan.status !== 'complete' ? o.lastScan : null;
    return html`<div class="row">
      <uui-button look="primary" color="positive" label="Scan now" ?disabled=${scanning} @click=${() => this.#scan()}></uui-button>
      ${resumable && !scanning
        ? html`<uui-button look="secondary" label="Resume last scan" @click=${() => this.#scan(resumable)}></uui-button>`
        : nothing}
      ${scanning ? html`<uui-button look="secondary" label="Cancel scan" @click=${this.#cancel}></uui-button>` : nothing}
    </div>
    <p class="muted small">Pages load in hidden frames in your browser, so keep this dashboard open while scanning. Browsers slow down background tabs: a scan runs fastest while this tab is in front. Leaving the dashboard stops the scan; “Resume last scan” continues it.</p>`;
  }

  #progressView() {
    const p = this._progress;
    if (!p) return nothing;
    const current = p.current[0] ? new URL(p.current[0]).pathname : '';
    return html`
      <div class="progress">
        <uui-loader-bar progress=${p.total ? Math.round((p.done / p.total) * 100) : 0}></uui-loader-bar>
        <p role="status">${p.total
          ? `Scanning page ${Math.min(p.done + 1, p.total)} of ${p.total}${current ? `: ${current}` : ''}`
          : 'Getting the list of pages…'}</p>
      </div>`;
  }

  #runMessages() {
    const run = this._run;
    return html`
      ${this._scanError ? html`<p class="error" role="alert">${this._scanError}</p>` : nothing}
      ${run?.blocked ? html`
        <div class="notice" role="alert">
          <h4>Pages can't be loaded for scanning</h4>
          <p>${run.blocked.reason ?? run.blocked.message}</p>
          <p>The scan loads each page in a frame on this site and runs axe-core in it, so the site must allow same-origin framing
          (<code>X-Frame-Options: SAMEORIGIN</code> or <code>Content-Security-Policy: frame-ancestors 'self'</code>, not DENY / 'none')
          and scripts from its own origin (<code>script-src 'self'</code>).</p>
        </div>` : nothing}
      ${run?.paused ? html`
        <div class="notice" role="alert">
          <h4>The scan was paused</h4>
          <p>Results couldn't be saved (${run.paused.message}). Everything up to now is kept; use “Resume last scan” to continue.</p>
        </div>` : nothing}
      ${run?.mostlyFailed ? html`
        <div class="notice" role="alert">
          <h4>Most pages couldn't be scanned</h4>
          <p>The scan was saved as incomplete so it doesn't replace your last full report. Check that the site is up, then use “Resume last scan” or scan again. Failed pages and reasons are listed in each page result.</p>
        </div>` : nothing}
      ${run?.otherOrigins.length ? html`<p class="muted">${otherOriginsMessage(run.otherOrigins, location.origin)}</p>` : nothing}`;
  }

  static override styles = [shared, css`
    .hero { display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; }
    .ring { position: relative; margin: 0; width: ${RING.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-2); }
    .ring svg { display: block; }
    .ring .track { stroke: var(--uui-color-surface-alt); }
    .ring.good .fill { stroke: var(--a11y-good); }
    .ring.good .track { stroke: color-mix(in srgb, var(--a11y-good) 22%, transparent); }
    .ring.warning .fill { stroke: var(--a11y-warning); }
    .ring.warning .track { stroke: color-mix(in srgb, var(--a11y-warning) 22%, transparent); }
    .ring.critical .fill { stroke: var(--a11y-critical); }
    .ring.critical .track { stroke: color-mix(in srgb, var(--a11y-critical) 22%, transparent); }
    .ring-text { position: absolute; top: 0; height: ${RING.size}px; display: grid; place-content: center; text-align: center; }
    .ring-value { font-size: 48px; font-weight: 600; line-height: 1; }
    .ring-target { color: var(--uui-color-text-alt); font-size: 0.85rem; margin-top: 4px; }
    .ring-band { display: flex; gap: 4px; align-items: center; font-weight: 600; }
    .ring-facts { display: grid; gap: 2px; min-width: 12rem; }
    .ring-facts .sub { padding-left: var(--uui-size-space-4); }
    .ring-facts details { margin-top: var(--uui-size-space-2); max-width: 28rem; }
    .small { font-size: 0.85rem; line-height: 1.5; margin: var(--uui-size-space-5) 0 0; max-width: 40rem; }
    .progress { margin-top: var(--uui-size-space-4); }
    .notice { margin-top: var(--uui-size-space-4); padding: var(--uui-size-space-4); border-left: 4px solid var(--uui-color-danger); background: var(--uui-color-surface-alt); }
    .section-title { margin-bottom: var(--uui-size-space-3); }
    .tiles { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); }
    .tile {
      width: 100%; min-height: 44px; display: grid; gap: 4px; text-align: left; cursor: pointer;
      padding: var(--uui-size-space-5); border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text); font: inherit;
    }
    .tile:hover { border-color: var(--uui-color-border-emphasis); }
    .tile-label { color: var(--uui-color-text-alt); }
    .tile-value { font-size: 1.25rem; font-weight: 600; }
    #frames { position: relative; height: 0; overflow: hidden; }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-overview': EsattoA11yOverviewElement;
  }
}
