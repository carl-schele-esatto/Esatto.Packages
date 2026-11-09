// Overview tab: the score and its trend, "Fix these first", counts by severity, the whole-site checklist,
// pages hidden from search, and Scan now. Scans run on the server; this tab polls their progress.

import { css, customElement, html, nothing, property, state, styleMap } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { cancelScan, getOverview, getProgress, startScan } from '../api';
import { shared } from '../dashboard.styles';
import { band, bandIcon, bandLabel, editContentUrl, formatDate, ownerLabel, plural, safeHref, severityIcon, severityLabel, skipLabel, type Band } from '../format';
import { navigate } from '../navigate';
import type { Knowledge, Overview, PageRow, ScanProgress, Severity } from '../types';

const RING = { size: 168, stroke: 14 };
const POLL_MS = 2000;

@customElement('esatto-seo-health-overview')
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
      <div class="page">
        ${this.#banners(o)}
        ${this.#hero(o)}
        ${o.lastScan ? this.#results(o) : nothing}
      </div>`;
  }

  #banner(tone: 'critical' | 'warning' | 'notice', icon: string, body: unknown, alert = false) {
    return html`
      <div class="banner tone-${tone}" role=${alert ? 'alert' : nothing}>
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${icon}></uui-icon></span>
        <div>${body}</div>
      </div>`;
  }

  #banners(o: Overview) {
    const attempt = o.latestAttempt;
    return html`
      ${o.siteHidden && !o.intentionallyHidden ? this.#banner('critical', 'icon-alert', html`
        <strong>The whole site is hidden from search engines.</strong>
        <p>That's expected on a Test or Dev environment. On the live site it's the most serious problem there is. To mark this environment as hidden on purpose, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.</p>`, true) : nothing}
      ${o.siteHidden && o.intentionallyHidden ? this.#banner('notice', 'icon-info', html`
        <strong>This environment is hidden from search engines on purpose.</strong>
        <p>Pages are checked as if they were public, so content problems still show up here before they reach the live site.</p>`) : nothing}
      ${attempt && attempt.status !== 'complete' ? this.#banner('warning', 'icon-alert', html`
        <strong>The latest scan (${formatDate(attempt.startedUtc)}) ${attempt.status === 'cancelled' ? 'was cancelled' : attempt.status === 'incomplete' ? 'was interrupted by a restart' : "couldn't run"}.</strong>
        ${attempt.message ? html`<p>${attempt.message}</p>` : nothing}
        ${o.lastScan ? html`<p class="muted">The results below are from ${formatDate(o.lastScan.startedUtc)}.</p>` : nothing}`) : nothing}`;
  }

  #hero(o: Overview) {
    const b = band(o.score);
    return html`
      <section class="card hero tone-${b ?? 'none'}" aria-labelledby="hero-heading">
        ${this.#ring(o.score, b)}
        <div class="hero-body stack">
          <div>
            <p class="eyebrow">SEO score</p>
            <h3 id="hero-heading" class="hero-title">${b ? bandLabel[b] : 'Not scanned yet'}</h3>
          </div>
          ${this.#lastScanLine(o)}
          ${this.#scanControls()}
          <div aria-live="polite" class="progress">${this.#progressView()}</div>
          <details class="how">
            <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>How is the score worked out?</summary>
            <p>Each page that search engines may index scores 0–100: zero if it has a critical problem, otherwise the share of checks it passes (critical checks weigh 10, warnings 3, notices 1). The site score is the average, reduced by any failed whole-site check. Ignored findings don't count.</p>
          </details>
        </div>
      </section>`;
  }

  #ring(score: number | null, b: Band | null) {
    const r = (RING.size - RING.stroke) / 2;
    const circumference = 2 * Math.PI * r;
    const filled = ((score ?? 0) / 100) * circumference;
    const label = score === null ? 'No score yet' : `SEO score ${score} out of 100, ${bandLabel[b!]}`;
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
          <circle class="fill ${!score ? 'empty' : ''}" cx=${c} cy=${c} r=${r} stroke-width=${RING.stroke} fill="none" stroke="url(#ring-gradient)"
            stroke-dasharray="${filled} ${circumference}" stroke-linecap="round" transform="rotate(-90 ${c} ${c})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true"><span class="ring-value">${score ?? '–'}</span><span class="muted small">of 100</span></div>
        <figcaption aria-hidden="true">
          ${b ? html`<span class="tag tone-${b}"><uui-icon name=${bandIcon[b]}></uui-icon>${bandLabel[b]}</span>` : html`<span class="tag">Not scanned yet</span>`}
        </figcaption>
      </figure>`;
  }

  #lastScanLine(o: Overview) {
    const s = o.lastScan;
    if (!s) return html`<p class="muted">This site hasn't been scanned yet. Scans also run every night (see Settings).</p>`;
    return html`
      <ul class="meta" aria-label="Last scan">
        <li class="chip"><uui-icon name="icon-calendar" aria-hidden="true"></uui-icon>${formatDate(s.startedUtc)}</li>
        <li class="chip"><uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${plural(s.pagesDone, 'page', 'pages')}${s.pagesSkipped ? ` · ${s.pagesSkipped} skipped` : ''}</li>
        ${s.pagesFailed ? html`<li class="chip"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${s.pagesFailed} didn't load</li>` : nothing}
        <li class="chip"><uui-icon name=${s.trigger === 'scheduled' ? 'icon-time' : 'icon-user'} aria-hidden="true"></uui-icon>${s.trigger === 'scheduled' ? 'Nightly scan' : `By ${s.userName}`}</li>
      </ul>`;
  }

  #scanControls() {
    if (this._progress) {
      return html`<div class="row"><uui-button look="secondary" label="Cancel scan" @click=${this.#cancel}></uui-button></div>`;
    }
    return html`<div class="row">
      <uui-button look="primary" label="Scan now" .state=${this._starting ? 'waiting' : undefined} @click=${this.#scan}>
        <uui-icon name="icon-sync" aria-hidden="true"></uui-icon>&nbsp;Scan now
      </uui-button>
      <span class="muted small">Runs on the server: you can leave this page.</span>
    </div>`;
  }

  #progressView() {
    const p = this._progress;
    if (!p) return nothing;
    if (!p.total) return html`<p>Starting the scan…</p>`;
    return html`
      <div class="progress-bar" aria-hidden="true"><span style=${styleMap({ width: `${Math.round((p.done / p.total) * 100)}%` })}></span></div>
      <p>Scanned ${p.done} of ${plural(p.total, 'page', 'pages')}${p.done === p.total ? ', checking links and the sitemap…' : ''}</p>
      ${p.currentUrl && p.done < p.total ? html`<p class="muted url">${p.currentUrl}</p>` : nothing}`;
  }

  #results(o: Overview) {
    const tiles: { severity: Severity; count: number }[] = [
      { severity: 'critical', count: o.counts.critical },
      { severity: 'warning', count: o.counts.warning },
      { severity: 'notice', count: o.counts.notice },
    ];
    const applicable = o.siteChecks.filter((c) => c.passed !== null);
    return html`
      <section aria-labelledby="sev-heading">
        <h3 id="sev-heading" class="section-label">Open findings</h3>
        <ul class="tiles">
          ${tiles.map((t) => html`<li>
            <button class="tile lift tone-${t.severity}" @click=${() => navigate(this, 'issues', { severity: t.severity })}>
              <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${severityIcon[t.severity]}></uui-icon></span>
              <span class="tile-text"><span class="tile-value">${t.count}</span><span class="tile-label">${severityLabel[t.severity]}</span></span>
              <uui-icon class="tile-go" name="icon-arrow-right" aria-hidden="true"></uui-icon>
            </button>
          </li>`)}
        </ul>
      </section>

      <div class="split">
        <section class="card" aria-labelledby="first-heading">
          <div class="card-head">
            <span class="icon-badge tone-critical" aria-hidden="true"><uui-icon name="icon-flag"></uui-icon></span>
            <h3 id="first-heading">Fix these first</h3>
          </div>
          ${o.top.length ? html`
            <ol class="top">
              ${o.top.map((t, i) => html`<li>
                <button class="top-item lift tone-${t.severity}" @click=${() => navigate(this, 'issues', { checkId: t.checkId })}>
                  <span class="rank" aria-hidden="true">${i + 1}</span>
                  <span class="top-main">
                    <span class="top-title">${this.#title(t.checkId)}</span>
                    <span class="muted small">${t.pages ? plural(t.pages, 'page', 'pages') : 'whole site'} · ${ownerLabel[t.owner]}</span>
                  </span>
                  <span class="tag tone-${t.severity}"><uui-icon name=${severityIcon[t.severity]}></uui-icon>${severityLabel[t.severity]}</span>
                  <uui-icon class="go" name="icon-navigation-right" aria-hidden="true"></uui-icon>
                </button>
              </li>`)}
            </ol>` : html`
            <div class="empty">
              <span class="icon-badge solid round tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span>
              <p>Nothing to fix. Well done.</p>
            </div>`}
        </section>

        <section class="card" aria-labelledby="site-heading">
          <div class="card-head">
            <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-globe"></uui-icon></span>
            <h3 id="site-heading">Whole site</h3>
            ${applicable.length ? html`<span class="chip end">${applicable.filter((c) => c.passed).length} of ${applicable.length} passed</span>` : nothing}
          </div>
          <ul class="checklist">
            ${o.siteChecks.map((c) => html`<li>
              ${c.passed === null
                ? html`<span class="status na" aria-hidden="true"><uui-icon name="icon-circle-dotted"></uui-icon></span><span>${this.#title(c.checkId)} <span class="muted">(not applicable here)</span></span>`
                : c.passed
                  ? html`<span class="status tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span><span><span class="visually-hidden">Passed: </span>${this.#title(c.checkId)}</span>`
                  : html`<span class="status tone-critical" aria-hidden="true"><uui-icon name="icon-wrong"></uui-icon></span><span><span class="visually-hidden">Failed: </span>
                      <a href="#" @click=${(e: Event) => { e.preventDefault(); navigate(this, 'issues', { checkId: c.checkId }); }}>${this.#title(c.checkId)}</a>
                      ${c.value ? html`<span class="muted">: ${c.value}</span>` : nothing}</span>`}
            </li>`)}
          </ul>
        </section>
      </div>

      ${o.trend.length ? html`
        <section class="card" aria-labelledby="trend-heading">
          <div class="card-head">
            <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
            <h3 id="trend-heading">Recent scans</h3>
          </div>
          <div class="table-wrap">
            <table>
              <caption class="visually-hidden">Score of the last ${o.trend.length} scans, oldest first</caption>
              <thead><tr><th scope="col">Scan</th><th scope="col">Score</th><th scope="col" class="num">Pages</th></tr></thead>
              <tbody>${o.trend.map((t) => html`<tr>
                <td>${formatDate(t.startedUtc)}</td>
                <td><span class="score-cell tone-${band(t.score)}">
                  <span class="score-bar" aria-hidden="true"><span style=${styleMap({ width: `${t.score}%` })}></span></span>
                  <span class="score-num">${t.score}</span>
                </span></td>
                <td class="num">${t.pages}</td>
              </tr>`)}</tbody>
            </table>
          </div>
        </section>` : nothing}

      ${this.#pageList('Hidden from search', 'icon-block', 'Pages that tell search engines not to index them, or that robots.txt blocks. Usually on purpose (login, search, thank-you pages); check that nothing important is here.', o.hidden, (p) => p.reason)}
      ${this.#pageList('Not scanned', 'icon-document-dashed-line', 'Published pages the scan left out.', o.skipped, (p) => skipLabel[p.reason] ?? p.reason)}`;
  }

  #pageList(title: string, icon: string, description: string, pages: PageRow[], reason: (p: PageRow) => string) {
    if (!pages.length) return nothing;
    return html`
      <details class="card disclosure">
        <summary class="card-head">
          <span class="icon-badge tone-none" aria-hidden="true"><uui-icon name=${icon}></uui-icon></span>
          <h3>${title}</h3>
          <span class="chip">${pages.length}</span>
          <uui-icon class="chevron end" name="icon-navigation-right" aria-hidden="true"></uui-icon>
        </summary>
        <p class="muted">${description}</p>
        <div class="table-wrap">
          <table>
            <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Why</th></tr></thead>
            <tbody>${pages.map((p) => html`<tr>
              <td><a href=${editContentUrl(p.contentKey, p.culture)}>${p.name}</a>
                ${safeHref(p.url) ? html`<br><a class="muted url" href=${safeHref(p.url)!} target="_blank" rel="noopener">${p.url}</a>` : nothing}</td>
              <td>${p.culture || '–'}</td>
              <td>${reason(p)}</td>
            </tr>`)}</tbody>
          </table>
        </div>
      </details>`;
  }

  static override styles = [shared, css`
    .hero {
      display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; padding: var(--uui-size-space-6);
      background: radial-gradient(120% 160% at 0% 0%, color-mix(in srgb, var(--tone) 16%, var(--uui-color-surface)) 0%, var(--uui-color-surface) 62%);
      border-color: color-mix(in srgb, var(--tone) 30%, var(--seo-line));
    }
    .hero.tone-good { --ring-a: #4ade80; --ring-b: var(--seo-good); }
    .hero.tone-warning { --ring-a: #fcd34d; --ring-b: #ea7c0c; }
    .hero.tone-critical { --ring-a: #fb7185; --ring-b: var(--seo-critical); }
    .hero-body { flex: 1; min-width: 18rem; }
    .eyebrow { font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--seo-muted); }
    .hero-title { font-size: 28px; font-weight: 800; letter-spacing: -0.015em; line-height: 1.2; margin-top: 2px; }
    .ring { position: relative; margin: 0; width: ${RING.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-3); }
    .ring svg { display: block; filter: drop-shadow(0 8px 18px color-mix(in srgb, var(--tone) 30%, transparent)); }
    .ring .track { stroke: color-mix(in srgb, var(--tone) 24%, var(--uui-color-surface)); }
    .ring .fill.empty { display: none; }
    .stop-a { stop-color: var(--ring-a, var(--tone)); }
    .stop-b { stop-color: var(--ring-b, var(--tone)); }
    .ring-text { position: absolute; top: 0; height: ${RING.size}px; display: grid; place-content: center; text-align: center; }
    .ring-value { font-size: 54px; font-weight: 800; line-height: 1; letter-spacing: -0.03em; font-variant-numeric: tabular-nums; }
    .meta { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
    .how > summary { display: inline-flex; gap: 6px; align-items: center; font-weight: 600; color: var(--uui-color-interactive); }
    .how > summary .chevron { color: inherit; }
    .how > p { margin-top: var(--uui-size-space-3); max-width: 46rem; color: var(--seo-muted); }
    .progress:empty { display: none; }
    .progress { display: grid; gap: var(--uui-size-space-2); max-width: 36rem; }
    .progress-bar { height: 8px; border-radius: 999px; background: var(--seo-wash); border: 1px solid var(--seo-line); overflow: hidden; }
    .progress-bar > span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #2f6fde, #6d4ce0); transition: width 400ms ease; }

    .section-label { font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--seo-muted); margin-bottom: var(--uui-size-space-3); }
    .tiles { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--seo-gap); grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr)); }
    .tile, .top-item { font: inherit; color: inherit; text-align: left; cursor: pointer; width: 100%; }
    .tile {
      display: flex; gap: var(--uui-size-space-4); align-items: center; min-height: 88px; padding: var(--uui-size-space-4) var(--uui-size-space-5);
      border-radius: var(--seo-radius); border: 1px solid color-mix(in srgb, var(--tone) 35%, var(--seo-line));
      background: linear-gradient(135deg, color-mix(in srgb, var(--tone) 14%, var(--uui-color-surface)), var(--uui-color-surface) 78%);
      box-shadow: var(--seo-shadow);
    }
    .tile:hover { border-color: var(--tone); }
    .tile .icon-badge { width: 44px; height: 44px; font-size: 22px; box-shadow: 0 6px 14px -6px var(--tone); }
    .tile-text { display: grid; flex: 1; }
    .tile-value { font-size: 32px; font-weight: 800; line-height: 1.05; letter-spacing: -0.02em; font-variant-numeric: tabular-nums; }
    .tile-label { font-size: 13px; font-weight: 600; color: var(--seo-muted); }
    .tile-go { color: var(--seo-muted); transition: transform var(--seo-ease); }
    .tile:hover .tile-go { transform: translateX(3px); }

    .split { display: grid; gap: var(--seo-gap); grid-template-columns: repeat(auto-fit, minmax(min(100%, 26rem), 1fr)); align-items: start; }
    .top { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-2); }
    .top-item {
      display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; min-height: 52px; padding: var(--uui-size-space-3) var(--uui-size-space-4);
      background: var(--uui-color-surface); border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm);
      box-shadow: inset 3px 0 0 var(--tone);
    }
    .top-item:hover { border-color: color-mix(in srgb, var(--tone) 55%, var(--seo-line)); box-shadow: inset 3px 0 0 var(--tone), var(--seo-shadow-raised); }
    .rank {
      flex: none; display: grid; place-items: center; width: 28px; height: 28px; border-radius: 50%; font-size: 13px; font-weight: 700;
      background: color-mix(in srgb, var(--tone) 15%, var(--uui-color-surface)); font-variant-numeric: tabular-nums;
    }
    .top-main { flex: 1; min-width: 10rem; display: grid; gap: 2px; }
    .top-title { font-weight: 600; }
    .go { color: var(--seo-muted); }
    .empty { display: flex; gap: var(--uui-size-space-3); align-items: center; }

    .checklist { list-style: none; margin: 0; padding: 0; display: grid; }
    .checklist li { display: flex; gap: var(--uui-size-space-3); align-items: flex-start; padding: var(--uui-size-space-3) 0; border-bottom: 1px solid var(--seo-line); }
    .checklist li:first-child { padding-top: 0; }
    .checklist li:last-child { padding-bottom: 0; border-bottom: 0; }
    .status { flex: none; display: grid; place-items: center; width: 22px; height: 22px; border-radius: 50%; font-size: 12px; background: var(--tone); color: var(--tone-on); }
    .status.na { background: var(--seo-wash); color: var(--seo-muted); border: 1px solid var(--seo-line); box-sizing: border-box; }

    .disclosure > summary { margin-bottom: 0; }
    .disclosure[open] > summary { margin-bottom: var(--uui-size-space-4); }
    .disclosure > p { margin-bottom: var(--uui-size-space-4); }
    .score-cell { display: flex; gap: var(--uui-size-space-3); align-items: center; min-width: 10rem; max-width: 18rem; }
    .score-bar { flex: 1; height: 8px; border-radius: 999px; background: color-mix(in srgb, var(--tone) 15%, var(--uui-color-surface)); overflow: hidden; }
    .score-bar > span { display: block; height: 100%; border-radius: inherit; background: var(--tone); }
    .score-num { font-weight: 700; font-variant-numeric: tabular-nums; min-width: 2ch; text-align: right; }
  `];
}


declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-health-overview': EsattoSeoOverviewElement;
  }
}
