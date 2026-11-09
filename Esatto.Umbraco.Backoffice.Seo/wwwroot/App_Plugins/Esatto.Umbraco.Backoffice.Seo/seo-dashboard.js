import { css as W, property as T, state as v, customElement as G, html as a, nothing as l, styleMap as ue } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as B } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as m } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as b } from "@umbraco-cms/backoffice/http-client";
import { UMB_NOTIFICATION_CONTEXT as Be } from "@umbraco-cms/backoffice/notification";
const y = "/umbraco/management/api/v1/esatto/seo-health", w = () => [{ scheme: "bearer", type: "http" }], ie = { "Content-Type": "application/json" }, U = (e) => `site=${encodeURIComponent(e)}`;
function de(e, t) {
  const i = new URLSearchParams({ site: e });
  return t.severity && i.set("severity", t.severity), t.owner && i.set("owner", t.owner), t.culture && i.set("culture", t.culture), t.checkId && i.set("checkId", t.checkId), t.ignored && i.set("ignored", "true"), t.search?.trim() && i.set("search", t.search.trim()), i.toString();
}
const qe = () => b.get({ url: `${y}/knowledge`, security: w() }), Ve = () => b.get({ url: `${y}/sites`, security: w() }), je = () => b.get({ url: `${y}/settings`, security: w() }), Je = (e) => b.put({ url: `${y}/settings`, body: e, security: w(), headers: ie }), Xe = (e) => b.get({ url: `${y}/overview?${U(e)}`, security: w() }), Ze = (e, t) => b.get({ url: `${y}/issues?${de(e, t)}`, security: w() }), Qe = (e, t) => b.get({ url: `${y}/issues/csv?${de(e, t)}`, security: w(), parseAs: "blob" }), Ye = (e) => b.post({ url: `${y}/scans?${U(e)}`, security: w() }), et = (e) => b.get({ url: `${y}/scans/progress?${U(e)}`, security: w() }), tt = (e) => b.delete({ url: `${y}/scans/current?${U(e)}`, security: w() }), it = (e, t, i) => b.post({ url: `${y}/recheck?${U(e)}`, body: { contentKey: t, culture: i }, security: w(), headers: ie }), st = (e, t) => b.post({ url: `${y}/ignores?${U(e)}`, body: t, security: w(), headers: ie }), at = (e) => b.delete({ url: `${y}/ignores/${e}`, security: w() }), q = W`
  :host {
    display: block;
    --seo-critical: #c9374c;
    --seo-critical-on: #fff;
    --seo-warning: #f59e0b;
    --seo-warning-on: #1d1d1f;
    --seo-notice: #2f6fde;
    --seo-notice-on: #fff;
    --seo-good: #0e7f41;
    --seo-good-on: #fff;
    --seo-neutral: color-mix(in srgb, var(--uui-color-text) 70%, var(--uui-color-surface));
    --seo-neutral-on: var(--uui-color-surface);

    --seo-muted: color-mix(in srgb, var(--uui-color-text) 68%, var(--uui-color-surface));
    --seo-line: var(--uui-color-border);
    --seo-wash: color-mix(in srgb, var(--uui-color-text) 4%, var(--uui-color-surface));
    --seo-radius: 12px;
    --seo-radius-sm: 8px;
    --seo-shadow: var(--uui-shadow-depth-1);
    --seo-shadow-raised: var(--uui-shadow-depth-3);
    --seo-ease: 160ms ease;
    /* One gap between all blocks on a tab (cards, rows of cards, the save button), so spacing reads as one rhythm. */
    --seo-gap: var(--uui-size-space-5);
  }

  /* Tones: set --tone/--tone-on once, every component below reads them. */
  .tone-critical { --tone: var(--seo-critical); --tone-on: var(--seo-critical-on); }
  .tone-warning { --tone: var(--seo-warning); --tone-on: var(--seo-warning-on); }
  .tone-notice { --tone: var(--seo-notice); --tone-on: var(--seo-notice-on); }
  .tone-good { --tone: var(--seo-good); --tone-on: var(--seo-good-on); }
  .tone-none { --tone: var(--seo-neutral); --tone-on: var(--seo-neutral-on); }

  .muted { color: var(--seo-muted); }
  .small { font-size: 13px; }
  .row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .stack > * + * { margin-top: var(--uui-size-space-4); }
  .page { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--seo-gap); }
  .error { color: var(--uui-color-danger-standalone); }
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  code { padding: 1px 6px; border-radius: 6px; background: var(--seo-wash); border: 1px solid var(--seo-line); }
  pre { white-space: pre-wrap; word-break: break-word; margin: 0; }
  a { color: var(--uui-color-interactive); text-underline-offset: 2px; }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .url { word-break: break-all; font-size: 13px; }

  /* Surfaces */
  .card {
    background: var(--uui-color-surface); color: var(--uui-color-text);
    border: 1px solid var(--seo-line); border-radius: var(--seo-radius); box-shadow: var(--seo-shadow);
    padding: var(--uui-size-space-5) var(--uui-size-space-6);
  }
  .card-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-bottom: var(--uui-size-space-4); }
  .card-head h3 { font-size: 16px; font-weight: 700; }
  .card-head .end { margin-left: auto; }
  .icon-badge {
    flex: none; display: inline-grid; place-items: center; width: 34px; height: 34px; border-radius: 10px; font-size: 18px;
    background: color-mix(in srgb, var(--tone, var(--seo-notice)) 14%, var(--uui-color-surface));
    color: color-mix(in srgb, var(--tone, var(--seo-notice)) 80%, var(--uui-color-text));
  }
  .icon-badge.solid { background: var(--tone); color: var(--tone-on); }
  .icon-badge.round { border-radius: 50%; }

  /* Badges */
  .tag {
    display: inline-flex; gap: 5px; align-items: center; padding: 2px 10px 2px 8px; border-radius: 999px;
    font-size: 12px; font-weight: 700; line-height: 18px; letter-spacing: 0.01em; white-space: nowrap;
    background: var(--tone, var(--seo-wash)); color: var(--tone-on, var(--uui-color-text));
  }
  .tag uui-icon { font-size: 13px; }
  .chip {
    display: inline-flex; gap: 6px; align-items: center; padding: 2px 10px; border-radius: 999px; font-size: 13px; line-height: 20px;
    white-space: nowrap; background: var(--seo-wash); border: 1px solid var(--seo-line); color: var(--uui-color-text);
  }
  .chip uui-icon { font-size: 14px; color: var(--seo-muted); }

  /* Alerts */
  .banner {
    display: flex; gap: var(--uui-size-space-4); align-items: flex-start; padding: var(--uui-size-space-4) var(--uui-size-space-5);
    border-radius: var(--seo-radius); color: var(--uui-color-text);
    background: color-mix(in srgb, var(--tone) 9%, var(--uui-color-surface));
    border: 1px solid color-mix(in srgb, var(--tone) 45%, var(--seo-line));
    box-shadow: inset 4px 0 0 var(--tone);
  }
  .banner > div { display: grid; gap: 4px; padding-top: 6px; }

  /* Form controls */
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label { font-size: 13px; font-weight: 600; }
  select, input:not([type='checkbox']), textarea {
    font: inherit; min-height: 40px; padding: 0 var(--uui-size-space-3); box-sizing: border-box;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  textarea { padding: var(--uui-size-space-2) var(--uui-size-space-3); }
  select:hover, input:not([type='checkbox']):hover, textarea:hover { border-color: var(--uui-color-border-emphasis); }
  select:focus-visible, input:focus-visible, textarea:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  input:disabled, select:disabled, textarea:disabled { opacity: 0.65; cursor: not-allowed; }

  /* Tables */
  .table-wrap { overflow-x: auto; border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm); }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; vertical-align: top; padding: var(--uui-size-space-3) var(--uui-size-space-4); border-bottom: 1px solid var(--seo-line); }
  tbody tr:last-child > * { border-bottom: 0; }
  th {
    font-size: 12px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase; color: var(--seo-muted);
    background: var(--seo-wash); white-space: nowrap;
  }
  tbody tr { transition: background-color var(--seo-ease); }
  tbody tr:hover { background: var(--seo-wash); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }

  /* Disclosures: a chevron instead of the browser's triangle. */
  summary { cursor: pointer; list-style: none; }
  summary::-webkit-details-marker { display: none; }
  .chevron { flex: none; color: var(--seo-muted); transition: transform var(--seo-ease); }
  details[open] > summary .chevron { transform: rotate(90deg); }

  /* Clickable cards lift on hover. */
  .lift { transition: transform var(--seo-ease), box-shadow var(--seo-ease), border-color var(--seo-ease); }
  .lift:hover { transform: translateY(-2px); box-shadow: var(--seo-shadow-raised); }

  @media (prefers-reduced-motion: reduce) {
    *, .lift, .chevron { transition: none !important; }
    .lift:hover { transform: none; }
  }
`, he = "esatto-seo-navigate";
function J(e, t, i) {
  e.dispatchEvent(new CustomEvent(he, { detail: { tab: t, filter: i }, bubbles: !0, composed: !0 }));
}
function rt(e) {
  return e ? new Date(/[zZ]|[+-]\d\d:\d\d$/.test(e) ? e : `${e}Z`) : null;
}
function A(e) {
  const t = rt(e);
  return t ? new Intl.DateTimeFormat(void 0, { dateStyle: "medium", timeStyle: "short" }).format(t) : "";
}
function pe(e) {
  return e === null ? null : e < 50 ? "critical" : e < 90 ? "warning" : "good";
}
const X = { good: "Good", warning: "Needs work", critical: "Serious problems" }, nt = { good: "icon-check", warning: "icon-alert", critical: "icon-wrong" }, Z = { critical: "Critical", warning: "Warning", notice: "Notice" }, Q = { critical: "icon-wrong", warning: "icon-alert", notice: "icon-info" }, ge = { editor: "Editor", developer: "Developer" }, ot = {
  protected: "Members only",
  excluded: "Excluded in Settings",
  "no-url": "No address (no domain bound and no base URL)"
}, V = (e, t, i) => `${e} ${e === 1 ? t : i}`;
function ve(e, t) {
  const i = `/umbraco/section/content/workspace/document/edit/${e}`;
  return t ? `${i}/${t}` : i;
}
function D(e) {
  try {
    const t = new URL(e ?? "");
    return t.protocol === "http:" || t.protocol === "https:" ? t.toString() : void 0;
  } catch {
    return;
  }
}
var lt = Object.defineProperty, ct = Object.getOwnPropertyDescriptor, fe = (e) => {
  throw TypeError(e);
}, O = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? ct(t, i) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (r = (s ? o(t, i, r) : o(r)) || r);
  return s && r && lt(t, i, r), r;
}, se = (e, t, i) => t.has(e) || fe("Cannot " + i), me = (e, t, i) => (se(e, t, "read from private field"), t.get(e)), oe = (e, t, i) => t.has(e) ? fe("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), ut = (e, t, i, s) => (se(e, t, "write to private field"), t.set(e, i), i), h = (e, t, i) => (se(e, t, "access private method"), i), M, c, ae, R, be, ye, L, H, we, $e, xe, _e, ke, ze, Se, Y;
const x = { size: 168, stroke: 14 }, dt = 2e3;
let k = class extends B {
  constructor() {
    super(...arguments), oe(this, c), this.siteKey = "", this.knowledge = null, this._overview = null, this._progress = null, this._error = null, this._starting = !1, oe(this, M);
  }
  updated(e) {
    e.has("siteKey") && this.siteKey && h(this, c, ae).call(this);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.clearTimeout(me(this, M));
  }
  render() {
    if (this._error) return a`<uui-box><p class="error">${this._error}</p></uui-box>`;
    const e = this._overview;
    return e ? a`
      <div class="page">
        ${h(this, c, we).call(this, e)}
        ${h(this, c, $e).call(this, e)}
        ${e.lastScan ? h(this, c, Se).call(this, e) : l}
      </div>` : a`<uui-loader></uui-loader>`;
  }
};
M = /* @__PURE__ */ new WeakMap();
c = /* @__PURE__ */ new WeakSet();
ae = async function() {
  const { data: e, error: t } = await m(this, Xe(this.siteKey), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load the overview.";
    return;
  }
  this._error = null, this._overview = e, this._progress = e.running, e.running && h(this, c, R).call(this);
};
R = function() {
  window.clearTimeout(me(this, M)), ut(this, M, window.setTimeout(async () => {
    const { data: e, response: t } = await m(this, et(this.siteKey), { disableNotifications: !0 });
    t?.status === 200 && e ? (this._progress = e, h(this, c, R).call(this)) : (this._progress = null, await h(this, c, ae).call(this));
  }, dt));
};
be = async function() {
  this._starting = !0;
  const { error: e } = await m(this, Ye(this.siteKey));
  this._starting = !1, !e && (this._progress = { scanId: 0, done: 0, total: 0, currentUrl: null }, h(this, c, R).call(this));
};
ye = async function() {
  await m(this, tt(this.siteKey), { disableNotifications: !0 });
};
L = function(e) {
  return this.knowledge?.texts[e]?.title ?? e;
};
H = function(e, t, i, s = !1) {
  return a`
      <div class="banner tone-${e}" role=${s ? "alert" : l}>
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${t}></uui-icon></span>
        <div>${i}</div>
      </div>`;
};
we = function(e) {
  const t = e.latestAttempt;
  return a`
      ${e.siteHidden && !e.intentionallyHidden ? h(this, c, H).call(this, "critical", "icon-alert", a`
        <strong>The whole site is hidden from search engines.</strong>
        <p>That's expected on a Test or Dev environment. On the live site it's the most serious problem there is. To mark this environment as hidden on purpose, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.</p>`, !0) : l}
      ${e.siteHidden && e.intentionallyHidden ? h(this, c, H).call(this, "notice", "icon-info", a`
        <strong>This environment is hidden from search engines on purpose.</strong>
        <p>Pages are checked as if they were public, so content problems still show up here before they reach the live site.</p>`) : l}
      ${t && t.status !== "complete" ? h(this, c, H).call(this, "warning", "icon-alert", a`
        <strong>The latest scan (${A(t.startedUtc)}) ${t.status === "cancelled" ? "was cancelled" : t.status === "incomplete" ? "was interrupted by a restart" : "couldn't run"}.</strong>
        ${t.message ? a`<p>${t.message}</p>` : l}
        ${e.lastScan ? a`<p class="muted">The results below are from ${A(e.lastScan.startedUtc)}.</p>` : l}`) : l}`;
};
$e = function(e) {
  const t = pe(e.score);
  return a`
      <section class="card hero tone-${t ?? "none"}" aria-labelledby="hero-heading">
        ${h(this, c, xe).call(this, e.score, t)}
        <div class="hero-body stack">
          <div>
            <p class="eyebrow">SEO score</p>
            <h3 id="hero-heading" class="hero-title">${t ? X[t] : "Not scanned yet"}</h3>
          </div>
          ${h(this, c, _e).call(this, e)}
          ${h(this, c, ke).call(this)}
          <div aria-live="polite" class="progress">${h(this, c, ze).call(this)}</div>
          <details class="how">
            <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>How is the score worked out?</summary>
            <p>Each page that search engines may index scores 0–100: zero if it has a critical problem, otherwise the share of checks it passes (critical checks weigh 10, warnings 3, notices 1). The site score is the average, reduced by any failed whole-site check. Ignored findings don't count.</p>
          </details>
        </div>
      </section>`;
};
xe = function(e, t) {
  const i = (x.size - x.stroke) / 2, s = 2 * Math.PI * i, r = (e ?? 0) / 100 * s, n = e === null ? "No score yet" : `SEO score ${e} out of 100, ${X[t]}`, o = x.size / 2;
  return a`
      <figure class="ring" role="img" aria-label=${n}>
        <svg width=${x.size} height=${x.size} viewBox="0 0 ${x.size} ${x.size}" aria-hidden="true">
          <defs>
            <linearGradient id="ring-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" class="stop-a"></stop>
              <stop offset="100%" class="stop-b"></stop>
            </linearGradient>
          </defs>
          <circle class="track" cx=${o} cy=${o} r=${i} stroke-width=${x.stroke} fill="none"></circle>
          <circle class="fill ${e ? "" : "empty"}" cx=${o} cy=${o} r=${i} stroke-width=${x.stroke} fill="none" stroke="url(#ring-gradient)"
            stroke-dasharray="${r} ${s}" stroke-linecap="round" transform="rotate(-90 ${o} ${o})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true"><span class="ring-value">${e ?? "–"}</span><span class="muted small">of 100</span></div>
        <figcaption aria-hidden="true">
          ${t ? a`<span class="tag tone-${t}"><uui-icon name=${nt[t]}></uui-icon>${X[t]}</span>` : a`<span class="tag">Not scanned yet</span>`}
        </figcaption>
      </figure>`;
};
_e = function(e) {
  const t = e.lastScan;
  return t ? a`
      <ul class="meta" aria-label="Last scan">
        <li class="chip"><uui-icon name="icon-calendar" aria-hidden="true"></uui-icon>${A(t.startedUtc)}</li>
        <li class="chip"><uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${V(t.pagesDone, "page", "pages")}${t.pagesSkipped ? ` · ${t.pagesSkipped} skipped` : ""}</li>
        ${t.pagesFailed ? a`<li class="chip"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${t.pagesFailed} didn't load</li>` : l}
        <li class="chip"><uui-icon name=${t.trigger === "scheduled" ? "icon-time" : "icon-user"} aria-hidden="true"></uui-icon>${t.trigger === "scheduled" ? "Nightly scan" : `By ${t.userName}`}</li>
      </ul>` : a`<p class="muted">This site hasn't been scanned yet. Scans also run every night (see Settings).</p>`;
};
ke = function() {
  return this._progress ? a`<div class="row"><uui-button look="secondary" label="Cancel scan" @click=${h(this, c, ye)}></uui-button></div>` : a`<div class="row">
      <uui-button look="primary" label="Scan now" .state=${this._starting ? "waiting" : void 0} @click=${h(this, c, be)}>
        <uui-icon name="icon-sync" aria-hidden="true"></uui-icon>&nbsp;Scan now
      </uui-button>
      <span class="muted small">Runs on the server: you can leave this page.</span>
    </div>`;
};
ze = function() {
  const e = this._progress;
  return e ? e.total ? a`
      <div class="progress-bar" aria-hidden="true"><span style=${ue({ width: `${Math.round(e.done / e.total * 100)}%` })}></span></div>
      <p>Scanned ${e.done} of ${V(e.total, "page", "pages")}${e.done === e.total ? ", checking links and the sitemap…" : ""}</p>
      ${e.currentUrl && e.done < e.total ? a`<p class="muted url">${e.currentUrl}</p>` : l}` : a`<p>Starting the scan…</p>` : l;
};
Se = function(e) {
  const t = [
    { severity: "critical", count: e.counts.critical },
    { severity: "warning", count: e.counts.warning },
    { severity: "notice", count: e.counts.notice }
  ], i = e.siteChecks.filter((s) => s.passed !== null);
  return a`
      <section aria-labelledby="sev-heading">
        <h3 id="sev-heading" class="section-label">Open findings</h3>
        <ul class="tiles">
          ${t.map((s) => a`<li>
            <button class="tile lift tone-${s.severity}" @click=${() => J(this, "issues", { severity: s.severity })}>
              <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${Q[s.severity]}></uui-icon></span>
              <span class="tile-text"><span class="tile-value">${s.count}</span><span class="tile-label">${Z[s.severity]}</span></span>
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
          ${e.top.length ? a`
            <ol class="top">
              ${e.top.map((s, r) => a`<li>
                <button class="top-item lift tone-${s.severity}" @click=${() => J(this, "issues", { checkId: s.checkId })}>
                  <span class="rank" aria-hidden="true">${r + 1}</span>
                  <span class="top-main">
                    <span class="top-title">${h(this, c, L).call(this, s.checkId)}</span>
                    <span class="muted small">${s.pages ? V(s.pages, "page", "pages") : "whole site"} · ${ge[s.owner]}</span>
                  </span>
                  <span class="tag tone-${s.severity}"><uui-icon name=${Q[s.severity]}></uui-icon>${Z[s.severity]}</span>
                  <uui-icon class="go" name="icon-navigation-right" aria-hidden="true"></uui-icon>
                </button>
              </li>`)}
            </ol>` : a`
            <div class="empty">
              <span class="icon-badge solid round tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span>
              <p>Nothing to fix. Well done.</p>
            </div>`}
        </section>

        <section class="card" aria-labelledby="site-heading">
          <div class="card-head">
            <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-globe"></uui-icon></span>
            <h3 id="site-heading">Whole site</h3>
            ${i.length ? a`<span class="chip end">${i.filter((s) => s.passed).length} of ${i.length} passed</span>` : l}
          </div>
          <ul class="checklist">
            ${e.siteChecks.map((s) => a`<li>
              ${s.passed === null ? a`<span class="status na" aria-hidden="true"><uui-icon name="icon-circle-dotted"></uui-icon></span><span>${h(this, c, L).call(this, s.checkId)} <span class="muted">(not applicable here)</span></span>` : s.passed ? a`<span class="status tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span><span><span class="visually-hidden">Passed: </span>${h(this, c, L).call(this, s.checkId)}</span>` : a`<span class="status tone-critical" aria-hidden="true"><uui-icon name="icon-wrong"></uui-icon></span><span><span class="visually-hidden">Failed: </span>
                      <a href="#" @click=${(r) => {
    r.preventDefault(), J(this, "issues", { checkId: s.checkId });
  }}>${h(this, c, L).call(this, s.checkId)}</a>
                      ${s.value ? a`<span class="muted">: ${s.value}</span>` : l}</span>`}
            </li>`)}
          </ul>
        </section>
      </div>

      ${e.trend.length ? a`
        <section class="card" aria-labelledby="trend-heading">
          <div class="card-head">
            <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
            <h3 id="trend-heading">Recent scans</h3>
          </div>
          <div class="table-wrap">
            <table>
              <caption class="visually-hidden">Score of the last ${e.trend.length} scans, oldest first</caption>
              <thead><tr><th scope="col">Scan</th><th scope="col">Score</th><th scope="col" class="num">Pages</th></tr></thead>
              <tbody>${e.trend.map((s) => a`<tr>
                <td>${A(s.startedUtc)}</td>
                <td><span class="score-cell tone-${pe(s.score)}">
                  <span class="score-bar" aria-hidden="true"><span style=${ue({ width: `${s.score}%` })}></span></span>
                  <span class="score-num">${s.score}</span>
                </span></td>
                <td class="num">${s.pages}</td>
              </tr>`)}</tbody>
            </table>
          </div>
        </section>` : l}

      ${h(this, c, Y).call(this, "Hidden from search", "icon-block", "Pages that tell search engines not to index them, or that robots.txt blocks. Usually on purpose (login, search, thank-you pages); check that nothing important is here.", e.hidden, (s) => s.reason)}
      ${h(this, c, Y).call(this, "Not scanned", "icon-document-dashed-line", "Published pages the scan left out.", e.skipped, (s) => ot[s.reason] ?? s.reason)}`;
};
Y = function(e, t, i, s, r) {
  return s.length ? a`
      <details class="card disclosure">
        <summary class="card-head">
          <span class="icon-badge tone-none" aria-hidden="true"><uui-icon name=${t}></uui-icon></span>
          <h3>${e}</h3>
          <span class="chip">${s.length}</span>
          <uui-icon class="chevron end" name="icon-navigation-right" aria-hidden="true"></uui-icon>
        </summary>
        <p class="muted">${i}</p>
        <div class="table-wrap">
          <table>
            <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Why</th></tr></thead>
            <tbody>${s.map((n) => a`<tr>
              <td><a href=${ve(n.contentKey, n.culture)}>${n.name}</a>
                ${D(n.url) ? a`<br><a class="muted url" href=${D(n.url)} target="_blank" rel="noopener">${n.url}</a>` : l}</td>
              <td>${n.culture || "–"}</td>
              <td>${r(n)}</td>
            </tr>`)}</tbody>
          </table>
        </div>
      </details>` : l;
};
k.styles = [q, W`
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
    .ring { position: relative; margin: 0; width: ${x.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-3); }
    .ring svg { display: block; filter: drop-shadow(0 8px 18px color-mix(in srgb, var(--tone) 30%, transparent)); }
    .ring .track { stroke: color-mix(in srgb, var(--tone) 24%, var(--uui-color-surface)); }
    .ring .fill.empty { display: none; }
    .stop-a { stop-color: var(--ring-a, var(--tone)); }
    .stop-b { stop-color: var(--ring-b, var(--tone)); }
    .ring-text { position: absolute; top: 0; height: ${x.size}px; display: grid; place-content: center; text-align: center; }
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
O([
  T({ attribute: !1 })
], k.prototype, "siteKey", 2);
O([
  T({ attribute: !1 })
], k.prototype, "knowledge", 2);
O([
  v()
], k.prototype, "_overview", 2);
O([
  v()
], k.prototype, "_progress", 2);
O([
  v()
], k.prototype, "_error", 2);
O([
  v()
], k.prototype, "_starting", 2);
k = O([
  G("esatto-seo-health-overview")
], k);
class ht {
  #e = 0;
  /** Runs `load`; resolves to its result, or to `undefined` when a newer run started meanwhile. */
  async run(t) {
    const i = ++this.#e, s = await t();
    return i === this.#e ? s : void 0;
  }
}
var pt = Object.defineProperty, gt = Object.getOwnPropertyDescriptor, Ee = (e) => {
  throw TypeError(e);
}, $ = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? gt(t, i) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (r = (s ? o(t, i, r) : o(r)) || r);
  return s && r && pt(t, i, r), r;
}, Ie = (e, t, i) => t.has(e) || Ee("Cannot " + i), vt = (e, t, i) => (Ie(e, t, "read from private field"), i ? i.call(e) : t.get(e)), le = (e, t, i) => t.has(e) ? Ee("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), g = (e, t, i) => (Ie(e, t, "access private method"), i), re, d, K, E, Ce, Oe, Ke, Pe, Ne, Te, Ue, Le, De;
let f = class extends B {
  constructor() {
    super(...arguments), le(this, d), this.siteKey = "", this.filter = {}, this.knowledge = null, this._filter = {}, this._groups = null, this._error = null, this._cultures = [], this._ignoring = null, this._note = "", this._busy = /* @__PURE__ */ new Set(), this._status = "", le(this, re, new ht());
  }
  willUpdate(e) {
    e.has("filter") && (this._filter = { ...this.filter });
  }
  updated(e) {
    (e.has("siteKey") || e.has("filter")) && this.siteKey && g(this, d, K).call(this);
  }
  render() {
    return a`
      <div class="page">
        ${g(this, d, Ne).call(this)}
        <p class="visually-hidden" role="status" aria-live="polite">${this._status}</p>
        ${this._error ? a`<p class="error">${this._error}</p>` : l}
        ${this._groups === null ? a`<uui-loader></uui-loader>` : g(this, d, Te).call(this, this._groups)}
      </div>`;
  }
};
re = /* @__PURE__ */ new WeakMap();
d = /* @__PURE__ */ new WeakSet();
K = async function() {
  const e = this.siteKey, t = this._filter, i = await vt(this, re).run(() => m(this, Ze(e, t), { disableNotifications: !0 }));
  if (!i) return;
  const { data: s, error: r } = i;
  if (r || !s) {
    this._error = "Could not load the issues.";
    return;
  }
  this._error = null, this._groups = s;
  const n = new Set(this._cultures);
  s.forEach((o) => o.occurrences.forEach((S) => S.culture && n.add(S.culture))), this._cultures = [...n].sort();
};
E = function(e) {
  this._filter = { ...this._filter, ...e }, g(this, d, K).call(this);
};
Ce = async function() {
  const { data: e } = await m(this, Qe(this.siteKey, this._filter));
  if (!e) return;
  const t = URL.createObjectURL(e), i = document.createElement("a");
  i.href = t, i.download = `seo-issues-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.csv`, i.click(), URL.revokeObjectURL(t);
};
Oe = async function(e) {
  if (!e.contentKey) return;
  const t = `${e.contentKey}|${e.culture}`;
  this._busy = /* @__PURE__ */ new Set([...this._busy, t]), this._status = `Re-checking ${e.name}…`;
  const { error: i } = await m(this, it(this.siteKey, e.contentKey, e.culture));
  this._busy = new Set([...this._busy].filter((s) => s !== t)), this._status = i ? `Re-check of ${e.name} failed.` : `${e.name} re-checked.`, i || await g(this, d, K).call(this);
};
Ke = async function(e) {
  e.preventDefault();
  const t = this._ignoring;
  if (!t || !this._note.trim()) return;
  const { error: i } = await m(this, st(this.siteKey, { checkId: t.checkId, contentKey: t.contentKey, culture: t.culture, note: this._note.trim() }));
  i || (this._status = `Ignored: ${t.label}.`, this._ignoring = null, this._note = "", await g(this, d, K).call(this));
};
Pe = async function(e) {
  const { error: t } = await m(this, at(e));
  t || (this._status = "No longer ignored.", await g(this, d, K).call(this));
};
Ne = function() {
  const e = this._filter, t = (i, s, r, n, o) => a`
      <div class="field">
        <label for=${i}>${s}</label>
        <select id=${i} @change=${(S) => o(S.target.value)}>
          ${n.map(([S, Ge]) => a`<option value=${S} ?selected=${S === r}>${Ge}</option>`)}
        </select>
      </div>`;
  return a`
      <form class="filters card" role="search" @submit=${(i) => {
    i.preventDefault(), g(this, d, K).call(this);
  }}>
        ${t(
    "f-sev",
    "Severity",
    e.severity ?? "",
    [["", "All"], ["critical", "Critical"], ["warning", "Warning"], ["notice", "Notice"]],
    (i) => g(this, d, E).call(this, { severity: i || void 0 })
  )}
        ${t(
    "f-owner",
    "Who fixes it",
    e.owner ?? "",
    [["", "Everyone"], ["editor", "Editors"], ["developer", "Developers"]],
    (i) => g(this, d, E).call(this, { owner: i || void 0 })
  )}
        ${this._cultures.length > 1 ? t(
    "f-culture",
    "Language",
    e.culture ?? "",
    [["", "All"], ...this._cultures.map((i) => [i, i])],
    (i) => g(this, d, E).call(this, { culture: i || void 0 })
  ) : l}
        ${t("f-status", "Show", e.ignored ? "ignored" : "open", [["open", "Open"], ["ignored", "Ignored"]], (i) => g(this, d, E).call(this, { ignored: i === "ignored" }))}
        <div class="field">
          <label for="f-search">Page or address contains</label>
          <input id="f-search" type="search" .value=${e.search ?? ""} @change=${(i) => g(this, d, E).call(this, { search: i.target.value })}>
        </div>
        <div class="filter-actions">
          ${e.checkId ? a`<uui-button look="secondary" label="Show all checks" @click=${() => g(this, d, E).call(this, { checkId: void 0 })}><uui-icon name="icon-filter" aria-hidden="true"></uui-icon>&nbsp;Show all checks</uui-button>` : l}
          <uui-button look="outline" label="Export CSV" @click=${g(this, d, Ce)}><uui-icon name="icon-download-alt" aria-hidden="true"></uui-icon>&nbsp;Export CSV</uui-button>
        </div>
      </form>`;
};
Te = function(e) {
  return e.length ? a`<ul class="groups">${e.map((t) => a`<li>${g(this, d, Ue).call(this, t)}</li>`)}</ul>` : a`
        <div class="card empty">
          <span class="icon-badge solid round tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span>
          <p>${this._filter.ignored ? "Nothing is ignored." : "No open issues match these filters."}</p>
        </div>`;
};
Ue = function(e) {
  const t = e.summary, i = this.knowledge?.texts[t.checkId], s = i?.title ?? t.checkId, r = t.pages === 0;
  return a`
      <details class="group tone-${t.severity}" ?open=${this._filter.checkId === t.checkId}>
        <summary>
          <span class="tag tone-${t.severity}"><uui-icon name=${Q[t.severity]}></uui-icon>${Z[t.severity]}</span>
          <span class="group-title">${s}</span>
          <span class="group-meta">
            <span class="chip"><uui-icon name=${r ? "icon-globe" : "icon-documents"} aria-hidden="true"></uui-icon>${r ? "Whole site" : V(t.pages, "page", "pages")}</span>
            <span class="chip"><uui-icon name=${t.owner === "developer" ? "icon-code" : "icon-edit"} aria-hidden="true"></uui-icon>${ge[t.owner]}</span>
          </span>
          <uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>
        </summary>
        <div class="group-body stack">
          ${i ? a`
            <div class="explain">
              <section class="note"><h4><uui-icon name="icon-info" aria-hidden="true"></uui-icon>Why it matters</h4><p>${i.why}</p></section>
              <section class="note fix"><h4><uui-icon name="icon-wand" aria-hidden="true"></uui-icon>How to fix it</h4><p>${i.fix}</p></section>
              ${i.caveats ? a`<section class="note"><h4><uui-icon name="icon-lightbulb-active" aria-hidden="true"></uui-icon>Good to know</h4><p>${i.caveats}</p></section>` : l}
            </div>
            ${D(i.learnMore) ? a`<p><a class="learn" href=${D(i.learnMore)} target="_blank" rel="noopener">Learn more<span class="visually-hidden"> about ${s} (opens in a new tab)</span><uui-icon name="icon-arrow-right" aria-hidden="true"></uui-icon></a></p>` : l}` : l}
          ${g(this, d, Le).call(this, e, s)}
          ${this._filter.ignored ? l : a`<div class="row">
            <uui-button look="secondary" compact label="Ignore this check on the whole site"
              @click=${() => {
    this._ignoring = { checkId: t.checkId, contentKey: null, culture: null, label: `${s} (whole site)` }, this._note = "";
  }}></uui-button>
          </div>`}
          ${this._ignoring?.checkId === t.checkId ? g(this, d, De).call(this, this._ignoring) : l}
        </div>
      </details>`;
};
Le = function(e, t) {
  return a`
      <div class="table-wrap"><table>
        <caption class="visually-hidden">Pages with: ${t}</caption>
        <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Found</th><th scope="col"><span class="visually-hidden">Actions</span></th></tr></thead>
        <tbody>
          ${e.occurrences.map((i) => {
    const s = D(i.url), r = this._busy.has(`${i.contentKey}|${i.culture}`);
    return a`<tr>
              <td>
                ${i.contentKey ? a`<a href=${ve(i.contentKey, i.culture)}>${i.name}</a>` : a`<span>${i.name}</span>`}
                ${s ? a`<br><a class="muted url" href=${s} target="_blank" rel="noopener">${i.url}<span class="visually-hidden"> (opens in a new tab)</span></a>` : l}
              </td>
              <td>${i.culture || "–"}</td>
              <td>${i.value ? a`<pre>${i.value}</pre>` : a`<span class="muted">–</span>`}</td>
              <td class="actions">
                ${i.ignored && i.ignoreId !== null ? a`<uui-button look="secondary" compact label="Stop ignoring" @click=${() => g(this, d, Pe).call(this, i.ignoreId)}></uui-button>` : i.contentKey ? a`
                    <uui-button look="secondary" compact label="Re-check" .state=${r ? "waiting" : void 0} ?disabled=${r}
                      @click=${() => g(this, d, Oe).call(this, i)}></uui-button>
                    <uui-button look="secondary" compact label="Ignore"
                      @click=${() => {
      this._ignoring = { checkId: e.summary.checkId, contentKey: i.contentKey, culture: i.culture || null, label: `${t} on ${i.name}` }, this._note = "";
    }}></uui-button>` : l}
              </td>
            </tr>`;
  })}
        </tbody>
      </table></div>`;
};
De = function(e) {
  return a`
      <form class="ignore stack" @submit=${g(this, d, Ke)}>
        <p><strong>Ignore: ${e.label}</strong></p>
        <div class="field">
          <label for="ignore-note">Why? (required, shown to others)</label>
          <textarea id="ignore-note" rows="2" required .value=${this._note} @input=${(t) => this._note = t.target.value}></textarea>
        </div>
        <div class="row">
          <uui-button type="submit" look="primary" label="Ignore" ?disabled=${!this._note.trim()}></uui-button>
          <uui-button look="secondary" label="Cancel" @click=${() => this._ignoring = null}></uui-button>
        </div>
      </form>`;
};
f.styles = [q, W`
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: flex-end; padding: var(--uui-size-space-4) var(--uui-size-space-5); }
    .filters input[type='search'] { min-width: 16rem; }
    .filter-actions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); margin-left: auto; }
    textarea { width: min(40rem, 100%); }
    .empty { display: flex; gap: var(--uui-size-space-3); align-items: center; }

    .groups { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
    .group {
      position: relative; background: var(--uui-color-surface); border: 1px solid var(--seo-line); border-radius: var(--seo-radius);
      box-shadow: var(--seo-shadow); overflow: hidden; transition: box-shadow var(--seo-ease), border-color var(--seo-ease);
    }
    /* The severity bar sits above the header's tinted background, so it runs the full height of the card. */
    .group::before { content: ''; position: absolute; inset: 0 auto 0 0; width: 4px; background: var(--tone); z-index: 1; pointer-events: none; }
    .group:hover, .group[open] { border-color: color-mix(in srgb, var(--tone) 45%, var(--seo-line)); }
    .group[open] { box-shadow: var(--seo-shadow-raised); }
    .group > summary {
      display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; min-height: 44px;
      padding: var(--uui-size-space-4) var(--uui-size-space-5) var(--uui-size-space-4) var(--uui-size-space-6);
      transition: background-color var(--seo-ease);
    }
    .group > summary:hover { background: color-mix(in srgb, var(--tone) 6%, var(--uui-color-surface)); }
    .group[open] > summary { background: linear-gradient(90deg, color-mix(in srgb, var(--tone) 10%, var(--uui-color-surface)), var(--uui-color-surface) 70%); border-bottom: 1px solid var(--seo-line); }
    .group-title { font-weight: 700; flex: 1; min-width: 12rem; }
    .group-meta { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
    .group-body { padding: var(--uui-size-space-5) var(--uui-size-space-5) var(--uui-size-space-5) var(--uui-size-space-6); }

    .explain { display: grid; gap: var(--uui-size-space-3); grid-template-columns: repeat(auto-fit, minmax(min(100%, 18rem), 1fr)); }
    .note { display: grid; gap: var(--uui-size-space-2); align-content: start; padding: var(--uui-size-space-4); border-radius: var(--seo-radius-sm); background: var(--seo-wash); border: 1px solid var(--seo-line); }
    .note.fix { background: color-mix(in srgb, var(--seo-good) 7%, var(--uui-color-surface)); border-color: color-mix(in srgb, var(--seo-good) 30%, var(--seo-line)); }
    .note h4 { display: flex; gap: 6px; align-items: center; font-size: 13px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
    .note h4 uui-icon { color: var(--seo-muted); }
    .learn { display: inline-flex; gap: 6px; align-items: center; font-weight: 600; }

    .actions { white-space: nowrap; text-align: right; }
    .ignore {
      padding: var(--uui-size-space-5); border-radius: var(--seo-radius-sm);
      background: color-mix(in srgb, var(--seo-warning) 8%, var(--uui-color-surface)); border: 1px solid color-mix(in srgb, var(--seo-warning) 40%, var(--seo-line));
    }
  `];
$([
  T({ attribute: !1 })
], f.prototype, "siteKey", 2);
$([
  T({ attribute: !1 })
], f.prototype, "filter", 2);
$([
  T({ attribute: !1 })
], f.prototype, "knowledge", 2);
$([
  v()
], f.prototype, "_filter", 2);
$([
  v()
], f.prototype, "_groups", 2);
$([
  v()
], f.prototype, "_error", 2);
$([
  v()
], f.prototype, "_cultures", 2);
$([
  v()
], f.prototype, "_ignoring", 2);
$([
  v()
], f.prototype, "_note", 2);
$([
  v()
], f.prototype, "_busy", 2);
$([
  v()
], f.prototype, "_status", 2);
f = $([
  G("esatto-seo-health-issues")
], f);
var ft = Object.defineProperty, mt = Object.getOwnPropertyDescriptor, Me = (e) => {
  throw TypeError(e);
}, j = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? mt(t, i) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (r = (s ? o(t, i, r) : o(r)) || r);
  return s && r && ft(t, i, r), r;
}, ne = (e, t, i) => t.has(e) || Me("Cannot " + i), bt = (e, t, i) => (ne(e, t, "read from private field"), t.get(e)), ce = (e, t, i) => t.has(e) ? Me("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), yt = (e, t, i, s) => (ne(e, t, "write to private field"), t.set(e, i), i), p = (e, t, i) => (ne(e, t, "access private method"), i), F, u, C, I, We, P, He, ee;
const Ae = (e) => JSON.parse(JSON.stringify(e)), wt = Array.from({ length: 24 }, (e, t) => t);
let N = class extends B {
  constructor() {
    super(), ce(this, u), this.settings = null, this._draft = null, this._saving = !1, ce(this, F), this.consumeContext(Be, (e) => yt(this, F, e));
  }
  willUpdate(e) {
    e.has("settings") && this.settings && (this._draft = Ae(this.settings.settings));
  }
  render() {
    const e = this._draft;
    return e ? a`
      <form class="page" @submit=${p(this, u, We)}>
        ${this.settings?.locked.length ? a`<p class="muted">Fields marked “Set in configuration” come from appsettings (<code>Esatto:Seo</code>) and can't be changed here.</p>` : l}

        <div class="cards">
        <section class="card" aria-labelledby="h-nightly">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-time"></uui-icon></span><h3 id="h-nightly">Nightly scan</h3></div>
          <div class="stack">
            ${p(this, u, He).call(this, "schedule.enabled", "Scan every night", e.schedule.enabled, (t) => p(this, u, I).call(this, (i) => i.schedule.enabled = t), "Keeps the score and trend up to date without anyone pressing Scan now.")}
            <div class="field">
              <label for="hour">Start time (server time)</label>
              <select id="hour" ?disabled=${!e.schedule.enabled || p(this, u, C).call(this, "schedule.hour")}
                @change=${(t) => p(this, u, I).call(this, (i) => i.schedule.hour = Number(t.target.value))}>
                ${wt.map((t) => a`<option value=${t} ?selected=${t === e.schedule.hour}>${String(t).padStart(2, "0")}:00</option>`)}
              </select>
              ${p(this, u, P).call(this, "schedule.hour")}
            </div>
          </div>
        </section>

        <section class="card" aria-labelledby="h-what">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-sitemap"></uui-icon></span><h3 id="h-what">What to scan</h3></div>
          <div class="stack">
            <div class="field">
              <label for="exclude">Never scan these paths (one per line, e.g. /search)</label>
              <textarea id="exclude" rows="3" ?disabled=${p(this, u, C).call(this, "excludePaths")} .value=${e.excludePaths.join(`
`)}
                @change=${(t) => p(this, u, I).call(this, (i) => i.excludePaths = t.target.value.split(`
`).map((s) => s.trim()).filter(Boolean))}></textarea>
              ${p(this, u, P).call(this, "excludePaths")}
            </div>
            <div class="field">
              <label for="base">Base URL for pages without a domain <span class="muted">(normally empty)</span></label>
              <input id="base" type="url" placeholder="https://www.example.com" ?disabled=${p(this, u, C).call(this, "baseUrl")} .value=${e.baseUrl ?? ""}
                aria-describedby="base-hint" @change=${(t) => p(this, u, I).call(this, (i) => i.baseUrl = t.target.value.trim() || null)}>
              <p id="base-hint" class="muted hint">Only needed when no domain is bound in Culture and Hostnames. Otherwise pages are fetched at their own addresses.</p>
              ${p(this, u, P).call(this, "baseUrl")}
            </div>
          </div>
        </section>

        <section class="card" aria-labelledby="h-load">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-speed-gauge"></uui-icon></span><h3 id="h-load">Load on the site</h3></div>
          <div class="row">
            ${p(this, u, ee).call(this, "parallelism", "Pages fetched at the same time", e.parallelism, 1, 8, (t) => p(this, u, I).call(this, (i) => i.parallelism = t))}
            ${p(this, u, ee).call(this, "delayMs", "Pause between requests (ms)", e.delayMs, 0, 5e3, (t) => p(this, u, I).call(this, (i) => i.delayMs = t))}
          </div>
        </section>

        <section class="card" aria-labelledby="h-env">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-server"></uui-icon></span><h3 id="h-env">This environment</h3></div>
          <p>${this.settings?.intentionallyHidden ? a`<uui-icon name="icon-lock"></uui-icon> Marked as hidden from search engines on purpose (<code>Esatto:Seo:IntentionallyHidden</code>).` : a`Not marked as hidden on purpose. On a Test or Dev environment that blocks search engines, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.`}</p>
        </section>
        </div>

        <div class="row">
          <uui-button type="submit" look="primary" color="positive" label="Save settings" .state=${this._saving ? "waiting" : void 0}></uui-button>
        </div>
      </form>` : a`<uui-loader></uui-loader>`;
  }
};
F = /* @__PURE__ */ new WeakMap();
u = /* @__PURE__ */ new WeakSet();
C = function(e) {
  return (this.settings?.locked ?? []).some((t) => t.toLowerCase() === e.toLowerCase());
};
I = function(e) {
  const t = Ae(this._draft);
  e(t), this._draft = t;
};
We = async function(e) {
  if (e.preventDefault(), !this._draft) return;
  this._saving = !0;
  const { data: t, error: i } = await m(this, Je(this._draft));
  this._saving = !1, !(i || !t) && (bt(this, F)?.peek("positive", { data: { headline: "Settings saved", message: "They apply from the next scan." } }), this.dispatchEvent(new CustomEvent("saved", { detail: t })));
};
P = function(e) {
  return p(this, u, C).call(this, e) ? a`<span class="lock"><uui-icon name="icon-lock"></uui-icon> Set in configuration</span>` : l;
};
He = function(e, t, i, s, r) {
  const n = `c-${e.split(".").join("-")}`;
  return a`
      <div class="check">
        <input id=${n} type="checkbox" .checked=${i} ?disabled=${p(this, u, C).call(this, e)} aria-describedby=${r ? `${n}-hint` : l}
          @change=${(o) => s(o.target.checked)}>
        <label for=${n}>${t}</label>
        ${p(this, u, P).call(this, e)}
        ${r ? a`<p id="${n}-hint" class="muted hint">${r}</p>` : l}
      </div>`;
};
ee = function(e, t, i, s, r, n) {
  return a`
      <div class="field">
        <label for="n-${e}">${t}</label>
        <input id="n-${e}" type="number" min=${s} max=${r} ?disabled=${p(this, u, C).call(this, e)} .value=${String(i)}
          @change=${(o) => n(Math.min(r, Math.max(s, Number(o.target.value) || s)))}>
        ${p(this, u, P).call(this, e)}
      </div>`;
};
N.styles = [q, W`
    .cards { display: grid; gap: var(--seo-gap); grid-template-columns: repeat(auto-fit, minmax(min(100%, 26rem), 1fr)); align-items: stretch; }
    .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); align-items: center; min-height: 44px; }
    .check input { width: 20px; height: 20px; margin: 0; accent-color: var(--uui-color-selected); }
    .check > label { font-weight: 600; }
    .check .hint, .check .lock { grid-column: 2; margin: 0; }
    select, input:not([type='checkbox']), textarea { width: 100%; max-width: 32rem; }
    .hint { font-size: 13px; }
    .lock { color: var(--seo-muted); font-size: 13px; display: inline-flex; gap: 4px; align-items: center; }
  `];
j([
  T({ attribute: !1 })
], N.prototype, "settings", 2);
j([
  v()
], N.prototype, "_draft", 2);
j([
  v()
], N.prototype, "_saving", 2);
N = j([
  G("esatto-seo-health-settings")
], N);
var $t = Object.defineProperty, xt = Object.getOwnPropertyDescriptor, Re = (e) => {
  throw TypeError(e);
}, z = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? xt(t, i) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (r = (s ? o(t, i, r) : o(r)) || r);
  return s && r && $t(t, i, r), r;
}, _t = (e, t, i) => t.has(e) || Re("Cannot " + i), kt = (e, t, i) => t.has(e) ? Re("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), zt = (e, t, i) => (_t(e, t, "access private method"), i), te, Fe;
const St = [
  { id: "overview", label: "Overview" },
  { id: "issues", label: "Issues" },
  { id: "settings", label: "Settings" }
];
let _ = class extends B {
  constructor() {
    super(), kt(this, te), this._knowledge = null, this._sites = [], this._siteKey = "", this._settings = null, this._tab = "overview", this._filter = {}, this._error = null, this.addEventListener(he, (e) => {
      const { tab: t, filter: i } = e.detail;
      this._filter = i ?? {}, this._tab = t;
    });
  }
  async connectedCallback() {
    super.connectedCallback();
    const [e, t, i] = await Promise.all([
      m(this, qe(), { disableNotifications: !0 }),
      m(this, Ve(), { disableNotifications: !0 }),
      m(this, je(), { disableNotifications: !0 })
    ]);
    if (e.error || t.error || i.error || !e.data || !t.data || !i.data) {
      this._error = "Could not load the SEO Health dashboard.";
      return;
    }
    this._knowledge = e.data, this._sites = t.data, this._siteKey = t.data[0]?.key ?? "", this._settings = i.data;
  }
  render() {
    if (this._error) return a`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._settings || !this._knowledge) return a`<uui-loader></uui-loader>`;
    if (!this._sites.length)
      return a`<uui-box headline="SEO Health"><p>There is no published site to scan yet. Publish a root page with a template first.</p></uui-box>`;
    const e = St.filter((t) => t.id !== "settings" || this._settings.canEdit);
    return a`
      <div class="header">
        <div class="brand">
          <span class="logo" aria-hidden="true"><uui-icon name="icon-pulse"></uui-icon></span>
          <div>
            <h2>SEO Health</h2>
            <p class="muted small">Search engine checks on every published page, in every language.</p>
          </div>
        </div>
        ${this._sites.length > 1 ? a`
          <uui-select label="Site" .options=${this._sites.map((t) => ({ name: t.name, value: t.key, selected: t.key === this._siteKey }))}
            @change=${(t) => this._siteKey = t.target.value}></uui-select>` : l}
      </div>
      <uui-tab-group>
        ${e.map((t) => a`<uui-tab id="tab-${t.id}" aria-controls="tabpanel" label=${t.label} ?active=${this._tab === t.id}
          @click=${() => {
      this._filter = {}, this._tab = t.id;
    }}>${t.label}</uui-tab>`)}
      </uui-tab-group>
      <div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${this._tab}">${zt(this, te, Fe).call(this)}</div>`;
  }
};
te = /* @__PURE__ */ new WeakSet();
Fe = function() {
  switch (this._tab) {
    case "issues":
      return a`<esatto-seo-health-issues .siteKey=${this._siteKey} .filter=${this._filter} .knowledge=${this._knowledge}></esatto-seo-health-issues>`;
    case "settings":
      return a`<esatto-seo-health-settings .settings=${this._settings} @saved=${(e) => this._settings = e.detail}></esatto-seo-health-settings>`;
    default:
      return a`<esatto-seo-health-overview .siteKey=${this._siteKey} .knowledge=${this._knowledge}></esatto-seo-health-overview>`;
  }
};
_.styles = [q, W`
    :host { padding: var(--uui-size-layout-1); max-width: 1440px; }
    .header { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; justify-content: space-between; margin-bottom: var(--uui-size-space-5); }
    .brand { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    .brand h2 { font-size: 24px; font-weight: 700; letter-spacing: -0.01em; line-height: 1.2; }
    .logo {
      display: grid; place-items: center; width: 44px; height: 44px; border-radius: 12px; font-size: 22px; color: #fff;
      background: linear-gradient(135deg, #2f6fde 0%, #6d4ce0 100%); box-shadow: 0 6px 16px -6px rgb(77 79 224 / 0.6);
    }
    uui-tab-group { border-bottom: 1px solid var(--seo-line); }
    .panel { padding-top: var(--uui-size-space-6); }
  `];
z([
  v()
], _.prototype, "_knowledge", 2);
z([
  v()
], _.prototype, "_sites", 2);
z([
  v()
], _.prototype, "_siteKey", 2);
z([
  v()
], _.prototype, "_settings", 2);
z([
  v()
], _.prototype, "_tab", 2);
z([
  v()
], _.prototype, "_filter", 2);
z([
  v()
], _.prototype, "_error", 2);
_ = z([
  G("esatto-seo-health-dashboard")
], _);
const Pt = _;
export {
  _ as EsattoSeoDashboardElement,
  Pt as default
};
//# sourceMappingURL=seo-dashboard.js.map
