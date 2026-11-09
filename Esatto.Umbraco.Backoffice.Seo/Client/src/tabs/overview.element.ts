// Overview tab: the score and its trend, "Fix these first", counts by severity, the whole-site checklist,
// pages hidden from search, and Scan now. Scans run on the server; this tab polls their progress.

import { css, customElement, html, nothing, property, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { cancelScan, getOverview, getProgress, startScan } from '../api';
import { shared } from '../dashboard.styles';
import { band, bandLabel, editContentUrl, formatDate, ownerLabel, plural, safeHref, severityIcon, severityLabel, skipLabel } from '../format';
import { navigate } from '../navigate';
import type { Knowledge, Overview, PageRow, ScanProgress, Severity } from '../types';

const RING = { size: 168, stroke: 14 };
const POLL_MS = 2000;

@customElement('esatto-seo-overview')
export class EsattoSeoOverviewElement extends UmbLitElement {
  @property({ attribute: false }) siteKey = '';
  @property({ attribute: false }) knowledge: Knowledge | null = null;

  @state() private _overview: Overview | null = null;
  @state() private _progress: ScanProgress | null = null;
  @state() private _error: string | null = null;
  @state() private _starting = false;

  #timer: number | undefined;

  override updated(changed: Map<string, unknown>) {
    if (changed.has('siteKey') && this.siteKey) this.#load();
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    window.clearTimeout(this.#timer);
  }

  async #load() {
    const { data, error } = await tryExecute(this, getOverview(this.siteKey), { disableNotifications: true });
    if (error || !data) {
      this._error = 'Could not load the overview.';
      return;
    }
    this._error = null;
    this._overview = data;
    this._progress = data.running;
    if (data.running) this.#poll();
  }

  #poll() {
    window.clearTimeout(this.#timer);
    this.#timer = window.setTimeout(async () => {
      const { data, response } = await tryExecute(this, getProgress(this.siteKey), { disableNotifications: true });
      if (response?.status === 200 && data) {
        this._progress = data as ScanProgress;
        this.#poll();
      } else {
        this._progress = null;
        await this.#load();
      }
    }, POLL_MS);
  }

  async #scan() {
    this._starting = true;
    const { error } = await tryExecute(this, startScan(this.siteKey));
    this._starting = false;
    if (error) return;
    this._progress = { scanId: 0, done: 0, total: 0, currentUrl: null };
    this.#poll();
  }

  async #cancel() {
    await tryExecute(this, cancelScan(this.siteKey), { disableNotifications: true });
  }

  #title(checkId: string) {
    return this.knowledge?.texts[checkId]?.title ?? checkId;
  }

  override render() {
    if (this._error) return html`<uui-box><p class="error">${this._error}</p></uui-box>`;
    const o = this._overview;
    if (!o) return html`<uui-loader></uui-loader>`;
    return html`
      <div class="stack">
        ${this.#banners(o)}
        <uui-box>
          <div class="hero">
            ${this.#ring(o.score)}
            <div class="stack">
              ${this.#lastScanLine(o)}
              ${this.#scanControls()}
              <div aria-live="polite" class="progress">${this.#progressView()}</div>
              <details>
                <summary>How is the score worked out?</summary>
                <p>Each page that search engines may index scores 0–100: zero if it has a critical problem, otherwise the share of checks it passes (critical checks weigh 10, warnings 3, notices 1). The site score is the average, reduced by any failed whole-site check. Ignored findings don't count.</p>
              </details>
            </div>
          </div>
        </uui-box>
        ${o.lastScan ? this.#results(o) : nothing}
      </div>`;
  }

  #banners(o: Overview) {
    const attempt = o.latestAttempt;
    return html`
      ${o.siteHidden && !o.intentionallyHidden ? html`
        <div class="banner critical" role="alert"><uui-icon name="icon-alert"></uui-icon><div>
          <strong>The whole site is hidden from search engines.</strong>
          <p>That's expected on a Test or Dev environment. On the live site it's the most serious problem there is. To mark this environment as hidden on purpose, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.</p>
        </div></div>` : nothing}
      ${o.siteHidden && o.intentionallyHidden ? html`
        <div class="banner info"><uui-icon name="icon-info"></uui-icon><div>
          <strong>This environment is hidden from search engines on purpose.</strong>
          <p>Pages are checked as if they were public, so content problems still show up here before they reach the live site.</p>
        </div></div>` : nothing}
      ${attempt && attempt.status !== 'complete' ? html`
        <div class="banner warning"><uui-icon name="icon-alert"></uui-icon><div>
          <strong>The latest scan (${formatDate(attempt.startedUtc)}) ${attempt.status === 'cancelled' ? 'was cancelled' : attempt.status === 'incomplete' ? 'was interrupted by a restart' : "couldn't run"}.</strong>
          ${attempt.message ? html`<p>${attempt.message}</p>` : nothing}
          ${o.lastScan ? html`<p class="muted">The results below are from ${formatDate(o.lastScan.startedUtc)}.</p>` : nothing}
        </div></div>` : nothing}`;
  }

  #ring(score: number | null) {
    const b = band(score);
    const r = (RING.size - RING.stroke) / 2;
    const circumference = 2 * Math.PI * r;
    const filled = ((score ?? 0) / 100) * circumference;
    const label = score === null ? 'No score yet' : `SEO score ${score} out of 100, ${bandLabel[b!]}`;
    return html`
      <figure class="ring ${b ?? 'none'}" role="img" aria-label=${label}>
        <svg width=${RING.size} height=${RING.size} viewBox="0 0 ${RING.size} ${RING.size}" aria-hidden="true">
          <circle class="track" cx=${RING.size / 2} cy=${RING.size / 2} r=${r} stroke-width=${RING.stroke} fill="none"></circle>
          <circle class="fill" cx=${RING.size / 2} cy=${RING.size / 2} r=${r} stroke-width=${RING.stroke} fill="none"
            stroke-dasharray="${filled} ${circumference}" stroke-linecap="round" transform="rotate(-90 ${RING.size / 2} ${RING.size / 2})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true"><span class="ring-value">${score ?? '–'}</span><span class="muted">of 100</span></div>
        <figcaption aria-hidden="true" class="ring-band">
          ${b ? html`<uui-icon name=${b === 'good' ? 'icon-check' : b === 'warning' ? 'icon-alert' : 'icon-wrong'}></uui-icon> ${bandLabel[b]}` : 'Not scanned yet'}
        </figcaption>
      </figure>`;
  }

  #lastScanLine(o: Overview) {
    const s = o.lastScan;
    if (!s) return html`<p class="muted">This site hasn't been scanned yet. Scans also run every night (see Settings).</p>`;
    return html`<p>
      Last scan: ${formatDate(s.startedUtc)} · ${plural(s.pagesDone, 'page', 'pages')}${s.pagesSkipped ? ` (${s.pagesSkipped} skipped)` : ''}${s.pagesFailed ? ` · ${s.pagesFailed} didn't load` : ''}
      · ${s.trigger === 'scheduled' ? 'nightly scan' : `by ${s.userName}`}</p>`;
  }

  #scanControls() {
    if (this._progress) {
      return html`<div class="row"><uui-button look="secondary" label="Cancel scan" @click=${this.#cancel}></uui-button></div>`;
    }
    return html`<div class="row">
      <uui-button look="primary" label="Scan now" .state=${this._starting ? 'waiting' : undefined} @click=${this.#scan}></uui-button>
      <span class="muted">Runs on the server: you can leave this page.</span>
    </div>`;
  }

  #progressView() {
    const p = this._progress;
    if (!p) return nothing;
    if (!p.total) return html`<p>Starting the scan…</p>`;
    return html`
      <uui-loader-bar progress=${Math.round((p.done / p.total) * 100)} aria-hidden="true"></uui-loader-bar>
      <p>Scanned ${p.done} of ${plural(p.total, 'page', 'pages')}${p.done === p.total ? ', checking links and the sitemap…' : ''}</p>
      ${p.currentUrl && p.done < p.total ? html`<p class="muted url">${p.currentUrl}</p>` : nothing}`;
  }

  #results(o: Overview) {
    const tiles: { severity: Severity; count: number }[] = [
      { severity: 'critical', count: o.counts.critical },
      { severity: 'warning', count: o.counts.warning },
      { severity: 'notice', count: o.counts.notice },
    ];
    return html`
      <section aria-labelledby="first-heading">
        <h3 id="first-heading" class="section-title">Fix these first</h3>
        ${o.top.length ? html`
          <ol class="top">
            ${o.top.map((t) => html`<li>
              <button class="top-item" @click=${() => navigate(this, 'issues', { checkId: t.checkId })}>
                <span class="tag sev-${t.severity}"><uui-icon name=${severityIcon[t.severity]}></uui-icon>${severityLabel[t.severity]}</span>
                <span class="top-title">${this.#title(t.checkId)}</span>
                <span class="muted">${t.pages ? plural(t.pages, 'page', 'pages') : 'whole site'} · ${ownerLabel[t.owner]}</span>
              </button>
            </li>`)}
          </ol>` : html`<p><uui-icon name="icon-check"></uui-icon> Nothing to fix. Well done.</p>`}
      </section>

      <section aria-labelledby="sev-heading">
        <h3 id="sev-heading" class="section-title">Open findings</h3>
        <ul class="tiles">
          ${tiles.map((t) => html`<li>
            <button class="tile sev-${t.severity}" @click=${() => navigate(this, 'issues', { severity: t.severity })}>
              <span class="tile-label"><uui-icon name=${severityIcon[t.severity]}></uui-icon> ${severityLabel[t.severity]}</span>
              <span class="tile-value">${t.count}</span>
            </button>
          </li>`)}
        </ul>
      </section>

      <uui-box headline="Whole site">
        <ul class="checklist">
          ${o.siteChecks.map((c) => html`<li>
            ${c.passed === null
              ? html`<uui-icon name="icon-remove" aria-hidden="true"></uui-icon><span>${this.#title(c.checkId)} <span class="muted">(not applicable here)</span></span>`
              : c.passed
                ? html`<uui-icon class="ok" name="icon-check" aria-hidden="true"></uui-icon><span><span class="visually-hidden">Passed: </span>${this.#title(c.checkId)}</span>`
                : html`<uui-icon class="bad" name="icon-wrong" aria-hidden="true"></uui-icon><span><span class="visually-hidden">Failed: </span>
                    <a href="#" @click=${(e: Event) => { e.preventDefault(); navigate(this, 'issues', { checkId: c.checkId }); }}>${this.#title(c.checkId)}</a>
                    ${c.value ? html`<span class="muted">: ${c.value}</span>` : nothing}</span>`}
          </li>`)}
        </ul>
      </uui-box>

      ${this.#pageList(`Hidden from search (${o.hidden.length})`, 'Pages that tell search engines not to index them, or that robots.txt blocks. Usually on purpose (login, search, thank-you pages); check that nothing important is here.', o.hidden, (p) => p.reason)}
      ${this.#pageList(`Not scanned (${o.skipped.length})`, 'Published pages the scan left out.', o.skipped, (p) => skipLabel[p.reason] ?? p.reason)}

      ${o.trend.length ? html`
        <uui-box headline="Recent scans">
          <table>
            <caption class="visually-hidden">Score of the last ${o.trend.length} scans, oldest first</caption>
            <thead><tr><th scope="col">Scan</th><th scope="col" class="num">Score</th><th scope="col" class="num">Pages</th></tr></thead>
            <tbody>${o.trend.map((t) => html`<tr><td>${formatDate(t.startedUtc)}</td><td class="num">${t.score}</td><td class="num">${t.pages}</td></tr>`)}</tbody>
          </table>
        </uui-box>` : nothing}`;
  }

  #pageList(title: string, description: string, pages: PageRow[], reason: (p: PageRow) => string) {
    if (!pages.length) return nothing;
    return html`
      <uui-box>
        <details>
          <summary><strong>${title}</strong></summary>
          <p class="muted">${description}</p>
          <table>
            <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Why</th></tr></thead>
            <tbody>${pages.map((p) => html`<tr>
              <td><a href=${editContentUrl(p.contentKey, p.culture)}>${p.name}</a>
                ${safeHref(p.url) ? html`<br><a class="muted url" href=${safeHref(p.url)!} target="_blank" rel="noopener">${p.url}</a>` : nothing}</td>
              <td>${p.culture || '–'}</td>
              <td>${reason(p)}</td>
            </tr>`)}</tbody>
          </table>
        </details>
      </uui-box>`;
  }

  static override styles = [shared, css`
    .hero { display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; }
    .hero > .stack { flex: 1; min-width: 16rem; }
    .ring { position: relative; margin: 0; width: ${RING.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-2); }
    .ring svg { display: block; }
    .ring .track { stroke: var(--uui-color-surface-alt); }
    .ring.good .fill { stroke: var(--seo-good); }
    .ring.warning .fill { stroke: var(--seo-warning); }
    .ring.critical .fill { stroke: var(--seo-critical); }
    .ring-text { position: absolute; top: 0; height: ${RING.size}px; display: grid; place-content: center; text-align: center; }
    .ring-value { font-size: 48px; font-weight: 600; line-height: 1; }
    .ring-band { display: flex; gap: 4px; align-items: center; font-weight: 600; }
    .url { word-break: break-all; font-size: 0.9em; }
    .section-title { margin-bottom: var(--uui-size-space-3); }
    .top { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-2); }
    .top-item, .tile {
      font: inherit; color: inherit; text-align: left; cursor: pointer; width: 100%;
      background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius);
      padding: var(--uui-size-space-3) var(--uui-size-space-4); min-height: 44px;
    }
    .top-item { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
    .top-item:hover, .tile:hover { border-color: var(--uui-color-interactive); }
    .top-title { font-weight: 600; flex: 1; min-width: 12rem; }
    .tiles { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr)); }
    .tile { display: grid; gap: 4px; }
    .tile-label { display: inline-flex; gap: 4px; align-items: center; font-weight: 600; }
    .tile-value { font-size: 2rem; font-weight: 600; line-height: 1.1; }
    .checklist { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-2); }
    .checklist li { display: flex; gap: var(--uui-size-space-2); align-items: flex-start; }
    .checklist .ok { color: var(--seo-good); }
    .checklist .bad { color: var(--seo-critical); }
    .progress:empty { display: none; }
  `];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-overview': EsattoSeoOverviewElement;
  }
}
