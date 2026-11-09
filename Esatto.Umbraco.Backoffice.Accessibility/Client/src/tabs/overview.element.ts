// Overview tab (mockup A): the score first, then the four WCAG principles, then scan history.
// "Scan now" runs the scan here, in the editor's browser (scan/scan-controller.ts).

import { css, customElement, html, nothing, property, state, styleMap } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { finishScan, getDone, getOverview, getPages, postPage, startScan } from '../api';
import { axeTags } from '../axe-tags';
import { shared } from '../dashboard.styles';
import { band, bandIcon, bandLabel, formatDate, notVerifiedParts, scanStatusLabel, targetLabel, type Band } from '../format';
import { runInFrame } from '../scan/frame-runner';
import type { Progress } from '../scan/queue';
import { otherOriginsMessage } from '../scan/partition';
import { ScanController, scanOptions, type ScanRunResult } from '../scan/scan-controller';
import type { Overview, ScanSummary, SettingsResponse } from '../types';
import { navigate } from '../navigate';

const AXE_URL = '/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js';
const RING = { size: 168, stroke: 14 };

/** One icon per WCAG principle, by its number. */
const PRINCIPLE_ICONS: Record<number, string> = { 1: 'icon-eye', 2: 'icon-keyboard', 3: 'icon-lightbulb-active', 4: 'icon-code' };

/** Scan status as a badge colour; a scan that isn't finished stays neutral. */
const STATUS_TONE: Record<ScanSummary['status'], string> = { complete: 'tone-good', incomplete: 'tone-warning', running: '' };

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
    const b = band(o.score.percent);
    return html`
      <div class="page">
        <section class="card hero-card tone-${b ?? 'none'}" aria-labelledby="hero-heading">
          <div class="hero">
            ${this.#ring(o, b)}
            ${this.#facts(o)}
            <div class="hero-body stack">
              <div>
                <p class="eyebrow">Accessibility score</p>
                <h3 id="hero-heading" class="hero-title">${b ? bandLabel[b] : 'Not scanned yet'}</h3>
              </div>
              ${this.#lastScanLine(o)}
              ${this.#scanControls(o)}
            </div>
          </div>
          ${this.#progressView()}
        </section>
        ${this.#runMessages()}

        <section aria-labelledby="principles-heading">
          <h3 id="principles-heading" class="section-label">By principle</h3>
          <ul class="tiles">
            ${o.principles.map((p) => {
              const tone = p.fail ? 'critical' : p.toCheck ? 'notice' : 'good';
              return html`<li>
                <button class="tile lift tone-${tone}" @click=${() => navigate(this, 'issues', { principle: p.principle })}>
                  <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${PRINCIPLE_ICONS[p.principle] ?? 'icon-checkbox'}></uui-icon></span>
                  <span class="tile-text">
                    <span class="tile-label">${p.principle}. ${p.name}</span>
                    <span class="tile-value">${p.fail} failing</span>
                    <span class="muted small">${p.toCheck} to check</span>
                  </span>
                  <uui-icon class="tile-go" name="icon-arrow-right" aria-hidden="true"></uui-icon>
                </button>
              </li>`;
            })}
          </ul>
        </section>

        <section class="card results" aria-label="Automated and manual results">
          <div class="result">
            <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-wand"></uui-icon></span>
            <div>
              <p class="result-label">Automated checks</p>
              <p><strong>${o.score.automatedPass}</strong> criteria pass · <strong>${o.score.automatedFail}</strong> fail</p>
            </div>
          </div>
          <div class="result">
            <span class="icon-badge tone-good" aria-hidden="true"><uui-icon name="icon-checkbox"></uui-icon></span>
            <div class="grow">
              <p class="result-label">Manual checklist</p>
              <p><strong>${o.manualChecked}</strong> of ${o.manualTotal} checked</p>
              <div class="progress-bar" aria-hidden="true"><span style=${styleMap({ width: `${o.manualTotal ? Math.round((o.manualChecked / o.manualTotal) * 100) : 0}%` })}></span></div>
            </div>
          </div>
          <uui-button look="secondary" label="Open manual checklist" @click=${() => navigate(this, 'checklist')}>
            Open manual checklist&nbsp;<uui-icon name="icon-arrow-right" aria-hidden="true"></uui-icon>
          </uui-button>
        </section>

        ${o.history.length ? html`
          <section class="card" aria-labelledby="history-heading">
            <div class="card-head">
              <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
              <h3 id="history-heading">Recent scans</h3>
            </div>
            <div class="table-wrap">
              <table>
                <caption class="visually-hidden">The last ${o.history.length} scans</caption>
                <thead><tr><th scope="col">Started</th><th scope="col">Target</th><th scope="col">Score</th><th scope="col" class="num">Pages</th><th scope="col">Status</th><th scope="col">By</th></tr></thead>
                <tbody>
                  ${o.history.map((s) => html`<tr>
                    <td>${formatDate(s.startedUtc)}</td>
                    <td>${s.target}</td>
                    <td>${s.score === null ? html`<span class="muted">–</span>` : html`
                      <span class="score-cell tone-${band(s.score)}">
                        <span class="score-bar" aria-hidden="true"><span style=${styleMap({ width: `${s.score}%` })}></span></span>
                        <span class="score-num">${s.score} %</span>
                      </span>`}</td>
                    <td class="num">${s.pagesDone}${s.pagesSkipped ? html` <span class="muted">(+${s.pagesSkipped} skipped)</span>` : nothing}</td>
                    <td><span class="tag ${STATUS_TONE[s.status]}">${scanStatusLabel(s.status)}</span></td>
                    <td>${s.userName}</td>
                  </tr>`)}
                </tbody>
              </table>
            </div>
          </section>` : nothing}
        <div id="frames" aria-hidden="true"></div>
      </div>`;
  }

  #ring(o: Overview, b: Band | null) {
    const percent = o.score.percent;
    const r = (RING.size - RING.stroke) / 2;
    const circumference = 2 * Math.PI * r;
    const filled = ((percent ?? 0) / 100) * circumference;
    const label = percent === null ? 'No score yet' : `Score ${percent} percent, ${targetLabel(o.target)}, ${bandLabel[b!]}`;
    const c = RING.size / 2;
    return html`
      <figure class="ring" role="img" aria-label=${label}>
        <svg width=${RING.size} height=${RING.size} viewBox="0 0 ${RING.size} ${RING.size}" aria-hidden="true">
          <defs>
            <linearGradient id="ring-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" class="stop-a"></stop>
              <stop offset="100%" class="stop-b"></stop>
            </linearGradient>
          </defs>
          <circle class="track" cx=${c} cy=${c} r=${r} stroke-width=${RING.stroke} fill="none"></circle>
          <circle class="fill ${!percent ? 'empty' : ''}" cx=${c} cy=${c} r=${r} stroke-width=${RING.stroke} fill="none" stroke="url(#ring-gradient)"
            stroke-dasharray="${filled} ${circumference}" stroke-linecap="round" transform="rotate(-90 ${c} ${c})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true">
          <span class="ring-value">${percent === null ? '–' : `${percent}%`}</span>
          <span class="muted small">${targetLabel(o.target)}</span>
        </div>
        <figcaption aria-hidden="true">
          ${b ? html`<span class="tag tone-${b}"><uui-icon name=${bandIcon[b]}></uui-icon>${bandLabel[b]}</span>` : html`<span class="tag">Not scanned yet</span>`}
        </figcaption>
      </figure>`;
  }

  #facts(o: Overview) {
    const parts = notVerifiedParts(o.score);
    return html`
      <div class="facts">
        <ul class="fact-list">
          <li><span class="dot tone-good" aria-hidden="true"></span><strong>${o.score.pass}</strong> pass</li>
          <li><span class="dot tone-critical" aria-hidden="true"></span><strong>${o.score.fail}</strong> fail</li>
          <li><span class="dot tone-none" aria-hidden="true"></span><strong>${o.score.notVerified}</strong> not verified:</li>
          <li class="sub"><strong>${parts.toConfirm}</strong> checked by the scanner, confirm by hand</li>
          <li class="sub"><strong>${parts.personOnly}</strong> only a person can check</li>
          ${o.score.notApplicable ? html`<li><span class="dot" aria-hidden="true"></span><strong>${o.score.notApplicable}</strong> not applicable</li>` : nothing}
        </ul>
        <details class="disclosure why">
          <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>Why isn't this 100 %?</summary>
          <p>A criterion only counts as passed when someone has confirmed it in the manual checklist, or when the scanner can check it completely (very few can). Automated tests find only part of the problems, so a clean scan alone never scores 100 %.</p>
          <p>“Checked by the scanner” means its rules for the criterion found nothing wrong, but they test only the mechanical part (that an image has alt text, not that the text describes it). Start with those: they are usually quick to confirm. The rest, such as captions, keyboard use and reading order, only a person can check.</p>
        </details>
      </div>`;
  }

  #lastScanLine(o: Overview) {
    const s = o.lastScan;
    return html`
      <ul class="meta" aria-label="Last scan">
        <li class="chip"><uui-icon name="icon-shield" aria-hidden="true"></uui-icon>Target ${targetLabel(o.target)}</li>
        ${s ? html`
          <li class="chip"><uui-icon name="icon-calendar" aria-hidden="true"></uui-icon>${formatDate(s.startedUtc)}</li>
          <li class="chip"><uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${s.pagesDone} ${s.pagesDone === 1 ? 'page' : 'pages'}${s.pagesSkipped ? ` · ${s.pagesSkipped} skipped` : ''}</li>
          ${s.pagesFailed ? html`<li class="chip"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${s.pagesFailed} failed</li>` : nothing}
          <li class="chip"><uui-icon name="icon-user" aria-hidden="true"></uui-icon>By ${s.userName}</li>
          ${s.status !== 'complete' ? html`<li class="tag ${STATUS_TONE[s.status]}">${scanStatusLabel(s.status)}</li>` : nothing}` : nothing}
      </ul>
      ${s ? nothing : html`<p class="muted">This site hasn't been scanned yet.</p>`}`;
  }

  #scanControls(o: Overview) {
    const scanning = this._progress !== null;
    const resumable = o.lastScan && o.lastScan.status !== 'complete' ? o.lastScan : null;
    return html`<div class="row">
      <uui-button look="primary" label="Scan now" ?disabled=${scanning} @click=${() => this.#scan()}>
        <uui-icon name="icon-sync" aria-hidden="true"></uui-icon>&nbsp;Scan now
      </uui-button>
      ${resumable && !scanning
        ? html`<uui-button look="secondary" label="Resume last scan" @click=${() => this.#scan(resumable)}></uui-button>`
        : nothing}
      ${scanning ? html`<uui-button look="secondary" label="Cancel scan" @click=${this.#cancel}></uui-button>` : nothing}
    </div>
    <p class="muted small help">Pages load in hidden frames in your browser, so keep this dashboard open while scanning. Browsers slow down background tabs: a scan runs fastest while this tab is in front. Leaving the dashboard stops the scan; “Resume last scan” continues it.</p>`;
  }

  #progressView() {
    const p = this._progress;
    if (!p) return nothing;
    const current = p.current[0] ? new URL(p.current[0]).pathname : '';
    return html`
      <div class="progress">
        <div class="progress-bar" aria-hidden="true"><span style=${styleMap({ width: `${p.total ? Math.round((p.done / p.total) * 100) : 0}%` })}></span></div>
        <p role="status">${p.total
          ? `Scanning page ${Math.min(p.done + 1, p.total)} of ${p.total}${current ? `: ${current}` : ''}`
          : 'Getting the list of pages…'}</p>
      </div>`;
  }

  #notice(title: string, body: unknown) {
    return html`
      <div class="banner tone-critical" role="alert">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
        <div><h4>${title}</h4>${body}</div>
      </div>`;
  }

  #runMessages() {
    const run = this._run;
    return html`
      ${this._scanError ? html`<p class="error" role="alert">${this._scanError}</p>` : nothing}
      ${run?.blocked ? this.#notice("Pages can't be loaded for scanning", html`
        <p>${run.blocked.reason ?? run.blocked.message}</p>
        <p>The scan loads each page in a frame on this site and runs axe-core in it, so the site must allow same-origin framing
        (<code>X-Frame-Options: SAMEORIGIN</code> or <code>Content-Security-Policy: frame-ancestors 'self'</code>, not DENY / 'none')
        and scripts from its own origin (<code>script-src 'self'</code>).</p>`) : nothing}
      ${run?.paused ? this.#notice('The scan was paused', html`
        <p>Results couldn't be saved (${run.paused.message}). Everything up to now is kept; use “Resume last scan” to continue.</p>`) : nothing}
      ${run?.mostlyFailed ? this.#notice("Most pages couldn't be scanned", html`
        <p>The scan was saved as incomplete so it doesn't replace your last full report. Check that the site is up, then use “Resume last scan” or scan again. Failed pages and reasons are listed in each page result.</p>`) : nothing}
      ${run?.otherOrigins.length ? html`<p class="muted">${otherOriginsMessage(run.otherOrigins, location.origin)}</p>` : nothing}`;
  }

  static override styles = [shared, css`
    .hero-card {
      padding: var(--uui-size-space-6);
      background: radial-gradient(120% 160% at 0% 0%, color-mix(in srgb, var(--tone) 16%, var(--uui-color-surface)) 0%, var(--uui-color-surface) 62%);
      border-color: color-mix(in srgb, var(--tone) 30%, var(--a11y-line));
    }
    .hero-card.tone-good { --ring-a: #4ade80; --ring-b: var(--a11y-good); }
    .hero-card.tone-warning { --ring-a: #fcd34d; --ring-b: #ea7c0c; }
    .hero-card.tone-critical { --ring-a: #fb7185; --ring-b: var(--a11y-critical); }
    /* Columns have fixed shares and align to the top, so opening "Why isn't this 100 %?" only makes the middle column
       taller: it never widens it (which wrapped the scan column underneath) or re-centres the ring. */
    .hero { display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: flex-start; }
    .hero-body { flex: 1 1 20rem; min-width: 0; }
    .eyebrow { font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--a11y-muted); }
    .hero-title { font-size: 28px; font-weight: 800; letter-spacing: -0.015em; line-height: 1.2; margin-top: 2px; }
    .ring { position: relative; margin: 0; width: ${RING.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-3); }
    .ring svg { display: block; filter: drop-shadow(0 8px 18px color-mix(in srgb, var(--tone) 30%, transparent)); }
    .ring .track { stroke: color-mix(in srgb, var(--tone) 24%, var(--uui-color-surface)); }
    .ring .fill.empty { display: none; }
    .stop-a { stop-color: var(--ring-a, var(--tone)); }
    .stop-b { stop-color: var(--ring-b, var(--tone)); }
    .ring-text { position: absolute; top: 0; height: ${RING.size}px; display: grid; place-content: center; text-align: center; gap: 2px; }
    .ring-value { font-size: 48px; font-weight: 800; line-height: 1; letter-spacing: -0.03em; font-variant-numeric: tabular-nums; }

    .facts { flex: 0 1 26rem; min-width: 12rem; display: grid; gap: var(--uui-size-space-3); }
    .fact-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
    .fact-list li { display: flex; gap: var(--uui-size-space-2); align-items: center; }
    .fact-list strong { font-variant-numeric: tabular-nums; }
    .fact-list .sub { padding-left: calc(10px + var(--uui-size-space-2) + var(--uui-size-space-4)); }
    .dot { flex: none; width: 10px; height: 10px; border-radius: 50%; background: var(--tone, var(--a11y-line)); }
    .why > p { margin-top: var(--uui-size-space-3); color: var(--a11y-muted); }
    .meta { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
    .help { max-width: 40rem; line-height: 1.5; }
    .progress { display: grid; gap: var(--uui-size-space-2); margin-top: var(--uui-size-space-5); }
    .banner h4 { font-size: 15px; }
    .banner code { white-space: nowrap; }

    .tiles { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--a11y-gap); grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); }
    .tile {
      font: inherit; color: inherit; text-align: left; cursor: pointer; width: 100%;
      display: flex; gap: var(--uui-size-space-4); align-items: center; min-height: 96px; padding: var(--uui-size-space-4) var(--uui-size-space-5);
      border-radius: var(--a11y-radius); border: 1px solid color-mix(in srgb, var(--tone) 35%, var(--a11y-line));
      background: linear-gradient(135deg, color-mix(in srgb, var(--tone) 14%, var(--uui-color-surface)), var(--uui-color-surface) 78%);
      box-shadow: var(--a11y-shadow);
    }
    .tile:hover { border-color: var(--tone); }
    .tile .icon-badge { width: 44px; height: 44px; font-size: 22px; box-shadow: 0 6px 14px -6px var(--tone); }
    .tile-text { display: grid; flex: 1; gap: 2px; }
    .tile-label { font-size: 13px; font-weight: 700; color: var(--a11y-muted); }
    .tile-value { font-size: 22px; font-weight: 800; line-height: 1.15; letter-spacing: -0.01em; }
    .tile-go { color: var(--a11y-muted); transition: transform var(--a11y-ease); }
    .tile:hover .tile-go { transform: translateX(3px); }

    .results { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-5) var(--uui-size-layout-2); align-items: center; }
    .result { display: flex; gap: var(--uui-size-space-3); align-items: center; }
    .result .grow { display: grid; gap: 4px; min-width: 14rem; }
    .result-label { font-size: 12px; font-weight: 700; letter-spacing: 0.06em; text-transform: uppercase; color: var(--a11y-muted); }
    .results > uui-button { margin-left: auto; }

    .score-cell { display: flex; gap: var(--uui-size-space-3); align-items: center; min-width: 10rem; max-width: 16rem; }
    .score-bar { flex: 1; height: 8px; border-radius: 999px; background: color-mix(in srgb, var(--tone) 15%, var(--uui-color-surface)); overflow: hidden; }
    .score-bar > span { display: block; height: 100%; border-radius: inherit; background: var(--tone); }
    .score-num { font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; }
    #frames { position: relative; height: 0; overflow: hidden; }
  `];
}
declare global {
  interface HTMLElementTagNameMap {
    'esatto-a11y-overview': EsattoA11yOverviewElement;
  }
}
