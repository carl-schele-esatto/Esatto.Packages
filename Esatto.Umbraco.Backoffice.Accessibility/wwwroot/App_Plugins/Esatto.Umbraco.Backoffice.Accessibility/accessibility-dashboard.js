import { css as X, property as M, state as d, customElement as ee, html as r, nothing as l, repeat as Vt } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as te } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as x } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as g } from "@umbraco-cms/backoffice/http-client";
import { UMB_NOTIFICATION_CONTEXT as Bt } from "@umbraco-cms/backoffice/notification";
const v = "/umbraco/management/api/v1/esatto/accessibility", m = () => [{ scheme: "bearer", type: "http" }], J = (e) => `site=${encodeURIComponent(e)}`;
function Qe(e, t) {
  const s = new URLSearchParams({ site: e });
  return t.principle && s.set("principle", String(t.principle)), t.level && s.set("level", t.level), t.owner && s.set("owner", t.owner), t.impact && s.set("impact", t.impact), t.criterion && s.set("criterion", t.criterion), t.falsePositives && s.set("falsePositives", "true"), t.ruleId && s.set("ruleId", t.ruleId), t.kind && s.set("kind", t.kind), s.toString();
}
const qt = () => g.get({ url: `${v}/knowledge`, security: m() }), Gt = () => g.get({ url: `${v}/sites`, security: m() }), Ht = (e) => g.get({ url: `${v}/pages?${J(e)}`, security: m() }), jt = () => g.get({ url: `${v}/settings`, security: m() }), Xt = (e) => g.put({ url: `${v}/settings`, body: e, security: m(), headers: { "Content-Type": "application/json" } }), Jt = (e, t, s) => g.post({ url: `${v}/scans`, body: { siteKey: e, mobile: t, pagesTotal: s }, security: m(), headers: { "Content-Type": "application/json" } }), Yt = (e) => g.get({ url: `${v}/scans/${e}/done`, security: m() }), Qt = (e, t) => g.post({ url: `${v}/scans/${e}/pages`, body: t, security: m(), headers: { "Content-Type": "application/json" } }), Zt = (e, t) => g.post({ url: `${v}/scans/${e}/finish`, body: { complete: t }, security: m(), headers: { "Content-Type": "application/json" } }), es = (e, t) => g.post({ url: `${v}/recheck?${J(e)}`, body: t, security: m(), headers: { "Content-Type": "application/json" } }), ts = (e) => g.get({ url: `${v}/overview?${J(e)}`, security: m() }), Ze = (e, t) => g.get({ url: `${v}/issues?${Qe(e, t)}`, security: m() }), ss = (e, t) => g.get({ url: `${v}/issues/csv?${Qe(e, t)}`, security: m(), parseAs: "blob" }), is = (e) => g.get({ url: `${v}/checklist?${J(e)}`, security: m() }), as = (e, t, s, a) => g.put({ url: `${v}/checklist/${encodeURIComponent(t)}?${J(e)}`, body: { status: s, note: a }, security: m(), headers: { "Content-Type": "application/json" } }), rs = (e, t) => g.delete({ url: `${v}/checklist/${encodeURIComponent(t)}?${J(e)}`, security: m() }), ns = (e, t) => g.post({ url: `${v}/false-positives?${J(e)}`, body: t, security: m(), headers: { "Content-Type": "application/json" } }), os = (e) => g.delete({ url: `${v}/false-positives/${e}`, security: m() }), se = X`
  :host {
    display: block;
    --a11y-good: #0ca30c;
    --a11y-warning: #fab219;
    --a11y-critical: #d03b3b;
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
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; padding: var(--uui-size-space-2) var(--uui-size-space-3); border-bottom: 1px solid var(--uui-color-divider); }
  th { font-weight: 600; color: var(--uui-color-text-alt); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  pre { white-space: pre-wrap; word-break: break-all; background: var(--uui-color-surface-alt); padding: var(--uui-size-space-3); border-radius: var(--uui-border-radius); margin: 0; }
  a { color: var(--uui-color-interactive); }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .tag {
    display: inline-block; padding: 0 var(--uui-size-space-2); border-radius: var(--uui-border-radius);
    border: 1px solid var(--uui-color-border); font-size: 0.85em; line-height: 1.6;
  }
`, et = "esatto-a11y-navigate";
function me(e, t, s) {
  e.dispatchEvent(new CustomEvent(et, { detail: { tab: t, filter: s }, bubbles: !0, composed: !0 }));
}
const De = ["V20", "V21", "V22"], Fe = ["A", "AA", "AAA"], ls = [
  { tag: "wcag2a", version: "V20", level: "A" },
  { tag: "wcag2aa", version: "V20", level: "AA" },
  { tag: "wcag2aaa", version: "V20", level: "AAA" },
  { tag: "wcag21a", version: "V21", level: "A" },
  { tag: "wcag21aa", version: "V21", level: "AA" },
  { tag: "wcag22aa", version: "V22", level: "AA" }
];
function tt(e, t, s) {
  const a = ls.filter((i) => De.indexOf(i.version) <= De.indexOf(e) && Fe.indexOf(i.level) <= Fe.indexOf(t)).map((i) => i.tag);
  return s ? [...a, "best-practice"] : a;
}
const st = (e) => ({ V20: "2.0", V21: "2.1", V22: "2.2" })[e], be = (e) => `WCAG ${st(e.version)} ${e.level}`, it = (e) => e ?? "Best practice", at = (e) => e === "running" ? "not finished" : e;
function cs(e) {
  return e ? new Date(/[zZ]|[+-]\d\d:\d\d$/.test(e) ? e : `${e}Z`) : null;
}
function xe(e, t = !0) {
  const s = cs(e);
  return s ? new Intl.DateTimeFormat(void 0, t ? { dateStyle: "medium", timeStyle: "short" } : { dateStyle: "medium" }).format(s) : "";
}
function us(e) {
  return e === null ? null : e < 50 ? "critical" : e < 90 ? "warning" : "good";
}
const We = { good: "Good", warning: "Needs work", critical: "Major gaps" };
function ds(e) {
  return { toConfirm: e.scannerChecked, personOnly: e.notVerified - e.scannerChecked };
}
function hs(e, t) {
  const s = `/umbraco/section/content/workspace/document/edit/${e}`;
  return t ? `${s}/${t}` : s;
}
function ps(e) {
  return `/umbraco/section/media/workspace/media/edit/${e}`;
}
function re(e) {
  try {
    const t = new URL(e ?? "");
    return t.protocol === "http:" || t.protocol === "https:" ? t.toString() : void 0;
  } catch {
    return;
  }
}
const fs = /\.(pdf|docx?|xlsx|pptx)$/i;
function gs(e) {
  const t = /* @__PURE__ */ new Set();
  for (const s of e)
    try {
      fs.test(new URL(s).pathname) && t.add(s);
    } catch {
    }
  return [...t];
}
function vs(e, t) {
  if (e.status >= 400) return { kind: "failed", reason: `HTTP ${e.status}` };
  const s = new URL(e.url).origin;
  if (s !== t) return { kind: "failed", reason: `redirects to ${s}` };
  const a = (e.contentType ?? "").split(";")[0].trim().toLowerCase();
  if (a && a !== "text/html" && a !== "application/xhtml+xml") return { kind: "failed", reason: `not an HTML page (${a})` };
  if ((e.xFrameOptions ?? "").trim().toUpperCase() === "DENY") return { kind: "blocked", reason: "The site sends X-Frame-Options: DENY." };
  const n = /frame-ancestors\s+([^;]*)/i.exec(e.csp ?? "")?.[1].trim().toLowerCase();
  return n !== void 0 && (n === "'none'" || !/('self'|\*)/.test(n) && !n.includes(t.toLowerCase())) ? { kind: "blocked", reason: `The site sends Content-Security-Policy frame-ancestors ${n}.` } : null;
}
async function ms(e) {
  const t = await fetch(e, { credentials: "same-origin", redirect: "follow", headers: { Accept: "text/html" } });
  return t.body?.cancel().catch(() => {
  }), {
    url: t.url || e,
    status: t.status,
    contentType: t.headers.get("content-type"),
    xFrameOptions: t.headers.get("x-frame-options"),
    csp: t.headers.get("content-security-policy")
  };
}
const bs = (e) => e.map((t) => Array.isArray(t) ? t.join(" >>> ") : t).join(" "), Ve = (e, t) => ({
  ruleId: e.id,
  kind: t,
  impact: e.impact ?? null,
  help: e.help,
  helpUrl: e.helpUrl,
  tags: e.tags,
  nodes: e.nodes.map((s) => ({ selector: bs(s.target), html: s.html }))
});
function $s(e, t, s) {
  return {
    contentKey: t.contentKey,
    name: t.name,
    culture: t.culture,
    url: t.url,
    viewport: t.viewport,
    status: "ok",
    reason: null,
    passes: e.passes.map((a) => ({ ruleId: a.id, tags: a.tags })),
    rules: [
      ...e.violations.map((a) => Ve(a, "violation")),
      ...e.incomplete.map((a) => Ve(a, "incomplete"))
    ],
    documentLinks: s
  };
}
function oe(e, t, s) {
  return { ...e, status: t, reason: s, passes: [], rules: [], documentLinks: [] };
}
const Be = 900;
function qe(e) {
  const t = new URL(e);
  return t.searchParams.set("esatto-a11y-scan", "1"), t.toString();
}
async function rt(e, t, s) {
  const a = document.createElement("iframe");
  a.setAttribute("aria-hidden", "true"), a.tabIndex = -1, a.title = "Accessibility scan", a.width = String(e.viewport), a.height = String(Be), a.style.cssText = `position:absolute;left:-10000px;top:0;width:${e.viewport}px;height:${Be}px;border:0;`;
  let i;
  try {
    const n = new Promise((o) => {
      i = setTimeout(() => o({ kind: "failed", reason: "timeout", result: oe(e, "failed", "timeout") }), s.timeoutMs);
    });
    return await Promise.race([ys(e, a, t, s), n]);
  } finally {
    clearTimeout(i), a.remove();
  }
}
async function ys(e, t, s, a) {
  let i;
  try {
    i = vs(await ms(qe(e.url)), location.origin);
  } catch (b) {
    i = { kind: "failed", reason: b instanceof Error ? b.message : "request failed" };
  }
  if (i) return i.kind === "failed" ? { ...i, result: oe(e, "failed", i.reason ?? "failed") } : i;
  const n = new Promise((b) => t.addEventListener("load", () => b(), { once: !0 }));
  t.src = qe(e.url), s.appendChild(t), await n, await _s(a.settleMs);
  let o = null;
  try {
    o = t.contentDocument;
  } catch {
    o = null;
  }
  if (!o || o.location.href === "about:blank" || !o.documentElement)
    return { kind: "failed", reason: "the page left the frame", result: oe(e, "failed", "the page left the frame") };
  const h = t.contentWindow;
  if (!h) return { kind: "failed", reason: "the frame has no window" };
  if (!h.axe) {
    const b = o.createElement("script");
    b.src = a.axeUrl;
    const Y = new Promise((C, ge) => {
      b.onload = () => C(), b.onerror = () => ge(new Error("axe-core could not be loaded into the page (Content-Security-Policy script-src?)"));
    });
    o.head.appendChild(b);
    try {
      await Y;
    } catch (C) {
      return { kind: "blocked", reason: C instanceof Error ? C.message : "axe-core could not be loaded into the page." };
    }
  }
  const P = await h.axe.run(o, {
    runOnly: { type: "tag", values: a.tags },
    // Node lists only for what we store; `passes` still lists every passing rule id and its tags.
    resultTypes: ["violations", "incomplete"]
  }), T = gs([...o.querySelectorAll("a[href]")].map((b) => b.href));
  return { kind: "ok", result: $s(P, e, T) };
}
const _s = (e) => new Promise((t) => setTimeout(t, e)), $e = "other-origin:";
function ws(e, t) {
  const s = [], a = [];
  for (const i of e) {
    if (i.skipReason) {
      a.push({ ...i, skipReason: i.skipReason });
      continue;
    }
    const n = new URL(i.url);
    n.origin !== t ? a.push({ ...i, skipReason: `${$e}${n.origin}` }) : s.push(i);
  }
  return { scannable: s, skipped: a };
}
function ks(e, t) {
  const s = new URL(t), a = e.join(", "), i = e.filter((o) => new URL(o).host === s.host), n = `Pages on ${a} were skipped: a browser can only scan pages on the same address as the backoffice (${t}), meaning the same scheme, host and port.`;
  if (i.length) {
    const o = (h) => new URL(h).protocol.replace(":", "");
    return `${n} These pages use ${o(i[0])} instead of ${o(s)}. Set the site's domain to ${t} in Culture and Hostnames, or open the backoffice on ${i[0]}.`;
  }
  return `${n} Open the backoffice on ${e[0]}${e.length > 1 ? " (and so on)" : ""} to scan them.`;
}
function ye(e, t) {
  return e.flatMap((s) => t.map((a) => ({
    contentKey: s.contentKey,
    name: s.name,
    culture: s.culture,
    url: s.url,
    viewport: a
  })));
}
class nt extends Error {
  constructor(t, s) {
    super(`The page ${t} can't be loaded in a frame${s ? ` (${s})` : ""}.`), this.url = t, this.reason = s, this.name = "BlockedFrameError";
  }
}
class xs {
  constructor(t, s, a, i) {
    this.opts = s, this.run = a, this.settle = i, this.#e = [...t], this.#r = t.length;
  }
  #e;
  #t = /* @__PURE__ */ new Set();
  #s = 0;
  #r;
  #i = null;
  #a = !1;
  #n = [];
  get cancelled() {
    return this.#a;
  }
  onProgress(t) {
    this.#n.push(t);
  }
  /**
   * No new jobs start. Jobs already running finish; a successful one is settled, a failed one is dropped
   * (not retried, not recorded): after a cancel its frame usually can't load, and a resume will scan it.
   */
  cancel() {
    this.#a = !0;
  }
  async start() {
    const t = Array.from({ length: Math.max(1, this.opts.concurrency) }, () => this.#l());
    if (await Promise.all(t), this.#o(), this.#i) throw this.#i;
  }
  async #l() {
    for (; !this.#a && !this.#i; ) {
      const t = this.#e.shift();
      if (!t) return;
      this.#t.add(t), this.#o();
      try {
        const s = await this.#c(t);
        if (s.kind === "blocked") {
          this.#i ??= new nt(t.url, s.reason);
          return;
        }
        if (this.#a && s.kind === "failed") return;
        await this.settle(t, s), this.#s++;
      } catch (s) {
        this.#i ??= s instanceof Error ? s : new Error(String(s));
        return;
      } finally {
        this.#t.delete(t);
      }
    }
  }
  async #c(t) {
    let s = { kind: "failed", reason: "not run" };
    for (let a = 0; a <= this.opts.retries; a++) {
      try {
        s = await this.run(t);
      } catch (i) {
        s = { kind: "failed", reason: i instanceof Error ? i.message : String(i) };
      }
      if (s.kind !== "failed" || this.#a) return s;
    }
    return s;
  }
  #o() {
    const t = { done: this.#s, total: this.#r, current: [...this.#t].map((s) => s.url) };
    for (const s of this.#n) s(t);
  }
}
const ce = 1280, Cs = 320, Es = 3, Ss = 1, Ge = [1e3, 2e3, 4e3];
function As(e, t) {
  return t ? { mobile: t.mobile, resumeScanId: t.id } : { mobile: e };
}
function Ps(e) {
  return ye([e], e.viewports.length ? e.viewports : [ce]);
}
class Ts {
  constructor(t) {
    this.deps = t;
  }
  #e = null;
  #t = !1;
  cancel() {
    this.#t = !0, this.#e?.cancel();
  }
  async start(t, s, a) {
    const i = await this.deps.getPages(t), { scannable: n, skipped: o } = ws(i, this.deps.origin), h = [...new Set(o.filter((p) => p.skipReason.startsWith($e)).map((p) => p.skipReason.slice($e.length)))], P = s.mobile ? [ce, Cs] : [ce];
    let T = ye(n, P), b = o.map((p) => ({ job: ye([p], [ce])[0], reason: p.skipReason }));
    const Y = T.length + b.length, C = s.resumeScanId ?? await this.deps.startScan(t, s.mobile, Y);
    if (s.resumeScanId !== void 0) {
      const p = new Set((await this.deps.getDone(C)).map(ve));
      T = T.filter((U) => !p.has(ve(U))), b = b.filter((U) => !p.has(ve(U.job)));
    }
    const ge = Y - T.length - b.length;
    let Ne = 0, Me = 0;
    const Ue = (p) => a?.({ ...p, done: p.done + Ne + ge, total: Y });
    try {
      for (const p of b)
        await this.#s(C, oe(p.job, "skipped", p.reason)), Ne++;
      Ue({ done: 0, total: Y, current: [] }), this.#e = new xs(
        T,
        { concurrency: Es, retries: Ss },
        (p) => this.deps.runJob(p),
        (p, U) => (U.kind !== "ok" && Me++, this.#s(C, U.kind === "ok" && U.result ? U.result : oe(p, "failed", U.reason ?? "failed")))
      ), this.#e.onProgress(Ue), this.#t && this.#e.cancel(), await this.#e.start();
    } catch (p) {
      return p instanceof nt ? { scanId: C, blocked: p, summary: await this.deps.finishScan(C, !1), otherOrigins: h } : { scanId: C, paused: p instanceof Error ? p : new Error(String(p)), otherOrigins: h };
    }
    const Ke = T.length > 0 && Me * 2 > T.length, Wt = !this.#t && !this.#e.cancelled && !Ke;
    return { scanId: C, summary: await this.deps.finishScan(C, Wt), otherOrigins: h, mostlyFailed: Ke };
  }
  /** Saves one page result, retrying with back-off; throws after the last retry. */
  async #s(t, s) {
    const a = this.deps.sleep ?? ((i) => new Promise((n) => setTimeout(n, i)));
    for (let i = 0; ; i++)
      try {
        await this.deps.postPage(t, s);
        return;
      } catch (n) {
        if (i >= Ge.length) throw n;
        await a(Ge[i]);
      }
  }
}
const ve = (e) => `${e.contentKey}|${e.culture}|${e.viewport}`;
var Os = Object.defineProperty, zs = Object.getOwnPropertyDescriptor, ot = (e) => {
  throw TypeError(e);
}, W = (e, t, s, a) => {
  for (var i = a > 1 ? void 0 : a ? zs(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (i = (a ? o(t, s, i) : o(i)) || i);
  return a && i && Os(t, s, i), i;
}, Ce = (e, t, s) => t.has(e) || ot("Cannot " + s), Ee = (e, t, s) => (Ce(e, t, "read from private field"), t.get(e)), He = (e, t, s) => t.has(e) ? ot("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), je = (e, t, s, a) => (Ce(e, t, "write to private field"), t.set(e, s), s), O = (e, t, s) => (Ce(e, t, "access private method"), s), H, S, Se, _e, lt, ct, ut, dt, ht, pt, ft;
const Rs = "/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js", $ = { size: 168, stroke: 14 };
async function ae(e) {
  const { data: t, error: s } = await e;
  if (s) throw s instanceof Error ? s : new Error(typeof s == "string" ? s : "Request failed");
  return t;
}
let L = class extends te {
  constructor() {
    super(...arguments), He(this, S), this.siteKey = "", this.settings = null, this._overview = null, this._error = null, this._progress = null, this._run = null, this._scanError = null, He(this, H, null);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), Ee(this, H)?.cancel();
  }
  updated(e) {
    e.has("siteKey") && this.siteKey && O(this, S, Se).call(this);
  }
  render() {
    if (this._error) return r`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._overview) return r`<uui-loader></uui-loader>`;
    const e = this._overview;
    return r`
      <div class="stack">
        <uui-box>
          <div class="hero">
            ${O(this, S, ct).call(this, e)}
            <div class="stack">
              ${O(this, S, ut).call(this, e)}
              ${O(this, S, dt).call(this, e)}
              ${O(this, S, ht).call(this, e)}
            </div>
          </div>
          ${O(this, S, pt).call(this)}
          ${O(this, S, ft).call(this)}
        </uui-box>

        <section aria-labelledby="principles-heading">
          <h3 id="principles-heading" class="section-title">By principle</h3>
          <ul class="tiles">
            ${e.principles.map((t) => r`
              <li>
                <button class="tile" @click=${() => me(this, "issues", { principle: t.principle })}>
                  <span class="tile-label">${t.principle}. ${t.name}</span>
                  <span class="tile-value">${t.fail} failing</span>
                  <span class="muted">${t.toCheck} to check</span>
                </button>
              </li>`)}
          </ul>
          <p>
            Automated: ${e.score.automatedPass} criteria pass, ${e.score.automatedFail} fail ·
            Manual: ${e.manualChecked} of ${e.manualTotal} checked
            <uui-button look="secondary" compact label="Open manual checklist" @click=${() => me(this, "checklist")}></uui-button>
          </p>
        </section>

        ${e.history.length ? r`
          <uui-box headline="Recent scans">
            <table>
              <caption class="visually-hidden">The last ${e.history.length} scans</caption>
              <thead><tr><th scope="col">Started</th><th scope="col">Target</th><th scope="col" class="num">Score</th><th scope="col" class="num">Pages</th><th scope="col">Status</th><th scope="col">By</th></tr></thead>
              <tbody>
                ${e.history.map((t) => r`<tr>
                  <td>${xe(t.startedUtc)}</td>
                  <td>${t.target}</td>
                  <td class="num">${t.score === null ? "–" : `${t.score} %`}</td>
                  <td class="num">${t.pagesDone}${t.pagesSkipped ? r` <span class="muted">(+${t.pagesSkipped} skipped)</span>` : l}</td>
                  <td>${at(t.status)}</td>
                  <td>${t.userName}</td>
                </tr>`)}
              </tbody>
            </table>
          </uui-box>` : l}
        <div id="frames" aria-hidden="true"></div>
      </div>`;
  }
};
H = /* @__PURE__ */ new WeakMap();
S = /* @__PURE__ */ new WeakSet();
Se = async function() {
  const { data: e, error: t } = await x(this, ts(this.siteKey), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load the accessibility overview.";
    return;
  }
  this._error = null, this._overview = e;
};
_e = async function(e) {
  if (!this.settings) return;
  const t = this.settings.settings, s = tt(t.version, t.level, t.scan.bestPractice), a = this.renderRoot.querySelector("#frames");
  je(this, H, new Ts({
    origin: location.origin,
    getPages: (i) => ae(Ht(i)),
    startScan: async (i, n, o) => (await ae(Jt(i, n, o))).scanId,
    getDone: (i) => ae(Yt(i)),
    postPage: async (i, n) => {
      await ae(Qt(i, n));
    },
    finishScan: (i, n) => ae(Zt(i, n)),
    runJob: (i) => rt(i, a, { axeUrl: Rs, tags: s, settleMs: 1e3, timeoutMs: 3e4 })
  })), this._run = null, this._scanError = null, this._progress = { done: 0, total: 0, current: [] };
  try {
    this._run = await Ee(this, H).start(this.siteKey, As(t.scan.mobileWidth, e), (i) => this._progress = i);
  } catch (i) {
    this._scanError = i instanceof Error ? i.message : "The scan could not start.";
  } finally {
    this._progress = null, je(this, H, null), await O(this, S, Se).call(this);
  }
};
lt = function() {
  Ee(this, H)?.cancel();
};
ct = function(e) {
  const t = e.score.percent, s = us(t), a = ($.size - $.stroke) / 2, i = 2 * Math.PI * a, n = (t ?? 0) / 100 * i, o = ds(e.score), h = t === null ? "No score yet" : `Score ${t} percent, ${be(e.target)}, ${We[s]}`;
  return r`
      <figure class="ring ${s ?? "none"}" role="img" aria-label=${h}>
        <svg width=${$.size} height=${$.size} viewBox="0 0 ${$.size} ${$.size}" aria-hidden="true">
          <circle class="track" cx=${$.size / 2} cy=${$.size / 2} r=${a} stroke-width=${$.stroke} fill="none"></circle>
          <circle class="fill" cx=${$.size / 2} cy=${$.size / 2} r=${a} stroke-width=${$.stroke} fill="none"
            stroke-dasharray="${n} ${i}" stroke-linecap="round"
            transform="rotate(-90 ${$.size / 2} ${$.size / 2})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true">
          <span class="ring-value">${t === null ? "–" : `${t}%`}</span>
          <span class="ring-target">${be(e.target)}</span>
        </div>
        <figcaption aria-hidden="true" class="ring-band">
          ${s ? r`<uui-icon name=${s === "good" ? "icon-check" : s === "warning" ? "icon-alert" : "icon-wrong"}></uui-icon> ${We[s]}` : "Not scanned yet"}
        </figcaption>
      </figure>
      <div class="ring-facts">
        <span><strong>${e.score.pass}</strong> pass</span>
        <span><strong>${e.score.fail}</strong> fail</span>
        <span><strong>${e.score.notVerified}</strong> not verified:</span>
        <span class="sub"><strong>${o.toConfirm}</strong> checked by the scanner, confirm by hand</span>
        <span class="sub"><strong>${o.personOnly}</strong> only a person can check</span>
        ${e.score.notApplicable ? r`<span><strong>${e.score.notApplicable}</strong> not applicable</span>` : l}
        <details>
          <summary>Why isn't this 100 %?</summary>
          <p>A criterion only counts as passed when someone has confirmed it in the manual checklist, or when the scanner can check it completely (very few can). Automated tests find only part of the problems, so a clean scan alone never scores 100 %.</p>
          <p>“Checked by the scanner” means its rules for the criterion found nothing wrong, but they test only the mechanical part (that an image has alt text, not that the text describes it). Start with those: they are usually quick to confirm. The rest, such as captions, keyboard use and reading order, only a person can check.</p>
        </details>
      </div>`;
};
ut = function(e) {
  return r`<p>Target: <strong>${be(e.target)}</strong></p>`;
};
dt = function(e) {
  const t = e.lastScan;
  return t ? r`<p>
      Last scan: ${xe(t.startedUtc)} · ${t.pagesDone} ${t.pagesDone === 1 ? "page" : "pages"}${t.pagesSkipped ? ` (${t.pagesSkipped} skipped)` : ""}${t.pagesFailed ? ` · ${t.pagesFailed} failed` : ""} · by ${t.userName}
      ${t.status !== "complete" ? r` · <span class="tag">${at(t.status)}</span>` : l}
    </p>` : r`<p class="muted">This site hasn't been scanned yet.</p>`;
};
ht = function(e) {
  const t = this._progress !== null, s = e.lastScan && e.lastScan.status !== "complete" ? e.lastScan : null;
  return r`<div class="row">
      <uui-button look="primary" color="positive" label="Scan now" ?disabled=${t} @click=${() => O(this, S, _e).call(this)}></uui-button>
      ${s && !t ? r`<uui-button look="secondary" label="Resume last scan" @click=${() => O(this, S, _e).call(this, s)}></uui-button>` : l}
      ${t ? r`<uui-button look="secondary" label="Cancel scan" @click=${O(this, S, lt)}></uui-button>` : l}
    </div>
    <p class="muted small">Pages load in hidden frames in your browser, so keep this dashboard open while scanning. Browsers slow down background tabs: a scan runs fastest while this tab is in front. Leaving the dashboard stops the scan; “Resume last scan” continues it.</p>`;
};
pt = function() {
  const e = this._progress;
  if (!e) return l;
  const t = e.current[0] ? new URL(e.current[0]).pathname : "";
  return r`
      <div class="progress">
        <uui-loader-bar progress=${e.total ? Math.round(e.done / e.total * 100) : 0}></uui-loader-bar>
        <p role="status">${e.total ? `Scanning page ${Math.min(e.done + 1, e.total)} of ${e.total}${t ? `: ${t}` : ""}` : "Getting the list of pages…"}</p>
      </div>`;
};
ft = function() {
  const e = this._run;
  return r`
      ${this._scanError ? r`<p class="error" role="alert">${this._scanError}</p>` : l}
      ${e?.blocked ? r`
        <div class="notice" role="alert">
          <h4>Pages can't be loaded for scanning</h4>
          <p>${e.blocked.reason ?? e.blocked.message}</p>
          <p>The scan loads each page in a frame on this site and runs axe-core in it, so the site must allow same-origin framing
          (<code>X-Frame-Options: SAMEORIGIN</code> or <code>Content-Security-Policy: frame-ancestors 'self'</code>, not DENY / 'none')
          and scripts from its own origin (<code>script-src 'self'</code>).</p>
        </div>` : l}
      ${e?.paused ? r`
        <div class="notice" role="alert">
          <h4>The scan was paused</h4>
          <p>Results couldn't be saved (${e.paused.message}). Everything up to now is kept; use “Resume last scan” to continue.</p>
        </div>` : l}
      ${e?.mostlyFailed ? r`
        <div class="notice" role="alert">
          <h4>Most pages couldn't be scanned</h4>
          <p>The scan was saved as incomplete so it doesn't replace your last full report. Check that the site is up, then use “Resume last scan” or scan again. Failed pages and reasons are listed in each page result.</p>
        </div>` : l}
      ${e?.otherOrigins.length ? r`<p class="muted">${ks(e.otherOrigins, location.origin)}</p>` : l}`;
};
L.styles = [se, X`
    .hero { display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; }
    .ring { position: relative; margin: 0; width: ${$.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-2); }
    .ring svg { display: block; }
    .ring .track { stroke: var(--uui-color-surface-alt); }
    .ring.good .fill { stroke: var(--a11y-good); }
    .ring.good .track { stroke: color-mix(in srgb, var(--a11y-good) 22%, transparent); }
    .ring.warning .fill { stroke: var(--a11y-warning); }
    .ring.warning .track { stroke: color-mix(in srgb, var(--a11y-warning) 22%, transparent); }
    .ring.critical .fill { stroke: var(--a11y-critical); }
    .ring.critical .track { stroke: color-mix(in srgb, var(--a11y-critical) 22%, transparent); }
    .ring-text { position: absolute; top: 0; height: ${$.size}px; display: grid; place-content: center; text-align: center; }
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
W([
  M({ attribute: !1 })
], L.prototype, "siteKey", 2);
W([
  M({ attribute: !1 })
], L.prototype, "settings", 2);
W([
  d()
], L.prototype, "_overview", 2);
W([
  d()
], L.prototype, "_error", 2);
W([
  d()
], L.prototype, "_progress", 2);
W([
  d()
], L.prototype, "_run", 2);
W([
  d()
], L.prototype, "_scanError", 2);
L = W([
  ee("esatto-a11y-overview")
], L);
var Is = Object.defineProperty, Ls = Object.getOwnPropertyDescriptor, gt = (e) => {
  throw TypeError(e);
}, K = (e, t, s, a) => {
  for (var i = a > 1 ? void 0 : a ? Ls(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (i = (a ? o(t, s, i) : o(i)) || i);
  return a && i && Is(t, s, i), i;
}, Ns = (e, t, s) => t.has(e) || gt("Cannot " + s), Ms = (e, t, s) => t.has(e) ? gt("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), A = (e, t, s) => (Ns(e, t, "access private method"), s), w, le, we, Ae, vt, mt, bt, $t, yt;
const Us = "/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js", Ks = 1024;
let z = class extends te {
  constructor() {
    super(...arguments), Ms(this, w), this.siteKey = "", this.settings = null, this._pending = null, this._note = "", this._noteError = !1, this._rechecking = null, this._message = null;
  }
  firstUpdated() {
    this.renderRoot.querySelector("h3")?.focus();
  }
  render() {
    const e = this.group;
    return r`
      <div class="stack">
        <uui-button look="secondary" label="Back to all issues" @click=${() => this.dispatchEvent(new CustomEvent("back"))}>← Back to all issues</uui-button>
        <h3 tabindex="-1">${e.title}</h3>
        <p class="facts">
          ${e.criteria.length ? r`<strong>WCAG ${e.criteria.join(", ")}</strong> · Level ${it(e.level)} ·` : r`<strong>Best practice</strong> ·`}
          ${e.kind === "incomplete" ? r`<span class="tag">Needs review</span> ·` : l}
          ${e.pageCount} ${e.pageCount === 1 ? "page" : "pages"} (${e.elementCount} ${e.elementCount === 1 ? "element" : "elements"}) ·
          Who fixes: <strong>${e.owner}</strong>${e.impact ? r` · Impact: ${e.impact}` : l}
        </p>
        ${this._message ? r`<p role="status" class="message">${this._message}</p>` : l}

        <uui-box>
          <div class="guide">
            <h4>Why it matters</h4><p>${e.why}</p>
            <h4>How to fix</h4><p>${e.howToFix}</p>
            ${e.caveats ? r`<h4>Caveats</h4><p>${e.caveats}</p>` : l}
          </div>
        </uui-box>
        ${e.kind === "incomplete" ? r`<p class="muted">The scanner couldn't decide these automatically. Check each one by hand; mark it as a false positive if it's fine.</p>` : l}
        ${re(e.learnMoreUrl) ? r`<p><a href=${re(e.learnMoreUrl)} target="_blank" rel="noopener">${e.criteria.length ? `Read W3C's “Understanding ${e.criteria[0]}”` : "Read more about this rule"}<span class="visually-hidden"> (opens in a new tab)</span></a></p>` : l}

        <h4>Where it occurs</h4>
        ${e.pages.map((t) => A(this, w, $t).call(this, t))}
        <div id="frames" aria-hidden="true"></div>
      </div>
      ${A(this, w, yt).call(this)}`;
  }
};
w = /* @__PURE__ */ new WeakSet();
le = function() {
  this.dispatchEvent(new CustomEvent("changed", { bubbles: !0, composed: !0 }));
};
we = function(e, t) {
  this._pending = { page: e, node: t }, this._note = "", this._noteError = !1, this.updateComplete.then(() => this.renderRoot.querySelector("dialog")?.showModal());
};
Ae = function() {
  this.renderRoot.querySelector("dialog")?.close(), this._pending = null;
};
vt = async function() {
  if (!this._pending) return;
  if (!this._note.trim()) {
    this._noteError = !0, this.renderRoot.querySelector("#fp-note")?.focus();
    return;
  }
  const { page: e, node: t } = this._pending, { error: s } = await x(this, ns(this.siteKey, {
    ruleId: this.group.ruleId,
    contentKey: e.contentKey,
    selector: t?.selector ?? null,
    note: this._note.trim()
  }));
  s || (A(this, w, Ae).call(this), this._message = "Marked as a false positive.", A(this, w, le).call(this));
};
mt = async function(e) {
  const { error: t } = await x(this, os(e));
  t || (this._message = "False positive removed.", A(this, w, le).call(this));
};
bt = async function(e) {
  if (!this.settings) return;
  const t = this.settings.settings;
  this._rechecking = e.contentKey, this._message = null;
  const s = this.renderRoot.querySelector("#frames");
  for (const a of Ps(e)) {
    const i = await rt(a, s, { axeUrl: Us, tags: tt(t.version, t.level, t.scan.bestPractice), settleMs: 1e3, timeoutMs: 3e4 });
    if (i.kind !== "ok" || !i.result) {
      this._message = `Re-check failed at ${a.viewport} px: ${i.reason ?? i.kind}.`, this._rechecking = null, A(this, w, le).call(this);
      return;
    }
    const { error: n } = await x(this, es(this.siteKey, i.result));
    if (n) {
      this._rechecking = null;
      return;
    }
  }
  this._rechecking = null, this._message = `${e.name} was re-checked.`, A(this, w, le).call(this);
};
$t = function(e) {
  const t = this._rechecking === e.contentKey;
  return r`
      <uui-box>
        <div slot="headline" class="page-head">
          <span>${e.name}</span> <span class="muted">${re(e.url) ? new URL(e.url).pathname : ""}</span>
        </div>
        <div class="row actions">
          <a href=${hs(e.contentKey, e.culture)}>Edit page in Umbraco</a>
          ${re(e.url) ? r`<a href=${re(e.url)} target="_blank" rel="noopener">View on page<span class="visually-hidden"> (opens in a new tab)</span></a>` : l}
          <uui-button compact look="secondary" label="Re-check this page" ?disabled=${t} @click=${() => A(this, w, bt).call(this, e)}>${t ? "Re-checking…" : "Re-check this page"}</uui-button>
          ${e.nodes.some((s) => s.falsePositiveId) ? l : r`<uui-button compact look="secondary" label="Mark all on this page as false positive" @click=${() => A(this, w, we).call(this, e, null)}></uui-button>`}
        </div>
        <ul class="nodes">
          ${e.nodes.map((s) => r`<li>
            <pre>${s.html}</pre>
            <p class="muted">Selector: <code>${s.selector}</code></p>
            <div class="row">
              ${s.mediaKey ? r`<a href=${ps(s.mediaKey)}>Edit image in Umbraco</a>` : l}
              ${s.falsePositiveId ? r`<uui-button compact look="secondary" label="Undo false positive" @click=${() => A(this, w, mt).call(this, s.falsePositiveId)}></uui-button>` : r`<uui-button compact look="secondary" label="Mark as false positive" @click=${() => A(this, w, we).call(this, e, s)}></uui-button>`}
            </div>
          </li>`)}
        </ul>
      </uui-box>`;
};
yt = function() {
  const e = this._pending;
  return r`
      <dialog aria-labelledby="fp-title" @close=${() => this._pending = null}>
        ${e ? r`
          <form method="dialog" @submit=${(t) => {
    t.preventDefault(), A(this, w, vt).call(this);
  }}>
            <h3 id="fp-title">Mark as false positive</h3>
            <p>${e.node ? r`This element on <strong>${e.page.name}</strong>` : r`Every “${this.group.title}” finding on <strong>${e.page.name}</strong>`} will be hidden from the report and the score, now and in later scans.</p>
            <label for="fp-note">Why is this not a problem? (required)</label>
            <textarea id="fp-note" rows="3" maxlength=${Ks} aria-required="true" aria-invalid=${this._noteError ? "true" : "false"} aria-describedby=${this._noteError ? "fp-error" : l}
              .value=${this._note} @input=${(t) => {
    this._note = t.target.value, this._noteError = !1;
  }}></textarea>
            ${this._noteError ? r`<p id="fp-error" class="error">Write a short reason, so others know why.</p>` : l}
            <div class="row">
              <uui-button type="submit" look="primary" label="Mark as false positive"></uui-button>
              <uui-button look="secondary" label="Cancel" @click=${A(this, w, Ae)}></uui-button>
            </div>
          </form>` : l}
      </dialog>`;
};
z.styles = [se, X`
    h3:focus { outline: none; }
    h3:focus-visible { outline: 2px solid var(--uui-color-focus); }
    .facts { margin: 0; }
    .guide { max-width: 60rem; }
    .guide h4 { margin-top: var(--uui-size-space-4); }
    .guide h4:first-child { margin-top: 0; }
    .guide p { margin: var(--uui-size-space-2) 0 0; }
    .message { padding: var(--uui-size-space-3); background: var(--uui-color-surface-alt); border-radius: var(--uui-border-radius); }
    .page-head { display: flex; gap: var(--uui-size-space-3); flex-wrap: wrap; }
    .actions { margin-bottom: var(--uui-size-space-4); }
    .nodes { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); }
    .nodes li { border-top: 1px solid var(--uui-color-divider); padding-top: var(--uui-size-space-3); }
    .nodes p { margin: var(--uui-size-space-2) 0; }
    dialog { border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); padding: var(--uui-size-layout-1); max-width: 36rem; width: calc(100% - 2rem); background: var(--uui-color-surface); color: var(--uui-color-text); }
    dialog::backdrop { background: rgb(0 0 0 / 0.4); }
    dialog form { display: grid; gap: var(--uui-size-space-3); }
    textarea { font: inherit; padding: var(--uui-size-space-2); border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); }
    textarea[aria-invalid='true'] { border-color: var(--uui-color-danger); }
    #frames { position: relative; height: 0; overflow: hidden; }
  `];
K([
  M({ attribute: !1 })
], z.prototype, "group", 2);
K([
  M({ attribute: !1 })
], z.prototype, "siteKey", 2);
K([
  M({ attribute: !1 })
], z.prototype, "settings", 2);
K([
  d()
], z.prototype, "_pending", 2);
K([
  d()
], z.prototype, "_note", 2);
K([
  d()
], z.prototype, "_noteError", 2);
K([
  d()
], z.prototype, "_rechecking", 2);
K([
  d()
], z.prototype, "_message", 2);
z = K([
  ee("esatto-a11y-issue-detail")
], z);
var Ds = Object.defineProperty, Fs = Object.getOwnPropertyDescriptor, _t = (e) => {
  throw TypeError(e);
}, D = (e, t, s, a) => {
  for (var i = a > 1 ? void 0 : a ? Fs(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (i = (a ? o(t, s, i) : o(i)) || i);
  return a && i && Ds(t, s, i), i;
}, Pe = (e, t, s) => t.has(e) || _t("Cannot " + s), Xe = (e, t, s) => (Pe(e, t, "read from private field"), s ? s.call(e) : t.get(e)), Je = (e, t, s) => t.has(e) ? _t("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), wt = (e, t, s, a) => (Pe(e, t, "write to private field"), t.set(e, s), s), y = (e, t, s) => (Pe(e, t, "access private method"), s), Q, f, de, Te, q, kt, xt, Ct, Et, St;
const Ws = ["Perceivable", "Operable", "Understandable", "Robust"], Vs = ["critical", "serious", "moderate", "minor"], Oe = (e) => `${e.ruleId}|${e.kind}`;
let R = class extends te {
  constructor() {
    super(...arguments), Je(this, f), this.siteKey = "", this.filter = {}, this.settings = null, this._filter = {}, this._groups = null, this._selected = null, this._detail = null, this._error = null, Je(this, Q, null);
  }
  willUpdate(e) {
    e.has("filter") && (this._filter = { ...this.filter });
  }
  updated(e) {
    (e.has("siteKey") || e.has("filter")) && this.siteKey && y(this, f, de).call(this), e.has("_selected") && this._selected === null && Xe(this, Q) && (this.renderRoot.querySelector(`[data-group="${CSS.escape(Xe(this, Q))}"]`)?.focus(), wt(this, Q, null));
  }
  render() {
    if (this._error) return r`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (this._selected && !this._detail) return r`<uui-loader></uui-loader>`;
    const e = this._detail;
    return this._selected && e ? r`<esatto-a11y-issue-detail .group=${e} .siteKey=${this.siteKey} .settings=${this.settings}
        @back=${y(this, f, Ct)} @changed=${() => y(this, f, de).call(this)}></esatto-a11y-issue-detail>` : r`
      <div class="stack">
        ${y(this, f, Et).call(this)}
        ${this._groups === null ? r`<uui-loader></uui-loader>` : y(this, f, St).call(this, this._groups)}
      </div>`;
  }
};
Q = /* @__PURE__ */ new WeakMap();
f = /* @__PURE__ */ new WeakSet();
de = async function() {
  const { data: e, error: t } = await x(this, Ze(this.siteKey, this._filter), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load issues.";
    return;
  }
  this._error = null, this._groups = e, this._selected && !e.some((s) => Oe(s) === this._selected) && (this._selected = null), this._selected && await y(this, f, Te).call(this);
};
Te = async function() {
  const [e, t] = this._selected.split("|"), { data: s } = await x(this, Ze(this.siteKey, { ...this._filter, ruleId: e, kind: t }), { disableNotifications: !0 });
  this._detail = s?.[0] ?? null, this._detail || (this._selected = null);
};
q = function(e) {
  this._filter = { ...this._filter, ...e }, this._selected = null, y(this, f, de).call(this);
};
kt = async function() {
  const { data: e, error: t } = await x(this, ss(this.siteKey, this._filter));
  if (t || !e) return;
  const s = URL.createObjectURL(e), a = document.createElement("a");
  a.href = s, a.download = `accessibility-issues-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.csv`, a.click(), setTimeout(() => URL.revokeObjectURL(s), 1e3);
};
xt = async function(e) {
  this._selected = Oe(e), this._detail = null, await y(this, f, Te).call(this);
};
Ct = function() {
  wt(this, Q, this._selected), this._selected = null, this._detail = null;
};
Et = function() {
  const e = this._filter, t = (s, a, i, n, o) => r`
      <div class="field">
        <label for=${s}>${a}</label>
        <select id=${s} @change=${(h) => o(h.target.value)}>
          <option value="" ?selected=${!i}>All</option>
          ${n.map(([h, P]) => r`<option value=${h} ?selected=${i === h}>${P}</option>`)}
        </select>
      </div>`;
  return r`
      <form class="filters" @submit=${(s) => s.preventDefault()} aria-label="Filter issues">
        ${t(
    "f-principle",
    "Principle",
    e.principle ? String(e.principle) : void 0,
    Ws.map((s, a) => [String(a + 1), `${a + 1}. ${s}`]),
    (s) => y(this, f, q).call(this, { principle: s ? Number(s) : void 0 })
  )}
        ${t("f-level", "Level", e.level, [["A", "A"], ["AA", "AA"], ["AAA", "AAA"]], (s) => y(this, f, q).call(this, { level: s || void 0 }))}
        ${t("f-owner", "Who fixes it", e.owner, [["Editor", "Editors"], ["Developer", "Developers"], ["Both", "Both"]], (s) => y(this, f, q).call(this, { owner: s || void 0 }))}
        ${t("f-impact", "Impact", e.impact, Vs.map((s) => [s, s]), (s) => y(this, f, q).call(this, { impact: s || void 0 }))}
        <div class="field checkbox">
          <input id="f-fp" type="checkbox" ?checked=${!!e.falsePositives} @change=${(s) => y(this, f, q).call(this, { falsePositives: s.target.checked })}>
          <label for="f-fp">Show false positives only</label>
        </div>
        ${e.criterion ? r`<div class="field"><span class="tag">Criterion ${e.criterion}</span>
          <uui-button compact look="secondary" label="Clear criterion filter" @click=${() => y(this, f, q).call(this, { criterion: void 0 })}></uui-button></div>` : l}
        <uui-button look="secondary" label="Export CSV" @click=${y(this, f, kt)}></uui-button>
      </form>`;
};
St = function(e) {
  return e.length ? r`
      <p class="muted" role="status">${e.length} ${e.length === 1 ? "issue" : "issues"}</p>
      <table>
        <caption class="visually-hidden">Issues, most severe first</caption>
        <thead><tr>
          <th scope="col">Issue</th><th scope="col">Criteria</th><th scope="col">Level</th>
          <th scope="col" class="num">Pages</th><th scope="col" class="num">Elements</th><th scope="col">Who fixes</th><th scope="col">Impact</th>
        </tr></thead>
        <tbody>
          ${e.map((t) => r`<tr>
            <td>
              <button class="link" data-group=${Oe(t)} @click=${() => y(this, f, xt).call(this, t)}>${t.title}</button>
              ${t.kind === "incomplete" ? r` <span class="tag">Needs review</span>` : l}
              ${t.bestPractice ? r` <span class="tag">Best practice</span>` : l}
            </td>
            <td>${t.criteria.join(", ") || "–"}</td>
            <td>${it(t.level)}</td>
            <td class="num">${t.pageCount}</td>
            <td class="num">${t.elementCount}</td>
            <td>${t.owner}</td>
            <td>${t.impact ?? "–"}</td>
          </tr>`)}
        </tbody>
      </table>` : r`<uui-box><p>${this._filter.falsePositives ? "No false positives marked." : "No issues match. Either the site passes the automated checks for these filters, or it hasn't been scanned yet."}</p></uui-box>`;
};
R.styles = [se, X`
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: end; }
    .field { display: grid; gap: 4px; }
    .field.checkbox { display: flex; align-items: center; gap: var(--uui-size-space-2); min-height: 36px; } /* = the selects, so they share a centre line */
    .field.checkbox input { width: 20px; height: 20px; }
    .filters > uui-button { height: 36px; }
    select { min-height: 36px; padding: 0 var(--uui-size-space-3); border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); font: inherit; }
    button.link { background: none; border: 0; padding: 4px 0; font: inherit; color: var(--uui-color-interactive); text-decoration: underline; cursor: pointer; text-align: left; min-height: 24px; }
    button.link:hover { color: var(--uui-color-interactive-emphasis); }
  `];
D([
  M({ attribute: !1 })
], R.prototype, "siteKey", 2);
D([
  M({ attribute: !1 })
], R.prototype, "filter", 2);
D([
  M({ attribute: !1 })
], R.prototype, "settings", 2);
D([
  d()
], R.prototype, "_filter", 2);
D([
  d()
], R.prototype, "_groups", 2);
D([
  d()
], R.prototype, "_selected", 2);
D([
  d()
], R.prototype, "_detail", 2);
D([
  d()
], R.prototype, "_error", 2);
R = D([
  ee("esatto-a11y-issues")
], R);
var Bs = Object.defineProperty, qs = Object.getOwnPropertyDescriptor, At = (e) => {
  throw TypeError(e);
}, V = (e, t, s, a) => {
  for (var i = a > 1 ? void 0 : a ? qs(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (i = (a ? o(t, s, i) : o(i)) || i);
  return a && i && Bs(t, s, i), i;
}, Gs = (e, t, s) => t.has(e) || At("Cannot " + s), Hs = (e, t, s) => t.has(e) ? At("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), k = (e, t, s) => (Gs(e, t, "access private method"), s), _, ze, ke, Pt, Tt, Re, he, Ie, Ot;
const zt = [
  { id: "all", label: "All", test: () => !0 },
  { id: "unchecked", label: "Not checked", test: (e) => !e.status },
  { id: "scanner", label: "Not checked, scanner found nothing", test: (e) => !e.status && e.scannerChecked },
  { id: "failed", label: "Failed", test: (e) => e.status === "fail" },
  { id: "recheck", label: "Needs re-check", test: (e) => e.needsRecheck }
], js = 2048, ne = (e) => `c-${e.criterion.id.split(".").join("-")}`, Rt = [
  { id: "pass", label: "Pass" },
  { id: "fail", label: "Fail" },
  { id: "na", label: "Not applicable" }
];
let N = class extends te {
  constructor() {
    super(...arguments), Hs(this, _), this.siteKey = "", this._items = null, this._view = "all", this._error = null, this._saved = null, this._confirmed = null, this._drafts = {};
  }
  updated(e) {
    e.has("siteKey") && this.siteKey && k(this, _, ze).call(this);
  }
  render() {
    if (this._error) return r`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._items) return r`<uui-loader></uui-loader>`;
    const e = this._items, t = e.filter((a) => a.status).length, s = k(this, _, he).call(this);
    return r`
      <div class="stack">
        <div class="row header">
          <p class="summary" tabindex="-1"><strong>${t} of ${e.length}</strong> checked</p>
          <fieldset class="views">
            <legend class="visually-hidden">Show</legend>
            ${zt.map((a) => r`
              <label><input type="radio" name="view" .checked=${this._view === a.id} @change=${() => this._view = a.id}>
                ${a.label} <span class="muted">(${e.filter(a.test).length})</span></label>`)}
          </fieldset>
        </div>
        <p class="muted">These criteria need a person to judge them. Follow the steps, then record the result. “Checked by the scanner” means its automated rules found nothing wrong, so those are usually quick to confirm. Results become “needs re-check” after the interval set in Settings.</p>
        <p role="status" class="visually-hidden">${this._saved ?? ""}</p>
        ${s.length ? r`<ul class="items">${Vt(s, (a) => a.criterion.id, (a) => k(this, _, Ot).call(this, a))}</ul>` : r`<uui-box><p>Nothing here.</p></uui-box>`}
      </div>`;
  }
};
_ = /* @__PURE__ */ new WeakSet();
ze = async function() {
  const { data: e, error: t } = await x(this, is(this.siteKey), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load the checklist.";
    return;
  }
  this._error = null, this._items = e;
};
ke = async function(e, t, s) {
  if (!t) return;
  const { error: a } = await x(this, as(this.siteKey, e.criterion.id, t, s));
  a || (k(this, _, Re).call(this, e.criterion.id), this._confirmed = { id: e.criterion.id, text: "Saved" }, await k(this, _, Ie).call(this, e, `${e.criterion.id} saved as ${Rt.find((i) => i.id === t).label}.`));
};
Pt = async function(e) {
  const { error: t } = await x(this, rs(this.siteKey, e.criterion.id));
  t || (this._confirmed = { id: e.criterion.id, text: "Cleared" }, await k(this, _, Ie).call(this, e, `${e.criterion.id} cleared; it's not checked any more.`));
};
Tt = function(e, t) {
  const s = t.trim();
  s === (e.note ?? "") ? k(this, _, Re).call(this, e.criterion.id) : this._drafts = { ...this._drafts, [e.criterion.id]: s };
};
Re = function(e) {
  if (!(e in this._drafts)) return;
  const { [e]: t, ...s } = this._drafts;
  this._drafts = s;
};
he = function() {
  return (this._items ?? []).filter(zt.find((e) => e.id === this._view).test);
};
Ie = async function(e, t) {
  const s = k(this, _, he).call(this).findIndex((h) => h.criterion.id === e.criterion.id);
  if (this._saved = t, await k(this, _, ze).call(this), await this.updateComplete, this.renderRoot.activeElement) return;
  const a = k(this, _, he).call(this), i = a.find((h) => h.criterion.id === e.criterion.id), n = i ?? a[Math.min(s, a.length - 1)];
  (i ? this.renderRoot.querySelector(`[aria-labelledby="${ne(i)}"] button`) : n ? this.renderRoot.querySelector(`#${ne(n)}`) : this.renderRoot.querySelector(".summary"))?.focus();
};
Ot = function(e) {
  const t = e.criterion, s = t.id in this._drafts, a = `${ne(e)}-note-hint`;
  return r`
      <li>
        <uui-box>
          <div class="item">
            <div class="title-row">
              <h3 id=${ne(e)} tabindex="-1">${t.id} ${t.title}</h3>
              <span class="tag">Level ${t.level}</span>
              ${t.since !== "V20" ? r`<span class="tag">New in ${st(t.since)}</span>` : l}
              ${e.needsRecheck ? r`<span class="tag warn">Needs re-check</span>` : l}
              ${e.scannerChecked ? r`<span class="tag">Checked by the scanner</span>` : l}
            </div>
            <p><strong>How to test:</strong> ${t.howToTest}</p>
            ${e.automatedFindings ? r`<p>
              <a href="#" @click=${(i) => {
    i.preventDefault(), me(this, "issues", { criterion: t.id });
  }}>
                The scanner found ${e.automatedFindings} ${e.automatedFindings === 1 ? "thing" : "things"} to look at</a></p>` : l}
            ${e.documentLinks ? r`<p class="muted">This site links ${e.documentLinks} ${e.documentLinks === 1 ? "document" : "documents"} (PDF/Office); they must be checked separately.</p>` : l}
            <details>
              <summary>Why it matters and how to fix</summary>
              <p>${t.why}</p><p>${t.howToFix}</p><p class="muted">${t.caveats}</p>
              <p><a href=${t.understandingUrl} target="_blank" rel="noopener">Understanding ${t.id} (W3C)<span class="visually-hidden"> (opens in a new tab)</span></a></p>
            </details>
            <div class="row" role="group" aria-labelledby=${ne(e)}>
              ${Rt.map((i) => r`
                <button class="status ${i.id}" aria-pressed=${e.status === i.id ? "true" : "false"}
                  @click=${() => k(this, _, ke).call(this, e, i.id, s ? this._drafts[t.id] || null : e.note)}>${i.label}</button>`)}
              ${e.status ? r`<button class="clear" @click=${() => k(this, _, Pt).call(this, e)}>Clear result</button>` : l}
              ${e.updatedUtc ? r`<span class="muted">${e.userName}, ${xe(e.updatedUtc, !1)}</span>` : l}
              ${this._confirmed?.id === t.id ? r`<span class="confirmed" aria-hidden="true"><uui-icon name="icon-check"></uui-icon> ${this._confirmed.text}</span>` : l}
            </div>
            <label class="note">
              <span>Note${e.status ? "" : " (saved with a result)"}</span>
              <textarea rows="2" maxlength=${js} .value=${e.note ?? ""} aria-describedby=${s ? a : l}
                @input=${(i) => k(this, _, Tt).call(this, e, i.target.value)}
                @blur=${() => {
    e.status && s && k(this, _, ke).call(this, e, e.status, this._drafts[t.id] || null);
  }}></textarea>
            </label>
            ${s ? r`<p id=${a} class="unsaved"><uui-icon name="icon-alert"></uui-icon>
              ${e.status ? "Not saved yet. It saves when you leave the note field." : "Not saved yet. Choose Pass, Fail or Not applicable to save it with the note."}</p>` : l}
          </div>
        </uui-box>
      </li>`;
};
N.styles = [se, X`
    .header { justify-content: space-between; }
    .header p { margin: 0; }
    .views { border: 0; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); }
    .views label { display: flex; align-items: center; gap: 6px; min-height: 44px; cursor: pointer; }
    .views input { width: 18px; height: 18px; }
    .items { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); }
    .item { display: grid; gap: var(--uui-size-space-3); }
    .item p { margin: 0; }
    .title-row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
    .title-row h3 { font-size: 1rem; }
    h3:focus, .summary:focus { outline: none; }
    h3:focus-visible, .summary:focus-visible { outline: 2px solid var(--uui-color-focus); }
    button.clear { min-height: 44px; padding: 0 var(--uui-size-space-3); font: inherit; cursor: pointer; background: none; border: 0; color: var(--uui-color-interactive); text-decoration: underline; }
    button.clear:hover { color: var(--uui-color-interactive-emphasis); }
    .tag.warn { border-color: var(--a11y-warning); }
    details p { margin: var(--uui-size-space-2) 0 0; }
    button.status {
      min-height: 44px; min-width: 44px; padding: 0 var(--uui-size-space-5); font: inherit; cursor: pointer;
      border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    button.status[aria-pressed='true'] { font-weight: 600; border-width: 2px; }
    button.status.pass[aria-pressed='true'] { border-color: var(--a11y-good); }
    button.status.fail[aria-pressed='true'] { border-color: var(--a11y-critical); }
    button.status.na[aria-pressed='true'] { border-color: var(--uui-color-border-emphasis); }
    button.status[aria-pressed='true']::before { content: '✓ '; }
    .confirmed { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; }
    .confirmed uui-icon { color: var(--a11y-good); }
    .unsaved { display: flex; align-items: center; gap: var(--uui-size-space-2); max-width: 40rem; padding: var(--uui-size-space-2) var(--uui-size-space-3); border-left: 4px solid var(--a11y-warning); background: var(--uui-color-surface-alt); }
    .unsaved uui-icon { flex: none; }
    .note { display: grid; gap: 4px; max-width: 40rem; }
    textarea { font: inherit; padding: var(--uui-size-space-2); border: 1px solid var(--uui-color-border-standalone); border-radius: var(--uui-border-radius); background: var(--uui-color-surface); color: var(--uui-color-text); }
  `];
V([
  M({ attribute: !1 })
], N.prototype, "siteKey", 2);
V([
  d()
], N.prototype, "_items", 2);
V([
  d()
], N.prototype, "_view", 2);
V([
  d()
], N.prototype, "_error", 2);
V([
  d()
], N.prototype, "_saved", 2);
V([
  d()
], N.prototype, "_confirmed", 2);
V([
  d()
], N.prototype, "_drafts", 2);
N = V([
  ee("esatto-a11y-checklist")
], N);
var Xs = Object.defineProperty, Js = Object.getOwnPropertyDescriptor, It = (e) => {
  throw TypeError(e);
}, ie = (e, t, s, a) => {
  for (var i = a > 1 ? void 0 : a ? Js(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (i = (a ? o(t, s, i) : o(i)) || i);
  return a && i && Xs(t, s, i), i;
}, Le = (e, t, s) => t.has(e) || It("Cannot " + s), Ys = (e, t, s) => (Le(e, t, "read from private field"), t.get(e)), Ye = (e, t, s) => t.has(e) ? It("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), Qs = (e, t, s, a) => (Le(e, t, "write to private field"), t.set(e, s), s), u = (e, t, s) => (Le(e, t, "access private method"), s), pe, c, j, E, Lt, Nt, Z, G, ue;
const Zs = {
  ButtonLabel: "Accessibility",
  DialogTitle: "Accessibility preferences",
  LargerText: "Larger text",
  Spacing: "Bigger buttons and more spacing",
  HighContrast: "High contrast",
  ReduceMotion: "Reduce motion",
  ReadableFont: "Readable font",
  HighlightLinks: "Highlight links",
  ReadingGuide: "Reading guide",
  Reset: "Reset",
  Close: "Close",
  Note: "These settings are saved in this browser only."
}, ei = [
  { key: "largerText", label: "Larger text" },
  { key: "spacing", label: "Bigger buttons and more spacing" },
  { key: "highContrast", label: "High contrast" },
  { key: "reduceMotion", label: "Reduce motion" },
  { key: "readableFont", label: "Readable font" },
  { key: "highlightLinks", label: "Highlight links" },
  { key: "readingGuide", label: "Reading guide" }
], Mt = (e) => JSON.parse(JSON.stringify(e));
let F = class extends te {
  constructor() {
    super(), Ye(this, c), this.settings = null, this._draft = null, this._saving = !1, this._cultures = ["en", "sv"], this._newCulture = "", Ye(this, pe), this.consumeContext(Bt, (e) => Qs(this, pe, e));
  }
  willUpdate(e) {
    e.has("settings") && this.settings && (this._draft = Mt(this.settings.settings), this._cultures = [.../* @__PURE__ */ new Set(["en", "sv", ...Object.keys(this._draft.visitor.panel.texts ?? {})])]);
  }
  render() {
    const e = this._draft;
    if (!e) return r`<uui-loader></uui-loader>`;
    const t = e.visitor.panel;
    return r`
      <form class="stack" @submit=${u(this, c, Lt)}>
        ${this.settings?.locked.length ? r`<p class="muted">Fields marked “Set in configuration” come from appsettings (<code>Esatto:Accessibility</code>) and can't be changed here.</p>` : l}

        <uui-box headline="Target">
          <div class="row">
            ${u(this, c, ue).call(this, "version", "WCAG version", e.version, [["V20", "2.0"], ["V21", "2.1"], ["V22", "2.2"]], (s) => u(this, c, E).call(this, (a) => a.version = s))}
            ${u(this, c, ue).call(this, "level", "Level", e.level, [["A", "A"], ["AA", "AA"], ["AAA", "AAA"]], (s) => u(this, c, E).call(this, (a) => a.level = s))}
          </div>
          <p class="muted">The EU legal requirement today (European Accessibility Act, EN 301 549) is WCAG 2.1 AA. 2.2 AA adds six criteria, including minimum target size.</p>
          ${e.level === "AAA" ? r`<p class="warning" role="note"><uui-icon name="icon-alert"></uui-icon> W3C does not recommend AAA as a requirement for entire sites: some AAA criteria cannot be met for all content.</p>` : l}
        </uui-box>

        <uui-box headline="Scan">
          <div class="stack">
            <div class="field">
              <label for="exclude">Never scan these paths (one per line, e.g. /search)</label>
              <textarea id="exclude" rows="3" ?disabled=${u(this, c, j).call(this, "scan.excludePaths")} .value=${e.scan.excludePaths.join(`
`)}
                @change=${(s) => u(this, c, E).call(this, (a) => a.scan.excludePaths = s.target.value.split(`
`).map((i) => i.trim()).filter(Boolean))}></textarea>
              ${u(this, c, Z).call(this, "scan.excludePaths")}
            </div>
            ${u(this, c, G).call(this, "scan.mobileWidth", "Also scan at mobile width (320 px)", e.scan.mobileWidth, (s) => u(this, c, E).call(this, (a) => a.scan.mobileWidth = s), "Doubles the scan time.")}
            ${u(this, c, G).call(this, "scan.bestPractice", "Include axe best-practice rules", e.scan.bestPractice, (s) => u(this, c, E).call(this, (a) => a.scan.bestPractice = s), "Shown separately in Issues; never counted in the score.")}
            <div class="field">
              <label for="recheck">Ask to re-check manual results after (months)</label>
              <input id="recheck" type="number" min="1" max="24" ?disabled=${u(this, c, j).call(this, "scan.recheckMonths")} .value=${String(e.scan.recheckMonths)}
                @change=${(s) => u(this, c, E).call(this, (a) => a.scan.recheckMonths = Number(s.target.value) || 6)}>
              ${u(this, c, Z).call(this, "scan.recheckMonths")}
            </div>
          </div>
        </uui-box>

        <uui-box headline="Visitor features (public website)">
          <div class="stack">
            <p class="notice"><uui-icon name="icon-info"></uui-icon> The preferences button doesn't make the site compliant. It's an extra on top of a site that already passes.</p>
            <fieldset>
              <legend>Follow visitors' browser and OS settings</legend>
              ${u(this, c, G).call(this, "visitor.followOsMotion", "Reduced motion", e.visitor.followOsMotion, (s) => u(this, c, E).call(this, (a) => a.visitor.followOsMotion = s))}
              ${u(this, c, G).call(this, "visitor.followOsContrast", "More contrast", e.visitor.followOsContrast, (s) => u(this, c, E).call(this, (a) => a.visitor.followOsContrast = s), "Off by default: generic contrast CSS is what most often clashes with a design. Test the site with it on before enabling.")}
            </fieldset>
            <fieldset>
              <legend>Accessibility preferences button</legend>
              ${u(this, c, G).call(this, "visitor.panel.enabled", "Show the preferences button", t.enabled, (s) => u(this, c, E).call(this, (a) => a.visitor.panel.enabled = s))}
              <div class="indent">
                ${ei.map((s) => u(this, c, G).call(this, `visitor.panel.${s.key}`, s.label, t[s.key], (a) => u(this, c, E).call(this, (i) => i.visitor.panel[s.key] = a), void 0, !t.enabled))}
                ${u(this, c, ue).call(this, "visitor.panel.position", "Button position", t.position, [["bottom-right", "Bottom right"], ["bottom-left", "Bottom left"]], (s) => u(this, c, E).call(this, (a) => a.visitor.panel.position = s), !t.enabled)}
              </div>
            </fieldset>
            <details>
              <summary>Panel texts per language</summary>
              <p class="muted">Leave a field empty to use the built-in text (English and Swedish ship with the package).</p>
              ${this._cultures.map((s) => u(this, c, Nt).call(this, s))}
              <div class="row">
                <div class="field">
                  <label for="new-culture">Add a language (e.g. da, de)</label>
                  <input id="new-culture" .value=${this._newCulture} @input=${(s) => this._newCulture = s.target.value.trim().toLowerCase()}>
                </div>
                <uui-button look="secondary" label="Add language" ?disabled=${!/^[a-z]{2}(-[a-z]{2})?$/.test(this._newCulture)}
                  @click=${() => {
      this._cultures = [.../* @__PURE__ */ new Set([...this._cultures, this._newCulture])], this._newCulture = "";
    }}></uui-button>
              </div>
            </details>
          </div>
        </uui-box>

        <div class="row">
          <uui-button type="submit" look="primary" color="positive" label="Save settings" .state=${this._saving ? "waiting" : void 0}></uui-button>
        </div>
      </form>`;
  }
};
pe = /* @__PURE__ */ new WeakMap();
c = /* @__PURE__ */ new WeakSet();
j = function(e) {
  const t = e.toLowerCase();
  return (this.settings?.locked ?? []).some((s) => s.toLowerCase() === t);
};
E = function(e) {
  const t = Mt(this._draft);
  e(t), this._draft = t;
};
Lt = async function(e) {
  if (e.preventDefault(), !this._draft) return;
  this._saving = !0;
  const { data: t, error: s } = await x(this, Xt(this._draft));
  this._saving = !1, !(s || !t) && (Ys(this, pe)?.peek("positive", { data: { headline: "Settings saved", message: "The public site picks them up within a minute." } }), this.dispatchEvent(new CustomEvent("saved", { detail: t })));
};
Nt = function(e) {
  const t = this._draft.visitor.panel.texts?.[e] ?? {};
  return r`
      <table>
        <caption>${e}</caption>
        <thead><tr><th scope="col">Text</th><th scope="col">Override</th></tr></thead>
        <tbody>
          ${Object.entries(Zs).map(([s, a]) => {
    const i = `t-${e}-${s}`, n = `visitor.panel.texts.${e}.${s}`;
    return r`<tr>
              <th scope="row"><label for=${i}>${s}</label></th>
              <td>
                <input id=${i} placeholder=${a} ?disabled=${u(this, c, j).call(this, n)} .value=${t[s] ?? ""}
                  @change=${(o) => u(this, c, E).call(this, (h) => {
      const P = o.target.value.trim();
      h.visitor.panel.texts ??= {};
      const T = h.visitor.panel.texts[e] ??= {};
      P ? T[s] = P : delete T[s];
    })}>
                ${u(this, c, Z).call(this, n)}
              </td>
            </tr>`;
  })}
        </tbody>
      </table>`;
};
Z = function(e) {
  return u(this, c, j).call(this, e) ? r`<span class="lock"><uui-icon name="icon-lock"></uui-icon> Set in configuration</span>` : l;
};
G = function(e, t, s, a, i, n = !1) {
  const o = `c-${e.split(".").join("-")}`, h = i ? `${o}-hint` : void 0;
  return r`
      <div class="check">
        <input id=${o} type="checkbox" .checked=${s} ?disabled=${n || u(this, c, j).call(this, e)} aria-describedby=${h ?? l}
          @change=${(P) => a(P.target.checked)}>
        <label for=${o}>${t}</label>
        ${u(this, c, Z).call(this, e)}
        ${i ? r`<p id=${h} class="muted hint">${i}</p>` : l}
      </div>`;
};
ue = function(e, t, s, a, i, n = !1) {
  const o = `s-${e.split(".").join("-")}`;
  return r`
      <div class="field">
        <label for=${o}>${t}</label>
        <select id=${o} ?disabled=${n || u(this, c, j).call(this, e)} @change=${(h) => i(h.target.value)}>
          ${a.map(([h, P]) => r`<option value=${h} ?selected=${h === s}>${P}</option>`)}
        </select>
        ${u(this, c, Z).call(this, e)}
      </div>`;
};
F.styles = [se, X`
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
ie([
  M({ attribute: !1 })
], F.prototype, "settings", 2);
ie([
  d()
], F.prototype, "_draft", 2);
ie([
  d()
], F.prototype, "_saving", 2);
ie([
  d()
], F.prototype, "_cultures", 2);
ie([
  d()
], F.prototype, "_newCulture", 2);
F = ie([
  ee("esatto-a11y-settings")
], F);
var ti = Object.defineProperty, si = Object.getOwnPropertyDescriptor, Ut = (e) => {
  throw TypeError(e);
}, B = (e, t, s, a) => {
  for (var i = a > 1 ? void 0 : a ? si(t, s) : t, n = e.length - 1, o; n >= 0; n--)
    (o = e[n]) && (i = (a ? o(t, s, i) : o(i)) || i);
  return a && i && ti(t, s, i), i;
}, ii = (e, t, s) => t.has(e) || Ut("Cannot " + s), ai = (e, t, s) => t.has(e) ? Ut("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, s), Kt = (e, t, s) => (ii(e, t, "access private method"), s), fe, Dt, Ft;
const ri = [
  { id: "overview", label: "Overview" },
  { id: "issues", label: "Issues" },
  { id: "checklist", label: "Manual checklist" },
  { id: "settings", label: "Settings" }
];
let I = class extends te {
  constructor() {
    super(), ai(this, fe), this._knowledge = null, this._sites = [], this._siteKey = "", this._settings = null, this._tab = "overview", this._filter = {}, this._error = null, this.addEventListener(et, (e) => {
      const { tab: t, filter: s } = e.detail;
      this._filter = s ?? {}, this._tab = t;
    });
  }
  async connectedCallback() {
    super.connectedCallback();
    const [e, t, s] = await Promise.all([
      x(this, qt(), { disableNotifications: !0 }),
      x(this, Gt(), { disableNotifications: !0 }),
      x(this, jt(), { disableNotifications: !0 })
    ]);
    if (e.error || t.error || s.error || !e.data || !t.data || !s.data) {
      this._error = "Could not load the accessibility dashboard.";
      return;
    }
    this._knowledge = e.data, this._sites = t.data, this._siteKey = t.data[0]?.key ?? "", this._settings = s.data;
  }
  render() {
    if (this._error) return r`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._settings || !this._knowledge) return r`<uui-loader></uui-loader>`;
    if (!this._sites.length)
      return r`<uui-box headline="Accessibility"><p>There is no published site to scan yet. Publish a root page with a template first.</p></uui-box>`;
    const e = ri.filter((t) => t.id !== "settings" || this._settings.canEdit);
    return r`
      <div class="header">
        <h2>Accessibility</h2>
        ${this._sites.length > 1 ? r`
          <uui-select label="Site" .options=${this._sites.map((t) => ({ name: t.name, value: t.key, selected: t.key === this._siteKey }))}
            @change=${(t) => this._siteKey = t.target.value}></uui-select>` : l}
      </div>
      <uui-tab-group>
        ${e.map((t) => r`<uui-tab id="tab-${t.id}" aria-controls="tabpanel" label=${t.label} ?active=${this._tab === t.id} @click=${() => {
      this._filter = {}, this._tab = t.id;
    }}>${t.label}</uui-tab>`)}
      </uui-tab-group>
      <div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${this._tab}">${Kt(this, fe, Ft).call(this)}</div>`;
  }
};
fe = /* @__PURE__ */ new WeakSet();
Dt = function(e) {
  this._settings = e.detail;
};
Ft = function() {
  switch (this._tab) {
    case "issues":
      return r`<esatto-a11y-issues .siteKey=${this._siteKey} .filter=${this._filter} .settings=${this._settings}></esatto-a11y-issues>`;
    case "checklist":
      return r`<esatto-a11y-checklist .siteKey=${this._siteKey}></esatto-a11y-checklist>`;
    case "settings":
      return r`<esatto-a11y-settings .settings=${this._settings} @saved=${Kt(this, fe, Dt)}></esatto-a11y-settings>`;
    default:
      return r`<esatto-a11y-overview .siteKey=${this._siteKey} .settings=${this._settings}></esatto-a11y-overview>`;
  }
};
I.styles = [se, X`
    :host { padding: var(--uui-size-layout-1); }
    .header { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; justify-content: space-between; margin-bottom: var(--uui-size-space-4); }
    uui-tab-group { border-bottom: 1px solid var(--uui-color-divider); }
    .panel { padding-top: var(--uui-size-space-5); }
  `];
B([
  d()
], I.prototype, "_knowledge", 2);
B([
  d()
], I.prototype, "_sites", 2);
B([
  d()
], I.prototype, "_siteKey", 2);
B([
  d()
], I.prototype, "_settings", 2);
B([
  d()
], I.prototype, "_tab", 2);
B([
  d()
], I.prototype, "_filter", 2);
B([
  d()
], I.prototype, "_error", 2);
I = B([
  ee("esatto-accessibility-dashboard")
], I);
const di = I;
export {
  I as EsattoAccessibilityDashboardElement,
  di as default
};
//# sourceMappingURL=accessibility-dashboard.js.map
