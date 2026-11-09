import { css as W, property as T, state as g, customElement as F, html as s, nothing as o } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as G } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as $ } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as m } from "@umbraco-cms/backoffice/http-client";
import { UMB_NOTIFICATION_CONTEXT as Re } from "@umbraco-cms/backoffice/notification";
const y = "/umbraco/management/api/v1/esatto/seo-health", _ = () => [{ scheme: "bearer", type: "http" }], ee = { "Content-Type": "application/json" }, U = (e) => `site=${encodeURIComponent(e)}`;
function ce(e, t) {
  const i = new URLSearchParams({ site: e });
  return t.severity && i.set("severity", t.severity), t.owner && i.set("owner", t.owner), t.culture && i.set("culture", t.culture), t.checkId && i.set("checkId", t.checkId), t.ignored && i.set("ignored", "true"), t.search?.trim() && i.set("search", t.search.trim()), i.toString();
}
const Ae = () => m.get({ url: `${y}/knowledge`, security: _() }), Fe = () => m.get({ url: `${y}/sites`, security: _() }), Ge = () => m.get({ url: `${y}/settings`, security: _() }), Be = (e) => m.put({ url: `${y}/settings`, body: e, security: _(), headers: ee }), qe = (e) => m.get({ url: `${y}/overview?${U(e)}`, security: _() }), Ve = (e, t) => m.get({ url: `${y}/issues?${ce(e, t)}`, security: _() }), je = (e, t) => m.get({ url: `${y}/issues/csv?${ce(e, t)}`, security: _(), parseAs: "blob" }), Je = (e) => m.post({ url: `${y}/scans?${U(e)}`, security: _() }), Ze = (e) => m.get({ url: `${y}/scans/progress?${U(e)}`, security: _() }), Qe = (e) => m.delete({ url: `${y}/scans/current?${U(e)}`, security: _() }), Xe = (e, t, i) => m.post({ url: `${y}/recheck?${U(e)}`, body: { contentKey: t, culture: i }, security: _(), headers: ee }), Ye = (e, t) => m.post({ url: `${y}/ignores?${U(e)}`, body: t, security: _(), headers: ee }), et = (e) => m.delete({ url: `${y}/ignores/${e}`, security: _() }), B = W`
  :host {
    display: block;
    --seo-good: #0ca30c;
    --seo-warning: #fab219;
    --seo-critical: #d03b3b;
    --seo-info: var(--uui-color-selected, #3544b1);
  }
  .muted { color: var(--uui-color-text-alt); }
  .row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .stack > * + * { margin-top: var(--uui-size-space-4); }
  .error { color: var(--uui-color-danger-standalone); }
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; vertical-align: top; padding: var(--uui-size-space-2) var(--uui-size-space-3); border-bottom: 1px solid var(--uui-color-divider); }
  th { font-weight: 600; color: var(--uui-color-text-alt); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  pre { white-space: pre-wrap; word-break: break-word; margin: 0; }
  a { color: var(--uui-color-interactive); }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .tag {
    display: inline-flex; gap: 4px; align-items: center; padding: 0 var(--uui-size-space-2); border-radius: var(--uui-border-radius);
    border: 1px solid var(--uui-color-border); font-size: 0.85em; line-height: 1.6; white-space: nowrap;
  }
  .sev-critical uui-icon { color: var(--seo-critical); }
  .sev-warning uui-icon { color: var(--seo-warning); }
  .sev-notice uui-icon { color: var(--seo-info); }
  .banner { display: flex; gap: var(--uui-size-space-3); align-items: flex-start; padding: var(--uui-size-space-4); border-radius: var(--uui-border-radius);
    background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); }
  .banner uui-icon { flex: none; margin-top: 2px; }
  .banner.critical { border-left: 6px solid var(--seo-critical); }
  .banner.critical uui-icon { color: var(--seo-critical); }
  .banner.warning { border-left: 6px solid var(--seo-warning); }
  .banner.info { border-left: 6px solid var(--seo-info); background: color-mix(in srgb, var(--seo-info) 6%, var(--uui-color-surface)); }
  .banner.info uui-icon { color: var(--seo-info); }
  details > summary { cursor: pointer; }
`, ue = "esatto-seo-navigate";
function j(e, t, i) {
  e.dispatchEvent(new CustomEvent(ue, { detail: { tab: t, filter: i }, bubbles: !0, composed: !0 }));
}
function tt(e) {
  return e ? new Date(/[zZ]|[+-]\d\d:\d\d$/.test(e) ? e : `${e}Z`) : null;
}
function H(e) {
  const t = tt(e);
  return t ? new Intl.DateTimeFormat(void 0, { dateStyle: "medium", timeStyle: "short" }).format(t) : "";
}
function it(e) {
  return e === null ? null : e < 50 ? "critical" : e < 90 ? "warning" : "good";
}
const re = { good: "Good", warning: "Needs work", critical: "Serious problems" }, J = { critical: "Critical", warning: "Warning", notice: "Notice" }, Z = { critical: "icon-wrong", warning: "icon-alert", notice: "icon-info" }, de = { editor: "Editor", developer: "Developer" }, st = {
  protected: "Members only",
  excluded: "Excluded in Settings",
  "no-url": "No address (no domain bound and no base URL)"
}, q = (e, t, i) => `${e} ${e === 1 ? t : i}`;
function he(e, t) {
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
var at = Object.defineProperty, rt = Object.getOwnPropertyDescriptor, pe = (e) => {
  throw TypeError(e);
}, K = (e, t, i, r) => {
  for (var a = r > 1 ? void 0 : r ? rt(t, i) : t, n = e.length - 1, l; n >= 0; n--)
    (l = e[n]) && (a = (r ? l(t, i, a) : l(a)) || a);
  return r && a && at(t, i, a), a;
}, te = (e, t, i) => t.has(e) || pe("Cannot " + i), ge = (e, t, i) => (te(e, t, "read from private field"), t.get(e)), ne = (e, t, i) => t.has(e) ? pe("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), nt = (e, t, i, r) => (te(e, t, "write to private field"), t.set(e, i), i), v = (e, t, i) => (te(e, t, "access private method"), i), M, h, ie, R, ve, fe, L, be, $e, me, ye, _e, we, Q;
const f = { size: 168, stroke: 14 }, ot = 2e3;
let x = class extends G {
  constructor() {
    super(...arguments), ne(this, h), this.siteKey = "", this.knowledge = null, this._overview = null, this._progress = null, this._error = null, this._starting = !1, ne(this, M);
  }
  updated(e) {
    e.has("siteKey") && this.siteKey && v(this, h, ie).call(this);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.clearTimeout(ge(this, M));
  }
  render() {
    if (this._error) return s`<uui-box><p class="error">${this._error}</p></uui-box>`;
    const e = this._overview;
    return e ? s`
      <div class="stack">
        ${v(this, h, be).call(this, e)}
        <uui-box>
          <div class="hero">
            ${v(this, h, $e).call(this, e.score)}
            <div class="stack">
              ${v(this, h, me).call(this, e)}
              ${v(this, h, ye).call(this)}
              <div aria-live="polite" class="progress">${v(this, h, _e).call(this)}</div>
              <details>
                <summary>How is the score worked out?</summary>
                <p>Each page that search engines may index scores 0–100: zero if it has a critical problem, otherwise the share of checks it passes (critical checks weigh 10, warnings 3, notices 1). The site score is the average, reduced by any failed whole-site check. Ignored findings don't count.</p>
              </details>
            </div>
          </div>
        </uui-box>
        ${e.lastScan ? v(this, h, we).call(this, e) : o}
      </div>` : s`<uui-loader></uui-loader>`;
  }
};
M = /* @__PURE__ */ new WeakMap();
h = /* @__PURE__ */ new WeakSet();
ie = async function() {
  const { data: e, error: t } = await $(this, qe(this.siteKey), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load the overview.";
    return;
  }
  this._error = null, this._overview = e, this._progress = e.running, e.running && v(this, h, R).call(this);
};
R = function() {
  window.clearTimeout(ge(this, M)), nt(this, M, window.setTimeout(async () => {
    const { data: e, response: t } = await $(this, Ze(this.siteKey), { disableNotifications: !0 });
    t?.status === 200 && e ? (this._progress = e, v(this, h, R).call(this)) : (this._progress = null, await v(this, h, ie).call(this));
  }, ot));
};
ve = async function() {
  this._starting = !0;
  const { error: e } = await $(this, Je(this.siteKey));
  this._starting = !1, !e && (this._progress = { scanId: 0, done: 0, total: 0, currentUrl: null }, v(this, h, R).call(this));
};
fe = async function() {
  await $(this, Qe(this.siteKey), { disableNotifications: !0 });
};
L = function(e) {
  return this.knowledge?.texts[e]?.title ?? e;
};
be = function(e) {
  const t = e.latestAttempt;
  return s`
      ${e.siteHidden && !e.intentionallyHidden ? s`
        <div class="banner critical" role="alert"><uui-icon name="icon-alert"></uui-icon><div>
          <strong>The whole site is hidden from search engines.</strong>
          <p>That's expected on a Test or Dev environment. On the live site it's the most serious problem there is. To mark this environment as hidden on purpose, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.</p>
        </div></div>` : o}
      ${e.siteHidden && e.intentionallyHidden ? s`
        <div class="banner info"><uui-icon name="icon-info"></uui-icon><div>
          <strong>This environment is hidden from search engines on purpose.</strong>
          <p>Pages are checked as if they were public, so content problems still show up here before they reach the live site.</p>
        </div></div>` : o}
      ${t && t.status !== "complete" ? s`
        <div class="banner warning"><uui-icon name="icon-alert"></uui-icon><div>
          <strong>The latest scan (${H(t.startedUtc)}) ${t.status === "cancelled" ? "was cancelled" : t.status === "incomplete" ? "was interrupted by a restart" : "couldn't run"}.</strong>
          ${t.message ? s`<p>${t.message}</p>` : o}
          ${e.lastScan ? s`<p class="muted">The results below are from ${H(e.lastScan.startedUtc)}.</p>` : o}
        </div></div>` : o}`;
};
$e = function(e) {
  const t = it(e), i = (f.size - f.stroke) / 2, r = 2 * Math.PI * i, a = (e ?? 0) / 100 * r, n = e === null ? "No score yet" : `SEO score ${e} out of 100, ${re[t]}`;
  return s`
      <figure class="ring ${t ?? "none"}" role="img" aria-label=${n}>
        <svg width=${f.size} height=${f.size} viewBox="0 0 ${f.size} ${f.size}" aria-hidden="true">
          <circle class="track" cx=${f.size / 2} cy=${f.size / 2} r=${i} stroke-width=${f.stroke} fill="none"></circle>
          <circle class="fill" cx=${f.size / 2} cy=${f.size / 2} r=${i} stroke-width=${f.stroke} fill="none"
            stroke-dasharray="${a} ${r}" stroke-linecap="round" transform="rotate(-90 ${f.size / 2} ${f.size / 2})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true"><span class="ring-value">${e ?? "–"}</span><span class="muted">of 100</span></div>
        <figcaption aria-hidden="true" class="ring-band">
          ${t ? s`<uui-icon name=${t === "good" ? "icon-check" : t === "warning" ? "icon-alert" : "icon-wrong"}></uui-icon> ${re[t]}` : "Not scanned yet"}
        </figcaption>
      </figure>`;
};
me = function(e) {
  const t = e.lastScan;
  return t ? s`<p>
      Last scan: ${H(t.startedUtc)} · ${q(t.pagesDone, "page", "pages")}${t.pagesSkipped ? ` (${t.pagesSkipped} skipped)` : ""}${t.pagesFailed ? ` · ${t.pagesFailed} didn't load` : ""}
      · ${t.trigger === "scheduled" ? "nightly scan" : `by ${t.userName}`}</p>` : s`<p class="muted">This site hasn't been scanned yet. Scans also run every night (see Settings).</p>`;
};
ye = function() {
  return this._progress ? s`<div class="row"><uui-button look="secondary" label="Cancel scan" @click=${v(this, h, fe)}></uui-button></div>` : s`<div class="row">
      <uui-button look="primary" label="Scan now" .state=${this._starting ? "waiting" : void 0} @click=${v(this, h, ve)}></uui-button>
      <span class="muted">Runs on the server: you can leave this page.</span>
    </div>`;
};
_e = function() {
  const e = this._progress;
  return e ? e.total ? s`
      <uui-loader-bar progress=${Math.round(e.done / e.total * 100)} aria-hidden="true"></uui-loader-bar>
      <p>Scanned ${e.done} of ${q(e.total, "page", "pages")}${e.done === e.total ? ", checking links and the sitemap…" : ""}</p>
      ${e.currentUrl && e.done < e.total ? s`<p class="muted url">${e.currentUrl}</p>` : o}` : s`<p>Starting the scan…</p>` : o;
};
we = function(e) {
  const t = [
    { severity: "critical", count: e.counts.critical },
    { severity: "warning", count: e.counts.warning },
    { severity: "notice", count: e.counts.notice }
  ];
  return s`
      <section aria-labelledby="first-heading">
        <h3 id="first-heading" class="section-title">Fix these first</h3>
        ${e.top.length ? s`
          <ol class="top">
            ${e.top.map((i) => s`<li>
              <button class="top-item" @click=${() => j(this, "issues", { checkId: i.checkId })}>
                <span class="tag sev-${i.severity}"><uui-icon name=${Z[i.severity]}></uui-icon>${J[i.severity]}</span>
                <span class="top-title">${v(this, h, L).call(this, i.checkId)}</span>
                <span class="muted">${i.pages ? q(i.pages, "page", "pages") : "whole site"} · ${de[i.owner]}</span>
              </button>
            </li>`)}
          </ol>` : s`<p><uui-icon name="icon-check"></uui-icon> Nothing to fix. Well done.</p>`}
      </section>

      <section aria-labelledby="sev-heading">
        <h3 id="sev-heading" class="section-title">Open findings</h3>
        <ul class="tiles">
          ${t.map((i) => s`<li>
            <button class="tile sev-${i.severity}" @click=${() => j(this, "issues", { severity: i.severity })}>
              <span class="tile-label"><uui-icon name=${Z[i.severity]}></uui-icon> ${J[i.severity]}</span>
              <span class="tile-value">${i.count}</span>
            </button>
          </li>`)}
        </ul>
      </section>

      <uui-box headline="Whole site">
        <ul class="checklist">
          ${e.siteChecks.map((i) => s`<li>
            ${i.passed === null ? s`<uui-icon name="icon-remove" aria-hidden="true"></uui-icon><span>${v(this, h, L).call(this, i.checkId)} <span class="muted">(not applicable here)</span></span>` : i.passed ? s`<uui-icon class="ok" name="icon-check" aria-hidden="true"></uui-icon><span><span class="visually-hidden">Passed: </span>${v(this, h, L).call(this, i.checkId)}</span>` : s`<uui-icon class="bad" name="icon-wrong" aria-hidden="true"></uui-icon><span><span class="visually-hidden">Failed: </span>
                    <a href="#" @click=${(r) => {
    r.preventDefault(), j(this, "issues", { checkId: i.checkId });
  }}>${v(this, h, L).call(this, i.checkId)}</a>
                    ${i.value ? s`<span class="muted">: ${i.value}</span>` : o}</span>`}
          </li>`)}
        </ul>
      </uui-box>

      ${v(this, h, Q).call(this, `Hidden from search (${e.hidden.length})`, "Pages that tell search engines not to index them, or that robots.txt blocks. Usually on purpose (login, search, thank-you pages); check that nothing important is here.", e.hidden, (i) => i.reason)}
      ${v(this, h, Q).call(this, `Not scanned (${e.skipped.length})`, "Published pages the scan left out.", e.skipped, (i) => st[i.reason] ?? i.reason)}

      ${e.trend.length ? s`
        <uui-box headline="Recent scans">
          <table>
            <caption class="visually-hidden">Score of the last ${e.trend.length} scans, oldest first</caption>
            <thead><tr><th scope="col">Scan</th><th scope="col" class="num">Score</th><th scope="col" class="num">Pages</th></tr></thead>
            <tbody>${e.trend.map((i) => s`<tr><td>${H(i.startedUtc)}</td><td class="num">${i.score}</td><td class="num">${i.pages}</td></tr>`)}</tbody>
          </table>
        </uui-box>` : o}`;
};
Q = function(e, t, i, r) {
  return i.length ? s`
      <uui-box>
        <details>
          <summary><strong>${e}</strong></summary>
          <p class="muted">${t}</p>
          <table>
            <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Why</th></tr></thead>
            <tbody>${i.map((a) => s`<tr>
              <td><a href=${he(a.contentKey, a.culture)}>${a.name}</a>
                ${D(a.url) ? s`<br><a class="muted url" href=${D(a.url)} target="_blank" rel="noopener">${a.url}</a>` : o}</td>
              <td>${a.culture || "–"}</td>
              <td>${r(a)}</td>
            </tr>`)}</tbody>
          </table>
        </details>
      </uui-box>` : o;
};
x.styles = [B, W`
    .hero { display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; }
    .hero > .stack { flex: 1; min-width: 16rem; }
    .ring { position: relative; margin: 0; width: ${f.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-2); }
    .ring svg { display: block; }
    .ring .track { stroke: var(--uui-color-surface-alt); }
    .ring.good .fill { stroke: var(--seo-good); }
    .ring.warning .fill { stroke: var(--seo-warning); }
    .ring.critical .fill { stroke: var(--seo-critical); }
    .ring-text { position: absolute; top: 0; height: ${f.size}px; display: grid; place-content: center; text-align: center; }
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
K([
  T({ attribute: !1 })
], x.prototype, "siteKey", 2);
K([
  T({ attribute: !1 })
], x.prototype, "knowledge", 2);
K([
  g()
], x.prototype, "_overview", 2);
K([
  g()
], x.prototype, "_progress", 2);
K([
  g()
], x.prototype, "_error", 2);
K([
  g()
], x.prototype, "_starting", 2);
x = K([
  F("esatto-seo-overview")
], x);
class lt {
  #e = 0;
  /** Runs `load`; resolves to its result, or to `undefined` when a newer run started meanwhile. */
  async run(t) {
    const i = ++this.#e, r = await t();
    return i === this.#e ? r : void 0;
  }
}
var ct = Object.defineProperty, ut = Object.getOwnPropertyDescriptor, ke = (e) => {
  throw TypeError(e);
}, w = (e, t, i, r) => {
  for (var a = r > 1 ? void 0 : r ? ut(t, i) : t, n = e.length - 1, l; n >= 0; n--)
    (l = e[n]) && (a = (r ? l(t, i, a) : l(a)) || a);
  return r && a && ct(t, i, a), a;
}, xe = (e, t, i) => t.has(e) || ke("Cannot " + i), dt = (e, t, i) => (xe(e, t, "read from private field"), i ? i.call(e) : t.get(e)), oe = (e, t, i) => t.has(e) ? ke("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), p = (e, t, i) => (xe(e, t, "access private method"), i), se, u, O, E, Se, ze, Ee, Ie, Ce, Ke, Oe, Pe, Ne;
let b = class extends G {
  constructor() {
    super(...arguments), oe(this, u), this.siteKey = "", this.filter = {}, this.knowledge = null, this._filter = {}, this._groups = null, this._error = null, this._cultures = [], this._ignoring = null, this._note = "", this._busy = /* @__PURE__ */ new Set(), this._status = "", oe(this, se, new lt());
  }
  willUpdate(e) {
    e.has("filter") && (this._filter = { ...this.filter });
  }
  updated(e) {
    (e.has("siteKey") || e.has("filter")) && this.siteKey && p(this, u, O).call(this);
  }
  render() {
    return s`
      <div class="stack">
        ${p(this, u, Ce).call(this)}
        <p class="visually-hidden" role="status" aria-live="polite">${this._status}</p>
        ${this._error ? s`<p class="error">${this._error}</p>` : o}
        ${this._groups === null ? s`<uui-loader></uui-loader>` : p(this, u, Ke).call(this, this._groups)}
      </div>`;
  }
};
se = /* @__PURE__ */ new WeakMap();
u = /* @__PURE__ */ new WeakSet();
O = async function() {
  const e = this.siteKey, t = this._filter, i = await dt(this, se).run(() => $(this, Ve(e, t), { disableNotifications: !0 }));
  if (!i) return;
  const { data: r, error: a } = i;
  if (a || !r) {
    this._error = "Could not load the issues.";
    return;
  }
  this._error = null, this._groups = r;
  const n = new Set(this._cultures);
  r.forEach((l) => l.occurrences.forEach((z) => z.culture && n.add(z.culture))), this._cultures = [...n].sort();
};
E = function(e) {
  this._filter = { ...this._filter, ...e }, p(this, u, O).call(this);
};
Se = async function() {
  const { data: e } = await $(this, je(this.siteKey, this._filter));
  if (!e) return;
  const t = URL.createObjectURL(e), i = document.createElement("a");
  i.href = t, i.download = `seo-issues-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.csv`, i.click(), URL.revokeObjectURL(t);
};
ze = async function(e) {
  if (!e.contentKey) return;
  const t = `${e.contentKey}|${e.culture}`;
  this._busy = /* @__PURE__ */ new Set([...this._busy, t]), this._status = `Re-checking ${e.name}…`;
  const { error: i } = await $(this, Xe(this.siteKey, e.contentKey, e.culture));
  this._busy = new Set([...this._busy].filter((r) => r !== t)), this._status = i ? `Re-check of ${e.name} failed.` : `${e.name} re-checked.`, i || await p(this, u, O).call(this);
};
Ee = async function(e) {
  e.preventDefault();
  const t = this._ignoring;
  if (!t || !this._note.trim()) return;
  const { error: i } = await $(this, Ye(this.siteKey, { checkId: t.checkId, contentKey: t.contentKey, culture: t.culture, note: this._note.trim() }));
  i || (this._status = `Ignored: ${t.label}.`, this._ignoring = null, this._note = "", await p(this, u, O).call(this));
};
Ie = async function(e) {
  const { error: t } = await $(this, et(e));
  t || (this._status = "No longer ignored.", await p(this, u, O).call(this));
};
Ce = function() {
  const e = this._filter, t = (i, r, a, n, l) => s`
      <div class="field">
        <label for=${i}>${r}</label>
        <select id=${i} @change=${(z) => l(z.target.value)}>
          ${n.map(([z, He]) => s`<option value=${z} ?selected=${z === a}>${He}</option>`)}
        </select>
      </div>`;
  return s`
      <form class="filters" role="search" @submit=${(i) => {
    i.preventDefault(), p(this, u, O).call(this);
  }}>
        ${t(
    "f-sev",
    "Severity",
    e.severity ?? "",
    [["", "All"], ["critical", "Critical"], ["warning", "Warning"], ["notice", "Notice"]],
    (i) => p(this, u, E).call(this, { severity: i || void 0 })
  )}
        ${t(
    "f-owner",
    "Who fixes it",
    e.owner ?? "",
    [["", "Everyone"], ["editor", "Editors"], ["developer", "Developers"]],
    (i) => p(this, u, E).call(this, { owner: i || void 0 })
  )}
        ${this._cultures.length > 1 ? t(
    "f-culture",
    "Language",
    e.culture ?? "",
    [["", "All"], ...this._cultures.map((i) => [i, i])],
    (i) => p(this, u, E).call(this, { culture: i || void 0 })
  ) : o}
        ${t("f-status", "Show", e.ignored ? "ignored" : "open", [["open", "Open"], ["ignored", "Ignored"]], (i) => p(this, u, E).call(this, { ignored: i === "ignored" }))}
        <div class="field">
          <label for="f-search">Page or address contains</label>
          <input id="f-search" type="search" .value=${e.search ?? ""} @change=${(i) => p(this, u, E).call(this, { search: i.target.value })}>
        </div>
        ${e.checkId ? s`<uui-button look="secondary" label="Show all checks" @click=${() => p(this, u, E).call(this, { checkId: void 0 })}></uui-button>` : o}
        <uui-button look="secondary" label="Export CSV" @click=${p(this, u, Se)}></uui-button>
      </form>`;
};
Ke = function(e) {
  return e.length ? s`<ul class="groups">${e.map((t) => s`<li>${p(this, u, Oe).call(this, t)}</li>`)}</ul>` : s`<uui-box><p>${this._filter.ignored ? "Nothing is ignored." : "No open issues match these filters."}</p></uui-box>`;
};
Oe = function(e) {
  const t = e.summary, i = this.knowledge?.texts[t.checkId], r = i?.title ?? t.checkId, a = t.pages === 0;
  return s`
      <details class="group" ?open=${this._filter.checkId === t.checkId}>
        <summary>
          <span class="tag sev-${t.severity}"><uui-icon name=${Z[t.severity]}></uui-icon>${J[t.severity]}</span>
          <span class="group-title">${r}</span>
          <span class="muted">${a ? "whole site" : q(t.pages, "page", "pages")} · ${de[t.owner]}</span>
        </summary>
        <div class="group-body stack">
          ${i ? s`
            <div class="explain">
              <h4>Why it matters</h4><p>${i.why}</p>
              <h4>How to fix it</h4><p>${i.fix}</p>
              ${i.caveats ? s`<h4>Good to know</h4><p>${i.caveats}</p>` : o}
              ${D(i.learnMore) ? s`<p><a href=${D(i.learnMore)} target="_blank" rel="noopener">Learn more<span class="visually-hidden"> about ${r} (opens in a new tab)</span></a></p>` : o}
            </div>` : o}
          ${p(this, u, Pe).call(this, e, r)}
          ${this._filter.ignored ? o : s`<div class="row">
            <uui-button look="secondary" compact label="Ignore this check on the whole site"
              @click=${() => {
    this._ignoring = { checkId: t.checkId, contentKey: null, culture: null, label: `${r} (whole site)` }, this._note = "";
  }}></uui-button>
          </div>`}
          ${this._ignoring?.checkId === t.checkId ? p(this, u, Ne).call(this, this._ignoring) : o}
        </div>
      </details>`;
};
Pe = function(e, t) {
  return s`
      <table>
        <caption class="visually-hidden">Pages with: ${t}</caption>
        <thead><tr><th scope="col">Page</th><th scope="col">Language</th><th scope="col">Found</th><th scope="col"><span class="visually-hidden">Actions</span></th></tr></thead>
        <tbody>
          ${e.occurrences.map((i) => {
    const r = D(i.url), a = this._busy.has(`${i.contentKey}|${i.culture}`);
    return s`<tr>
              <td>
                ${i.contentKey ? s`<a href=${he(i.contentKey, i.culture)}>${i.name}</a>` : s`<span>${i.name}</span>`}
                ${r ? s`<br><a class="muted url" href=${r} target="_blank" rel="noopener">${i.url}<span class="visually-hidden"> (opens in a new tab)</span></a>` : o}
              </td>
              <td>${i.culture || "–"}</td>
              <td>${i.value ? s`<pre>${i.value}</pre>` : s`<span class="muted">–</span>`}</td>
              <td class="actions">
                ${i.ignored && i.ignoreId !== null ? s`<uui-button look="secondary" compact label="Stop ignoring" @click=${() => p(this, u, Ie).call(this, i.ignoreId)}></uui-button>` : i.contentKey ? s`
                    <uui-button look="secondary" compact label="Re-check" .state=${a ? "waiting" : void 0} ?disabled=${a}
                      @click=${() => p(this, u, ze).call(this, i)}></uui-button>
                    <uui-button look="secondary" compact label="Ignore"
                      @click=${() => {
      this._ignoring = { checkId: e.summary.checkId, contentKey: i.contentKey, culture: i.culture || null, label: `${t} on ${i.name}` }, this._note = "";
    }}></uui-button>` : o}
              </td>
            </tr>`;
  })}
        </tbody>
      </table>`;
};
Ne = function(e) {
  return s`
      <form class="ignore stack" @submit=${p(this, u, Ee)}>
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
b.styles = [B, W`
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: flex-end; }
    .field { display: grid; gap: 4px; }
    select, input, textarea {
      font: inherit; min-height: 36px; padding: 0 var(--uui-size-space-3);
      border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    textarea { padding: var(--uui-size-space-2); width: min(40rem, 100%); }
    .groups { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
    .group { background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); }
    .group > summary { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; padding: var(--uui-size-space-3) var(--uui-size-space-4); min-height: 44px; }
    .group-title { font-weight: 600; flex: 1; min-width: 12rem; }
    .group-body { padding: 0 var(--uui-size-space-4) var(--uui-size-space-4); }
    .explain { display: grid; gap: var(--uui-size-space-2); max-width: 48rem; }
    .explain h4 { margin-top: var(--uui-size-space-2); }
    .url { word-break: break-all; font-size: 0.9em; }
    .actions { white-space: nowrap; text-align: right; }
    .ignore { padding: var(--uui-size-space-4); background: var(--uui-color-surface-alt); border-radius: var(--uui-border-radius); }
  `];
w([
  T({ attribute: !1 })
], b.prototype, "siteKey", 2);
w([
  T({ attribute: !1 })
], b.prototype, "filter", 2);
w([
  T({ attribute: !1 })
], b.prototype, "knowledge", 2);
w([
  g()
], b.prototype, "_filter", 2);
w([
  g()
], b.prototype, "_groups", 2);
w([
  g()
], b.prototype, "_error", 2);
w([
  g()
], b.prototype, "_cultures", 2);
w([
  g()
], b.prototype, "_ignoring", 2);
w([
  g()
], b.prototype, "_note", 2);
w([
  g()
], b.prototype, "_busy", 2);
w([
  g()
], b.prototype, "_status", 2);
b = w([
  F("esatto-seo-issues")
], b);
var ht = Object.defineProperty, pt = Object.getOwnPropertyDescriptor, Te = (e) => {
  throw TypeError(e);
}, V = (e, t, i, r) => {
  for (var a = r > 1 ? void 0 : r ? pt(t, i) : t, n = e.length - 1, l; n >= 0; n--)
    (l = e[n]) && (a = (r ? l(t, i, a) : l(a)) || a);
  return r && a && ht(t, i, a), a;
}, ae = (e, t, i) => t.has(e) || Te("Cannot " + i), gt = (e, t, i) => (ae(e, t, "read from private field"), t.get(e)), le = (e, t, i) => t.has(e) ? Te("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), vt = (e, t, i, r) => (ae(e, t, "write to private field"), t.set(e, i), i), d = (e, t, i) => (ae(e, t, "access private method"), i), A, c, C, I, Ue, P, Le, X;
const De = (e) => JSON.parse(JSON.stringify(e)), ft = Array.from({ length: 24 }, (e, t) => t);
let N = class extends G {
  constructor() {
    super(), le(this, c), this.settings = null, this._draft = null, this._saving = !1, le(this, A), this.consumeContext(Re, (e) => vt(this, A, e));
  }
  willUpdate(e) {
    e.has("settings") && this.settings && (this._draft = De(this.settings.settings));
  }
  render() {
    const e = this._draft;
    return e ? s`
      <form class="stack" @submit=${d(this, c, Ue)}>
        ${this.settings?.locked.length ? s`<p class="muted">Fields marked “Set in configuration” come from appsettings (<code>Esatto:Seo</code>) and can't be changed here.</p>` : o}

        <uui-box headline="Nightly scan">
          <div class="stack">
            ${d(this, c, Le).call(this, "schedule.enabled", "Scan every night", e.schedule.enabled, (t) => d(this, c, I).call(this, (i) => i.schedule.enabled = t), "Keeps the score and trend up to date without anyone pressing Scan now.")}
            <div class="field">
              <label for="hour">Start time (server time)</label>
              <select id="hour" ?disabled=${!e.schedule.enabled || d(this, c, C).call(this, "schedule.hour")}
                @change=${(t) => d(this, c, I).call(this, (i) => i.schedule.hour = Number(t.target.value))}>
                ${ft.map((t) => s`<option value=${t} ?selected=${t === e.schedule.hour}>${String(t).padStart(2, "0")}:00</option>`)}
              </select>
              ${d(this, c, P).call(this, "schedule.hour")}
            </div>
          </div>
        </uui-box>

        <uui-box headline="What to scan">
          <div class="stack">
            <div class="field">
              <label for="exclude">Never scan these paths (one per line, e.g. /search)</label>
              <textarea id="exclude" rows="3" ?disabled=${d(this, c, C).call(this, "excludePaths")} .value=${e.excludePaths.join(`
`)}
                @change=${(t) => d(this, c, I).call(this, (i) => i.excludePaths = t.target.value.split(`
`).map((r) => r.trim()).filter(Boolean))}></textarea>
              ${d(this, c, P).call(this, "excludePaths")}
            </div>
            <div class="field">
              <label for="base">Base URL for pages without a domain <span class="muted">(normally empty)</span></label>
              <input id="base" type="url" placeholder="https://www.example.com" ?disabled=${d(this, c, C).call(this, "baseUrl")} .value=${e.baseUrl ?? ""}
                aria-describedby="base-hint" @change=${(t) => d(this, c, I).call(this, (i) => i.baseUrl = t.target.value.trim() || null)}>
              <p id="base-hint" class="muted hint">Only needed when no domain is bound in Culture and Hostnames. Otherwise pages are fetched at their own addresses.</p>
              ${d(this, c, P).call(this, "baseUrl")}
            </div>
          </div>
        </uui-box>

        <uui-box headline="Load on the site">
          <div class="row">
            ${d(this, c, X).call(this, "parallelism", "Pages fetched at the same time", e.parallelism, 1, 8, (t) => d(this, c, I).call(this, (i) => i.parallelism = t))}
            ${d(this, c, X).call(this, "delayMs", "Pause between requests (ms)", e.delayMs, 0, 5e3, (t) => d(this, c, I).call(this, (i) => i.delayMs = t))}
          </div>
        </uui-box>

        <uui-box headline="This environment">
          <p>${this.settings?.intentionallyHidden ? s`<uui-icon name="icon-lock"></uui-icon> Marked as hidden from search engines on purpose (<code>Esatto:Seo:IntentionallyHidden</code>).` : s`Not marked as hidden on purpose. On a Test or Dev environment that blocks search engines, set <code>Esatto:Seo:IntentionallyHidden</code> to <code>true</code> in its configuration.`}</p>
        </uui-box>

        <div class="row">
          <uui-button type="submit" look="primary" color="positive" label="Save settings" .state=${this._saving ? "waiting" : void 0}></uui-button>
        </div>
      </form>` : s`<uui-loader></uui-loader>`;
  }
};
A = /* @__PURE__ */ new WeakMap();
c = /* @__PURE__ */ new WeakSet();
C = function(e) {
  return (this.settings?.locked ?? []).some((t) => t.toLowerCase() === e.toLowerCase());
};
I = function(e) {
  const t = De(this._draft);
  e(t), this._draft = t;
};
Ue = async function(e) {
  if (e.preventDefault(), !this._draft) return;
  this._saving = !0;
  const { data: t, error: i } = await $(this, Be(this._draft));
  this._saving = !1, !(i || !t) && (gt(this, A)?.peek("positive", { data: { headline: "Settings saved", message: "They apply from the next scan." } }), this.dispatchEvent(new CustomEvent("saved", { detail: t })));
};
P = function(e) {
  return d(this, c, C).call(this, e) ? s`<span class="lock"><uui-icon name="icon-lock"></uui-icon> Set in configuration</span>` : o;
};
Le = function(e, t, i, r, a) {
  const n = `c-${e.split(".").join("-")}`;
  return s`
      <div class="check">
        <input id=${n} type="checkbox" .checked=${i} ?disabled=${d(this, c, C).call(this, e)} aria-describedby=${a ? `${n}-hint` : o}
          @change=${(l) => r(l.target.checked)}>
        <label for=${n}>${t}</label>
        ${d(this, c, P).call(this, e)}
        ${a ? s`<p id="${n}-hint" class="muted hint">${a}</p>` : o}
      </div>`;
};
X = function(e, t, i, r, a, n) {
  return s`
      <div class="field">
        <label for="n-${e}">${t}</label>
        <input id="n-${e}" type="number" min=${r} max=${a} ?disabled=${d(this, c, C).call(this, e)} .value=${String(i)}
          @change=${(l) => n(Math.min(a, Math.max(r, Number(l.target.value) || r)))}>
        ${d(this, c, P).call(this, e)}
      </div>`;
};
N.styles = [B, W`
    .field { display: grid; gap: 4px; }
    .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); align-items: center; min-height: 44px; }
    .check input { width: 20px; height: 20px; margin: 0; }
    .check .hint, .check .lock { grid-column: 2; margin: 0; }
    select, input:not([type='checkbox']), textarea {
      font: inherit; min-height: 36px; padding: 0 var(--uui-size-space-3); max-width: 32rem;
      border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    textarea { padding: var(--uui-size-space-2); }
    input:disabled, select:disabled, textarea:disabled { opacity: 0.7; }
    .lock { color: var(--uui-color-text-alt); font-size: 0.85rem; display: inline-flex; gap: 4px; align-items: center; }
  `];
V([
  T({ attribute: !1 })
], N.prototype, "settings", 2);
V([
  g()
], N.prototype, "_draft", 2);
V([
  g()
], N.prototype, "_saving", 2);
N = V([
  F("esatto-seo-settings")
], N);
var bt = Object.defineProperty, $t = Object.getOwnPropertyDescriptor, Me = (e) => {
  throw TypeError(e);
}, S = (e, t, i, r) => {
  for (var a = r > 1 ? void 0 : r ? $t(t, i) : t, n = e.length - 1, l; n >= 0; n--)
    (l = e[n]) && (a = (r ? l(t, i, a) : l(a)) || a);
  return r && a && bt(t, i, a), a;
}, mt = (e, t, i) => t.has(e) || Me("Cannot " + i), yt = (e, t, i) => t.has(e) ? Me("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), _t = (e, t, i) => (mt(e, t, "access private method"), i), Y, We;
const wt = [
  { id: "overview", label: "Overview" },
  { id: "issues", label: "Issues" },
  { id: "settings", label: "Settings" }
];
let k = class extends G {
  constructor() {
    super(), yt(this, Y), this._knowledge = null, this._sites = [], this._siteKey = "", this._settings = null, this._tab = "overview", this._filter = {}, this._error = null, this.addEventListener(ue, (e) => {
      const { tab: t, filter: i } = e.detail;
      this._filter = i ?? {}, this._tab = t;
    });
  }
  async connectedCallback() {
    super.connectedCallback();
    const [e, t, i] = await Promise.all([
      $(this, Ae(), { disableNotifications: !0 }),
      $(this, Fe(), { disableNotifications: !0 }),
      $(this, Ge(), { disableNotifications: !0 })
    ]);
    if (e.error || t.error || i.error || !e.data || !t.data || !i.data) {
      this._error = "Could not load the SEO Health dashboard.";
      return;
    }
    this._knowledge = e.data, this._sites = t.data, this._siteKey = t.data[0]?.key ?? "", this._settings = i.data;
  }
  render() {
    if (this._error) return s`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._settings || !this._knowledge) return s`<uui-loader></uui-loader>`;
    if (!this._sites.length)
      return s`<uui-box headline="SEO Health"><p>There is no published site to scan yet. Publish a root page with a template first.</p></uui-box>`;
    const e = wt.filter((t) => t.id !== "settings" || this._settings.canEdit);
    return s`
      <div class="header">
        <h2>SEO Health</h2>
        ${this._sites.length > 1 ? s`
          <uui-select label="Site" .options=${this._sites.map((t) => ({ name: t.name, value: t.key, selected: t.key === this._siteKey }))}
            @change=${(t) => this._siteKey = t.target.value}></uui-select>` : o}
      </div>
      <uui-tab-group>
        ${e.map((t) => s`<uui-tab id="tab-${t.id}" aria-controls="tabpanel" label=${t.label} ?active=${this._tab === t.id}
          @click=${() => {
      this._filter = {}, this._tab = t.id;
    }}>${t.label}</uui-tab>`)}
      </uui-tab-group>
      <div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${this._tab}">${_t(this, Y, We).call(this)}</div>`;
  }
};
Y = /* @__PURE__ */ new WeakSet();
We = function() {
  switch (this._tab) {
    case "issues":
      return s`<esatto-seo-issues .siteKey=${this._siteKey} .filter=${this._filter} .knowledge=${this._knowledge}></esatto-seo-issues>`;
    case "settings":
      return s`<esatto-seo-settings .settings=${this._settings} @saved=${(e) => this._settings = e.detail}></esatto-seo-settings>`;
    default:
      return s`<esatto-seo-overview .siteKey=${this._siteKey} .knowledge=${this._knowledge}></esatto-seo-overview>`;
  }
};
k.styles = [B, W`
    :host { padding: var(--uui-size-layout-1); }
    .header { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; justify-content: space-between; margin-bottom: var(--uui-size-space-4); }
    uui-tab-group { border-bottom: 1px solid var(--uui-color-divider); }
    .panel { padding-top: var(--uui-size-space-5); }
  `];
S([
  g()
], k.prototype, "_knowledge", 2);
S([
  g()
], k.prototype, "_sites", 2);
S([
  g()
], k.prototype, "_siteKey", 2);
S([
  g()
], k.prototype, "_settings", 2);
S([
  g()
], k.prototype, "_tab", 2);
S([
  g()
], k.prototype, "_filter", 2);
S([
  g()
], k.prototype, "_error", 2);
k = S([
  F("esatto-seo-dashboard")
], k);
const It = k;
export {
  k as EsattoSeoDashboardElement,
  It as default
};
//# sourceMappingURL=seo-dashboard.js.map
