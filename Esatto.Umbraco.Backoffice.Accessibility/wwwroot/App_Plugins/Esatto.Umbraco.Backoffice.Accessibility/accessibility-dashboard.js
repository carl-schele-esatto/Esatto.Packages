import { css as X, property as L, state as p, customElement as ee, html as n, nothing as l, styleMap as pe, repeat as Xt } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as te } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as C } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as f } from "@umbraco-cms/backoffice/http-client";
import { UMB_NOTIFICATION_CONTEXT as Jt } from "@umbraco-cms/backoffice/notification";
const v = "/umbraco/management/api/v1/esatto/accessibility", m = () => [{ scheme: "bearer", type: "http" }], J = (e) => `site=${encodeURIComponent(e)}`;
function it(e, t) {
  const i = new URLSearchParams({ site: e });
  return t.principle && i.set("principle", String(t.principle)), t.level && i.set("level", t.level), t.owner && i.set("owner", t.owner), t.impact && i.set("impact", t.impact), t.criterion && i.set("criterion", t.criterion), t.falsePositives && i.set("falsePositives", "true"), t.ruleId && i.set("ruleId", t.ruleId), t.kind && i.set("kind", t.kind), i.toString();
}
const Yt = () => f.get({ url: `${v}/knowledge`, security: m() }), Qt = () => f.get({ url: `${v}/sites`, security: m() }), Zt = (e) => f.get({ url: `${v}/pages?${J(e)}`, security: m() }), ei = () => f.get({ url: `${v}/settings`, security: m() }), ti = (e) => f.put({ url: `${v}/settings`, body: e, security: m(), headers: { "Content-Type": "application/json" } }), ii = (e, t, i) => f.post({ url: `${v}/scans`, body: { siteKey: e, mobile: t, pagesTotal: i }, security: m(), headers: { "Content-Type": "application/json" } }), si = (e) => f.get({ url: `${v}/scans/${e}/done`, security: m() }), ai = (e, t) => f.post({ url: `${v}/scans/${e}/pages`, body: t, security: m(), headers: { "Content-Type": "application/json" } }), ni = (e, t) => f.post({ url: `${v}/scans/${e}/finish`, body: { complete: t }, security: m(), headers: { "Content-Type": "application/json" } }), ri = (e, t) => f.post({ url: `${v}/recheck?${J(e)}`, body: t, security: m(), headers: { "Content-Type": "application/json" } }), oi = (e) => f.get({ url: `${v}/overview?${J(e)}`, security: m() }), st = (e, t) => f.get({ url: `${v}/issues?${it(e, t)}`, security: m() }), li = (e, t) => f.get({ url: `${v}/issues/csv?${it(e, t)}`, security: m(), parseAs: "blob" }), ci = (e) => f.get({ url: `${v}/checklist?${J(e)}`, security: m() }), di = (e, t, i, s) => f.put({ url: `${v}/checklist/${encodeURIComponent(t)}?${J(e)}`, body: { status: i, note: s }, security: m(), headers: { "Content-Type": "application/json" } }), ui = (e, t) => f.delete({ url: `${v}/checklist/${encodeURIComponent(t)}?${J(e)}`, security: m() }), pi = (e, t) => f.post({ url: `${v}/false-positives?${J(e)}`, body: t, security: m(), headers: { "Content-Type": "application/json" } }), hi = (e) => f.delete({ url: `${v}/false-positives/${e}`, security: m() }), ie = X`
  :host {
    display: block;
    --a11y-critical: #c9374c;
    --a11y-critical-on: #fff;
    --a11y-warning: #f59e0b;
    --a11y-warning-on: #1d1d1f;
    --a11y-notice: #2f6fde;
    --a11y-notice-on: #fff;
    --a11y-good: #0e7f41;
    --a11y-good-on: #fff;
    --a11y-neutral: color-mix(in srgb, var(--uui-color-text) 70%, var(--uui-color-surface));
    --a11y-neutral-on: var(--uui-color-surface);

    --a11y-muted: color-mix(in srgb, var(--uui-color-text) 68%, var(--uui-color-surface));
    --a11y-line: var(--uui-color-border);
    --a11y-wash: color-mix(in srgb, var(--uui-color-text) 4%, var(--uui-color-surface));
    --a11y-radius: 12px;
    --a11y-radius-sm: 8px;
    --a11y-shadow: var(--uui-shadow-depth-1);
    --a11y-shadow-raised: var(--uui-shadow-depth-3);
    --a11y-ease: 160ms ease;
    /* One gap between all blocks on a tab (cards, rows of cards, the save button), so spacing reads as one rhythm. */
    --a11y-gap: var(--uui-size-space-5);
  }

  /* Tones: set --tone/--tone-on once, every component below reads them. */
  .tone-critical { --tone: var(--a11y-critical); --tone-on: var(--a11y-critical-on); }
  .tone-warning { --tone: var(--a11y-warning); --tone-on: var(--a11y-warning-on); }
  .tone-notice { --tone: var(--a11y-notice); --tone-on: var(--a11y-notice-on); }
  .tone-good { --tone: var(--a11y-good); --tone-on: var(--a11y-good-on); }
  .tone-none { --tone: var(--a11y-neutral); --tone-on: var(--a11y-neutral-on); }

  .muted { color: var(--a11y-muted); }
  .small { font-size: 13px; }
  .row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .stack > * + * { margin-top: var(--uui-size-space-4); }
  .page { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--a11y-gap); }
  .error { color: var(--uui-color-danger-standalone); }
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  code { padding: 1px 6px; border-radius: 6px; background: var(--a11y-wash); border: 1px solid var(--a11y-line); word-break: break-word; }
  pre {
    white-space: pre-wrap; word-break: break-all; margin: 0; padding: var(--uui-size-space-3) var(--uui-size-space-4);
    background: var(--a11y-wash); border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius-sm);
  }
  a { color: var(--uui-color-interactive); text-underline-offset: 2px; }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
  .url { word-break: break-all; font-size: 13px; }

  /* Surfaces */
  .card {
    background: var(--uui-color-surface); color: var(--uui-color-text);
    border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius); box-shadow: var(--a11y-shadow);
    padding: var(--uui-size-space-5) var(--uui-size-space-6);
  }
  .card-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-bottom: var(--uui-size-space-4); }
  .card-head h3 { font-size: 16px; font-weight: 700; }
  .card-head .end { margin-left: auto; }
  .icon-badge {
    flex: none; display: inline-grid; place-items: center; width: 34px; height: 34px; border-radius: 10px; font-size: 18px;
    background: color-mix(in srgb, var(--tone, var(--a11y-notice)) 14%, var(--uui-color-surface));
    color: color-mix(in srgb, var(--tone, var(--a11y-notice)) 80%, var(--uui-color-text));
  }
  .icon-badge.solid { background: var(--tone); color: var(--tone-on); }
  .icon-badge.round { border-radius: 50%; }
  .section-label { font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--a11y-muted); margin-bottom: var(--uui-size-space-3); }

  /* Badges: .tag is solid in its tone (neutral wash without one); .chip is a quiet fact. */
  .tag {
    display: inline-flex; gap: 5px; align-items: center; padding: 2px 10px; border-radius: 999px;
    font-size: 12px; font-weight: 700; line-height: 18px; letter-spacing: 0.01em; white-space: nowrap;
    background: var(--a11y-wash); color: var(--uui-color-text); border: 1px solid var(--a11y-line);
  }
  .tag[class*='tone-'] { background: var(--tone); color: var(--tone-on); border-color: transparent; }
  .tag uui-icon { font-size: 13px; }
  .chip {
    display: inline-flex; gap: 6px; align-items: center; padding: 2px 10px; border-radius: 999px; font-size: 13px; line-height: 20px;
    white-space: nowrap; background: var(--a11y-wash); border: 1px solid var(--a11y-line); color: var(--uui-color-text);
  }
  .chip uui-icon { font-size: 14px; color: var(--a11y-muted); }

  /* Alerts */
  .banner {
    display: flex; gap: var(--uui-size-space-4); align-items: flex-start; padding: var(--uui-size-space-4) var(--uui-size-space-5);
    border-radius: var(--a11y-radius); color: var(--uui-color-text);
    background: color-mix(in srgb, var(--tone) 9%, var(--uui-color-surface));
    border: 1px solid color-mix(in srgb, var(--tone) 45%, var(--a11y-line));
    box-shadow: inset 4px 0 0 var(--tone);
  }
  .banner > div { display: grid; gap: 4px; padding-top: 6px; }
  .banner.compact { padding: var(--uui-size-space-3) var(--uui-size-space-4); gap: var(--uui-size-space-3); align-items: center; }
  .banner.compact .icon-badge { width: 26px; height: 26px; font-size: 14px; }
  .banner.compact > div { padding-top: 0; }

  /* Form controls */
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label { font-size: 13px; font-weight: 600; }
  select, input:not([type='checkbox']):not([type='radio']), textarea {
    font: inherit; min-height: 40px; padding: 0 var(--uui-size-space-3); box-sizing: border-box;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--a11y-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--a11y-ease), box-shadow var(--a11y-ease);
  }
  textarea { padding: var(--uui-size-space-2) var(--uui-size-space-3); }
  select:hover, input:not([type='checkbox']):not([type='radio']):hover, textarea:hover { border-color: var(--uui-color-border-emphasis); }
  select:focus-visible, input:focus-visible, textarea:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  input[type='checkbox'], input[type='radio'] { accent-color: var(--uui-color-selected); }
  input:disabled, select:disabled, textarea:disabled { opacity: 0.65; cursor: not-allowed; }

  /* Tables */
  .table-wrap { overflow-x: auto; border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius-sm); }
  table { border-collapse: collapse; width: 100%; }
  th, td { text-align: left; vertical-align: top; padding: var(--uui-size-space-3) var(--uui-size-space-4); border-bottom: 1px solid var(--a11y-line); }
  tbody tr:last-child > * { border-bottom: 0; }
  thead th {
    font-size: 12px; font-weight: 700; letter-spacing: 0.05em; text-transform: uppercase; color: var(--a11y-muted);
    background: var(--a11y-wash); white-space: nowrap;
  }
  tbody tr { transition: background-color var(--a11y-ease); }
  tbody tr:hover { background: var(--a11y-wash); }
  td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }

  /* Disclosures: a chevron instead of the browser's triangle, and the hand pointer. */
  summary { cursor: pointer; list-style: none; }
  summary::-webkit-details-marker { display: none; }
  .chevron { flex: none; color: var(--a11y-muted); transition: transform var(--a11y-ease); }
  details[open] > summary .chevron { transform: rotate(90deg); }
  .disclosure > summary { display: inline-flex; gap: 6px; align-items: center; font-weight: 600; color: var(--uui-color-interactive); }
  .disclosure > summary .chevron { color: inherit; }
  .disclosure > summary:hover { color: var(--uui-color-interactive-emphasis); }

  /* Clickable cards lift on hover. */
  .lift { transition: transform var(--a11y-ease), box-shadow var(--a11y-ease), border-color var(--a11y-ease); }
  .lift:hover { transform: translateY(-2px); box-shadow: var(--a11y-shadow-raised); }

  /* Progress */
  .progress-bar { height: 8px; border-radius: 999px; background: var(--a11y-wash); border: 1px solid var(--a11y-line); overflow: hidden; }
  .progress-bar > span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #2f6fde, #6d4ce0); transition: width 400ms ease; }

  @media (prefers-reduced-motion: reduce) {
    *, .lift, .chevron, .progress-bar > span { transition: none !important; }
    .lift:hover { transform: none; }
  }
`, at = "esatto-a11y-navigate";
function ye(e, t, i) {
  e.dispatchEvent(new CustomEvent(at, { detail: { tab: t, filter: i }, bubbles: !0, composed: !0 }));
}
const Ve = ["V20", "V21", "V22"], qe = ["A", "AA", "AAA"], gi = [
  { tag: "wcag2a", version: "V20", level: "A" },
  { tag: "wcag2aa", version: "V20", level: "AA" },
  { tag: "wcag2aaa", version: "V20", level: "AAA" },
  { tag: "wcag21a", version: "V21", level: "A" },
  { tag: "wcag21aa", version: "V21", level: "AA" },
  { tag: "wcag22aa", version: "V22", level: "AA" }
];
function nt(e, t, i) {
  const s = gi.filter((a) => Ve.indexOf(a.version) <= Ve.indexOf(e) && qe.indexOf(a.level) <= qe.indexOf(t)).map((a) => a.tag);
  return i ? [...s, "best-practice"] : s;
}
const rt = (e) => ({ V20: "2.0", V21: "2.1", V22: "2.2" })[e], we = (e) => `WCAG ${rt(e.version)} ${e.level}`, ot = (e) => e ?? "Best practice", lt = (e) => e === "running" ? "not finished" : e;
function fi(e) {
  return e ? new Date(/[zZ]|[+-]\d\d:\d\d$/.test(e) ? e : `${e}Z`) : null;
}
function ze(e, t = !0) {
  const i = fi(e);
  return i ? new Intl.DateTimeFormat(void 0, t ? { dateStyle: "medium", timeStyle: "short" } : { dateStyle: "medium" }).format(i) : "";
}
function Ge(e) {
  return e === null ? null : e < 50 ? "critical" : e < 90 ? "warning" : "good";
}
const $e = { good: "Good", warning: "Needs work", critical: "Major gaps" }, vi = { good: "icon-check", warning: "icon-alert", critical: "icon-wrong" };
function mi(e) {
  return { toConfirm: e.scannerChecked, personOnly: e.notVerified - e.scannerChecked };
}
const xe = { critical: "tone-critical", serious: "tone-warning", moderate: "tone-notice", minor: "tone-none" }, ct = { Editor: "icon-edit", Developer: "icon-code", Both: "icon-users" };
function bi(e, t) {
  const i = `/umbraco/section/content/workspace/document/edit/${e}`;
  return t ? `${i}/${t}` : i;
}
function yi(e) {
  return `/umbraco/section/media/workspace/media/edit/${e}`;
}
function ne(e) {
  try {
    const t = new URL(e ?? "");
    return t.protocol === "http:" || t.protocol === "https:" ? t.toString() : void 0;
  } catch {
    return;
  }
}
const wi = /\.(pdf|docx?|xlsx|pptx)$/i;
function $i(e) {
  const t = /* @__PURE__ */ new Set();
  for (const i of e)
    try {
      wi.test(new URL(i).pathname) && t.add(i);
    } catch {
    }
  return [...t];
}
function xi(e, t) {
  if (e.status >= 400) return { kind: "failed", reason: `HTTP ${e.status}` };
  const i = new URL(e.url).origin;
  if (i !== t) return { kind: "failed", reason: `redirects to ${i}` };
  const s = (e.contentType ?? "").split(";")[0].trim().toLowerCase();
  if (s && s !== "text/html" && s !== "application/xhtml+xml") return { kind: "failed", reason: `not an HTML page (${s})` };
  if ((e.xFrameOptions ?? "").trim().toUpperCase() === "DENY") return { kind: "blocked", reason: "The site sends X-Frame-Options: DENY." };
  const r = /frame-ancestors\s+([^;]*)/i.exec(e.csp ?? "")?.[1].trim().toLowerCase();
  return r !== void 0 && (r === "'none'" || !/('self'|\*)/.test(r) && !r.includes(t.toLowerCase())) ? { kind: "blocked", reason: `The site sends Content-Security-Policy frame-ancestors ${r}.` } : null;
}
async function _i(e) {
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
const ki = (e) => e.map((t) => Array.isArray(t) ? t.join(" >>> ") : t).join(" "), je = (e, t) => ({
  ruleId: e.id,
  kind: t,
  impact: e.impact ?? null,
  help: e.help,
  helpUrl: e.helpUrl,
  tags: e.tags,
  nodes: e.nodes.map((i) => ({ selector: ki(i.target), html: i.html }))
});
function Ci(e, t, i) {
  return {
    contentKey: t.contentKey,
    name: t.name,
    culture: t.culture,
    url: t.url,
    viewport: t.viewport,
    status: "ok",
    reason: null,
    passes: e.passes.map((s) => ({ ruleId: s.id, tags: s.tags })),
    rules: [
      ...e.violations.map((s) => je(s, "violation")),
      ...e.incomplete.map((s) => je(s, "incomplete"))
    ],
    documentLinks: i
  };
}
function oe(e, t, i) {
  return { ...e, status: t, reason: i, passes: [], rules: [], documentLinks: [] };
}
const He = 900;
function Xe(e) {
  const t = new URL(e);
  return t.searchParams.set("esatto-a11y-scan", "1"), t.toString();
}
async function dt(e, t, i) {
  const s = document.createElement("iframe");
  s.setAttribute("aria-hidden", "true"), s.tabIndex = -1, s.title = "Accessibility scan", s.width = String(e.viewport), s.height = String(He), s.style.cssText = `position:absolute;left:-10000px;top:0;width:${e.viewport}px;height:${He}px;border:0;`;
  let a;
  try {
    const r = new Promise((o) => {
      a = setTimeout(() => o({ kind: "failed", reason: "timeout", result: oe(e, "failed", "timeout") }), i.timeoutMs);
    });
    return await Promise.race([Si(e, s, t, i), r]);
  } finally {
    clearTimeout(a), s.remove();
  }
}
async function Si(e, t, i, s) {
  let a;
  try {
    a = xi(await _i(Xe(e.url)), location.origin);
  } catch (b) {
    a = { kind: "failed", reason: b instanceof Error ? b.message : "request failed" };
  }
  if (a) return a.kind === "failed" ? { ...a, result: oe(e, "failed", a.reason ?? "failed") } : a;
  const r = new Promise((b) => t.addEventListener("load", () => b(), { once: !0 }));
  t.src = Xe(e.url), i.appendChild(t), await r, await Ei(s.settleMs);
  let o = null;
  try {
    o = t.contentDocument;
  } catch {
    o = null;
  }
  if (!o || o.location.href === "about:blank" || !o.documentElement)
    return { kind: "failed", reason: "the page left the frame", result: oe(e, "failed", "the page left the frame") };
  const c = t.contentWindow;
  if (!c) return { kind: "failed", reason: "the frame has no window" };
  if (!c.axe) {
    const b = o.createElement("script");
    b.src = s.axeUrl;
    const Y = new Promise((S, me) => {
      b.onload = () => S(), b.onerror = () => me(new Error("axe-core could not be loaded into the page (Content-Security-Policy script-src?)"));
    });
    o.head.appendChild(b);
    try {
      await Y;
    } catch (S) {
      return { kind: "blocked", reason: S instanceof Error ? S.message : "axe-core could not be loaded into the page." };
    }
  }
  const A = await c.axe.run(o, {
    runOnly: { type: "tag", values: s.tags },
    // Node lists only for what we store; `passes` still lists every passing rule id and its tags.
    resultTypes: ["violations", "incomplete"]
  }), P = $i([...o.querySelectorAll("a[href]")].map((b) => b.href));
  return { kind: "ok", result: Ci(A, e, P) };
}
const Ei = (e) => new Promise((t) => setTimeout(t, e)), _e = "other-origin:";
function zi(e, t) {
  const i = [], s = [];
  for (const a of e) {
    if (a.skipReason) {
      s.push({ ...a, skipReason: a.skipReason });
      continue;
    }
    const r = new URL(a.url);
    r.origin !== t ? s.push({ ...a, skipReason: `${_e}${r.origin}` }) : i.push(a);
  }
  return { scannable: i, skipped: s };
}
function Ai(e, t) {
  const i = new URL(t), s = e.join(", "), a = e.filter((o) => new URL(o).host === i.host), r = `Pages on ${s} were skipped: a browser can only scan pages on the same address as the backoffice (${t}), meaning the same scheme, host and port.`;
  if (a.length) {
    const o = (c) => new URL(c).protocol.replace(":", "");
    return `${r} These pages use ${o(a[0])} instead of ${o(i)}. Set the site's domain to ${t} in Culture and Hostnames, or open the backoffice on ${a[0]}.`;
  }
  return `${r} Open the backoffice on ${e[0]}${e.length > 1 ? " (and so on)" : ""} to scan them.`;
}
function ke(e, t) {
  return e.flatMap((i) => t.map((s) => ({
    contentKey: i.contentKey,
    name: i.name,
    culture: i.culture,
    url: i.url,
    viewport: s
  })));
}
class ut extends Error {
  constructor(t, i) {
    super(`The page ${t} can't be loaded in a frame${i ? ` (${i})` : ""}.`), this.url = t, this.reason = i, this.name = "BlockedFrameError";
  }
}
class Pi {
  constructor(t, i, s, a) {
    this.opts = i, this.run = s, this.settle = a, this.#e = [...t], this.#n = t.length;
  }
  #e;
  #t = /* @__PURE__ */ new Set();
  #i = 0;
  #n;
  #s = null;
  #a = !1;
  #r = [];
  get cancelled() {
    return this.#a;
  }
  onProgress(t) {
    this.#r.push(t);
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
    if (await Promise.all(t), this.#o(), this.#s) throw this.#s;
  }
  async #l() {
    for (; !this.#a && !this.#s; ) {
      const t = this.#e.shift();
      if (!t) return;
      this.#t.add(t), this.#o();
      try {
        const i = await this.#c(t);
        if (i.kind === "blocked") {
          this.#s ??= new ut(t.url, i.reason);
          return;
        }
        if (this.#a && i.kind === "failed") return;
        await this.settle(t, i), this.#i++;
      } catch (i) {
        this.#s ??= i instanceof Error ? i : new Error(String(i));
        return;
      } finally {
        this.#t.delete(t);
      }
    }
  }
  async #c(t) {
    let i = { kind: "failed", reason: "not run" };
    for (let s = 0; s <= this.opts.retries; s++) {
      try {
        i = await this.run(t);
      } catch (a) {
        i = { kind: "failed", reason: a instanceof Error ? a.message : String(a) };
      }
      if (i.kind !== "failed" || this.#a) return i;
    }
    return i;
  }
  #o() {
    const t = { done: this.#i, total: this.#n, current: [...this.#t].map((i) => i.url) };
    for (const i of this.#r) i(t);
  }
}
const ce = 1280, Ti = 320, Oi = 3, Ri = 1, Je = [1e3, 2e3, 4e3];
function Ii(e, t) {
  return t ? { mobile: t.mobile, resumeScanId: t.id } : { mobile: e };
}
function Ni(e) {
  return ke([e], e.viewports.length ? e.viewports : [ce]);
}
class Mi {
  constructor(t) {
    this.deps = t;
  }
  #e = null;
  #t = !1;
  cancel() {
    this.#t = !0, this.#e?.cancel();
  }
  async start(t, i, s) {
    const a = await this.deps.getPages(t), { scannable: r, skipped: o } = zi(a, this.deps.origin), c = [...new Set(o.filter((h) => h.skipReason.startsWith(_e)).map((h) => h.skipReason.slice(_e.length)))], A = i.mobile ? [ce, Ti] : [ce];
    let P = ke(r, A), b = o.map((h) => ({ job: ke([h], [ce])[0], reason: h.skipReason }));
    const Y = P.length + b.length, S = i.resumeScanId ?? await this.deps.startScan(t, i.mobile, Y);
    if (i.resumeScanId !== void 0) {
      const h = new Set((await this.deps.getDone(S)).map(be));
      P = P.filter((U) => !h.has(be(U))), b = b.filter((U) => !h.has(be(U.job)));
    }
    const me = Y - P.length - b.length;
    let De = 0, Fe = 0;
    const We = (h) => s?.({ ...h, done: h.done + De + me, total: Y });
    try {
      for (const h of b)
        await this.#i(S, oe(h.job, "skipped", h.reason)), De++;
      We({ done: 0, total: Y, current: [] }), this.#e = new Pi(
        P,
        { concurrency: Oi, retries: Ri },
        (h) => this.deps.runJob(h),
        (h, U) => (U.kind !== "ok" && Fe++, this.#i(S, U.kind === "ok" && U.result ? U.result : oe(h, "failed", U.reason ?? "failed")))
      ), this.#e.onProgress(We), this.#t && this.#e.cancel(), await this.#e.start();
    } catch (h) {
      return h instanceof ut ? { scanId: S, blocked: h, summary: await this.deps.finishScan(S, !1), otherOrigins: c } : { scanId: S, paused: h instanceof Error ? h : new Error(String(h)), otherOrigins: c };
    }
    const Be = P.length > 0 && Fe * 2 > P.length, Ht = !this.#t && !this.#e.cancelled && !Be;
    return { scanId: S, summary: await this.deps.finishScan(S, Ht), otherOrigins: c, mostlyFailed: Be };
  }
  /** Saves one page result, retrying with back-off; throws after the last retry. */
  async #i(t, i) {
    const s = this.deps.sleep ?? ((a) => new Promise((r) => setTimeout(r, a)));
    for (let a = 0; ; a++)
      try {
        await this.deps.postPage(t, i);
        return;
      } catch (r) {
        if (a >= Je.length) throw r;
        await s(Je[a]);
      }
  }
}
const be = (e) => `${e.contentKey}|${e.culture}|${e.viewport}`;
var Li = Object.defineProperty, Ui = Object.getOwnPropertyDescriptor, pt = (e) => {
  throw TypeError(e);
}, W = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? Ui(t, i) : t, r = e.length - 1, o; r >= 0; r--)
    (o = e[r]) && (a = (s ? o(t, i, a) : o(a)) || a);
  return s && a && Li(t, i, a), a;
}, Ae = (e, t, i) => t.has(e) || pt("Cannot " + i), Pe = (e, t, i) => (Ae(e, t, "read from private field"), t.get(e)), Ye = (e, t, i) => t.has(e) ? pt("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Qe = (e, t, i, s) => (Ae(e, t, "write to private field"), t.set(e, i), i), _ = (e, t, i) => (Ae(e, t, "access private method"), i), j, y, Te, Ce, ht, gt, ft, vt, mt, bt, de, yt;
const Ki = "/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js", T = { size: 168, stroke: 14 }, Di = { 1: "icon-eye", 2: "icon-keyboard", 3: "icon-lightbulb-active", 4: "icon-code" }, wt = { complete: "tone-good", incomplete: "tone-warning", running: "" };
async function ae(e) {
  const { data: t, error: i } = await e;
  if (i) throw i instanceof Error ? i : new Error(typeof i == "string" ? i : "Request failed");
  return t;
}
let N = class extends te {
  constructor() {
    super(...arguments), Ye(this, y), this.siteKey = "", this.settings = null, this._overview = null, this._error = null, this._progress = null, this._run = null, this._scanError = null, Ye(this, j, null);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), Pe(this, j)?.cancel();
  }
  updated(e) {
    e.has("siteKey") && this.siteKey && _(this, y, Te).call(this);
  }
  render() {
    if (this._error) return n`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._overview) return n`<uui-loader></uui-loader>`;
    const e = this._overview, t = Ge(e.score.percent);
    return n`
      <div class="page">
        <section class="card hero-card tone-${t ?? "none"}" aria-labelledby="hero-heading">
          <div class="hero">
            ${_(this, y, gt).call(this, e, t)}
            ${_(this, y, ft).call(this, e)}
            <div class="hero-body stack">
              <div>
                <p class="eyebrow">Accessibility score</p>
                <h3 id="hero-heading" class="hero-title">${t ? $e[t] : "Not scanned yet"}</h3>
              </div>
              ${_(this, y, vt).call(this, e)}
              ${_(this, y, mt).call(this, e)}
            </div>
          </div>
          ${_(this, y, bt).call(this)}
        </section>
        ${_(this, y, yt).call(this)}

        <section aria-labelledby="principles-heading">
          <h3 id="principles-heading" class="section-label">By principle</h3>
          <ul class="tiles">
            ${e.principles.map((i) => {
      const s = i.fail ? "critical" : i.toCheck ? "notice" : "good";
      return n`<li>
                <button class="tile lift tone-${s}" @click=${() => ye(this, "issues", { principle: i.principle })}>
                  <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${Di[i.principle] ?? "icon-checkbox"}></uui-icon></span>
                  <span class="tile-text">
                    <span class="tile-label">${i.principle}. ${i.name}</span>
                    <span class="tile-value">${i.fail} failing</span>
                    <span class="muted small">${i.toCheck} to check</span>
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
              <p><strong>${e.score.automatedPass}</strong> criteria pass · <strong>${e.score.automatedFail}</strong> fail</p>
            </div>
          </div>
          <div class="result">
            <span class="icon-badge tone-good" aria-hidden="true"><uui-icon name="icon-checkbox"></uui-icon></span>
            <div class="grow">
              <p class="result-label">Manual checklist</p>
              <p><strong>${e.manualChecked}</strong> of ${e.manualTotal} checked</p>
              <div class="progress-bar" aria-hidden="true"><span style=${pe({ width: `${e.manualTotal ? Math.round(e.manualChecked / e.manualTotal * 100) : 0}%` })}></span></div>
            </div>
          </div>
          <uui-button look="secondary" label="Open manual checklist" @click=${() => ye(this, "checklist")}>
            Open manual checklist&nbsp;<uui-icon name="icon-arrow-right" aria-hidden="true"></uui-icon>
          </uui-button>
        </section>

        ${e.history.length ? n`
          <section class="card" aria-labelledby="history-heading">
            <div class="card-head">
              <span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
              <h3 id="history-heading">Recent scans</h3>
            </div>
            <div class="table-wrap">
              <table>
                <caption class="visually-hidden">The last ${e.history.length} scans</caption>
                <thead><tr><th scope="col">Started</th><th scope="col">Target</th><th scope="col">Score</th><th scope="col" class="num">Pages</th><th scope="col">Status</th><th scope="col">By</th></tr></thead>
                <tbody>
                  ${e.history.map((i) => n`<tr>
                    <td>${ze(i.startedUtc)}</td>
                    <td>${i.target}</td>
                    <td>${i.score === null ? n`<span class="muted">–</span>` : n`
                      <span class="score-cell tone-${Ge(i.score)}">
                        <span class="score-bar" aria-hidden="true"><span style=${pe({ width: `${i.score}%` })}></span></span>
                        <span class="score-num">${i.score} %</span>
                      </span>`}</td>
                    <td class="num">${i.pagesDone}${i.pagesSkipped ? n` <span class="muted">(+${i.pagesSkipped} skipped)</span>` : l}</td>
                    <td><span class="tag ${wt[i.status]}">${lt(i.status)}</span></td>
                    <td>${i.userName}</td>
                  </tr>`)}
                </tbody>
              </table>
            </div>
          </section>` : l}
        <div id="frames" aria-hidden="true"></div>
      </div>`;
  }
};
j = /* @__PURE__ */ new WeakMap();
y = /* @__PURE__ */ new WeakSet();
Te = async function() {
  const { data: e, error: t } = await C(this, oi(this.siteKey), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load the accessibility overview.";
    return;
  }
  this._error = null, this._overview = e;
};
Ce = async function(e) {
  if (!this.settings) return;
  const t = this.settings.settings, i = nt(t.version, t.level, t.scan.bestPractice), s = this.renderRoot.querySelector("#frames");
  Qe(this, j, new Mi({
    origin: location.origin,
    getPages: (a) => ae(Zt(a)),
    startScan: async (a, r, o) => (await ae(ii(a, r, o))).scanId,
    getDone: (a) => ae(si(a)),
    postPage: async (a, r) => {
      await ae(ai(a, r));
    },
    finishScan: (a, r) => ae(ni(a, r)),
    runJob: (a) => dt(a, s, { axeUrl: Ki, tags: i, settleMs: 1e3, timeoutMs: 3e4 })
  })), this._run = null, this._scanError = null, this._progress = { done: 0, total: 0, current: [] };
  try {
    this._run = await Pe(this, j).start(this.siteKey, Ii(t.scan.mobileWidth, e), (a) => this._progress = a);
  } catch (a) {
    this._scanError = a instanceof Error ? a.message : "The scan could not start.";
  } finally {
    this._progress = null, Qe(this, j, null), await _(this, y, Te).call(this);
  }
};
ht = function() {
  Pe(this, j)?.cancel();
};
gt = function(e, t) {
  const i = e.score.percent, s = (T.size - T.stroke) / 2, a = 2 * Math.PI * s, r = (i ?? 0) / 100 * a, o = i === null ? "No score yet" : `Score ${i} percent, ${we(e.target)}, ${$e[t]}`, c = T.size / 2;
  return n`
      <figure class="ring" role="img" aria-label=${o}>
        <svg width=${T.size} height=${T.size} viewBox="0 0 ${T.size} ${T.size}" aria-hidden="true">
          <defs>
            <linearGradient id="ring-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" class="stop-a"></stop>
              <stop offset="100%" class="stop-b"></stop>
            </linearGradient>
          </defs>
          <circle class="track" cx=${c} cy=${c} r=${s} stroke-width=${T.stroke} fill="none"></circle>
          <circle class="fill ${i ? "" : "empty"}" cx=${c} cy=${c} r=${s} stroke-width=${T.stroke} fill="none" stroke="url(#ring-gradient)"
            stroke-dasharray="${r} ${a}" stroke-linecap="round" transform="rotate(-90 ${c} ${c})"></circle>
        </svg>
        <div class="ring-text" aria-hidden="true">
          <span class="ring-value">${i === null ? "–" : `${i}%`}</span>
          <span class="muted small">${we(e.target)}</span>
        </div>
        <figcaption aria-hidden="true">
          ${t ? n`<span class="tag tone-${t}"><uui-icon name=${vi[t]}></uui-icon>${$e[t]}</span>` : n`<span class="tag">Not scanned yet</span>`}
        </figcaption>
      </figure>`;
};
ft = function(e) {
  const t = mi(e.score);
  return n`
      <div class="facts">
        <ul class="fact-list">
          <li><span class="dot tone-good" aria-hidden="true"></span><strong>${e.score.pass}</strong> pass</li>
          <li><span class="dot tone-critical" aria-hidden="true"></span><strong>${e.score.fail}</strong> fail</li>
          <li><span class="dot tone-none" aria-hidden="true"></span><strong>${e.score.notVerified}</strong> not verified:</li>
          <li class="sub"><strong>${t.toConfirm}</strong> checked by the scanner, confirm by hand</li>
          <li class="sub"><strong>${t.personOnly}</strong> only a person can check</li>
          ${e.score.notApplicable ? n`<li><span class="dot" aria-hidden="true"></span><strong>${e.score.notApplicable}</strong> not applicable</li>` : l}
        </ul>
        <details class="disclosure why">
          <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>Why isn't this 100 %?</summary>
          <p>A criterion only counts as passed when someone has confirmed it in the manual checklist, or when the scanner can check it completely (very few can). Automated tests find only part of the problems, so a clean scan alone never scores 100 %.</p>
          <p>“Checked by the scanner” means its rules for the criterion found nothing wrong, but they test only the mechanical part (that an image has alt text, not that the text describes it). Start with those: they are usually quick to confirm. The rest, such as captions, keyboard use and reading order, only a person can check.</p>
        </details>
      </div>`;
};
vt = function(e) {
  const t = e.lastScan;
  return n`
      <ul class="meta" aria-label="Last scan">
        <li class="chip"><uui-icon name="icon-shield" aria-hidden="true"></uui-icon>Target ${we(e.target)}</li>
        ${t ? n`
          <li class="chip"><uui-icon name="icon-calendar" aria-hidden="true"></uui-icon>${ze(t.startedUtc)}</li>
          <li class="chip"><uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${t.pagesDone} ${t.pagesDone === 1 ? "page" : "pages"}${t.pagesSkipped ? ` · ${t.pagesSkipped} skipped` : ""}</li>
          ${t.pagesFailed ? n`<li class="chip"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${t.pagesFailed} failed</li>` : l}
          <li class="chip"><uui-icon name="icon-user" aria-hidden="true"></uui-icon>By ${t.userName}</li>
          ${t.status !== "complete" ? n`<li class="tag ${wt[t.status]}">${lt(t.status)}</li>` : l}` : l}
      </ul>
      ${t ? l : n`<p class="muted">This site hasn't been scanned yet.</p>`}`;
};
mt = function(e) {
  const t = this._progress !== null, i = e.lastScan && e.lastScan.status !== "complete" ? e.lastScan : null;
  return n`<div class="row">
      <uui-button look="primary" label="Scan now" ?disabled=${t} @click=${() => _(this, y, Ce).call(this)}>
        <uui-icon name="icon-sync" aria-hidden="true"></uui-icon>&nbsp;Scan now
      </uui-button>
      ${i && !t ? n`<uui-button look="secondary" label="Resume last scan" @click=${() => _(this, y, Ce).call(this, i)}></uui-button>` : l}
      ${t ? n`<uui-button look="secondary" label="Cancel scan" @click=${_(this, y, ht)}></uui-button>` : l}
    </div>
    <p class="muted small help">Pages load in hidden frames in your browser, so keep this dashboard open while scanning. Browsers slow down background tabs: a scan runs fastest while this tab is in front. Leaving the dashboard stops the scan; “Resume last scan” continues it.</p>`;
};
bt = function() {
  const e = this._progress;
  if (!e) return l;
  const t = e.current[0] ? new URL(e.current[0]).pathname : "";
  return n`
      <div class="progress">
        <div class="progress-bar" aria-hidden="true"><span style=${pe({ width: `${e.total ? Math.round(e.done / e.total * 100) : 0}%` })}></span></div>
        <p role="status">${e.total ? `Scanning page ${Math.min(e.done + 1, e.total)} of ${e.total}${t ? `: ${t}` : ""}` : "Getting the list of pages…"}</p>
      </div>`;
};
de = function(e, t) {
  return n`
      <div class="banner tone-critical" role="alert">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
        <div><h4>${e}</h4>${t}</div>
      </div>`;
};
yt = function() {
  const e = this._run;
  return n`
      ${this._scanError ? n`<p class="error" role="alert">${this._scanError}</p>` : l}
      ${e?.blocked ? _(this, y, de).call(this, "Pages can't be loaded for scanning", n`
        <p>${e.blocked.reason ?? e.blocked.message}</p>
        <p>The scan loads each page in a frame on this site and runs axe-core in it, so the site must allow same-origin framing
        (<code>X-Frame-Options: SAMEORIGIN</code> or <code>Content-Security-Policy: frame-ancestors 'self'</code>, not DENY / 'none')
        and scripts from its own origin (<code>script-src 'self'</code>).</p>`) : l}
      ${e?.paused ? _(this, y, de).call(this, "The scan was paused", n`
        <p>Results couldn't be saved (${e.paused.message}). Everything up to now is kept; use “Resume last scan” to continue.</p>`) : l}
      ${e?.mostlyFailed ? _(this, y, de).call(this, "Most pages couldn't be scanned", n`
        <p>The scan was saved as incomplete so it doesn't replace your last full report. Check that the site is up, then use “Resume last scan” or scan again. Failed pages and reasons are listed in each page result.</p>`) : l}
      ${e?.otherOrigins.length ? n`<p class="muted">${Ai(e.otherOrigins, location.origin)}</p>` : l}`;
};
N.styles = [ie, X`
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
    .ring { position: relative; margin: 0; width: ${T.size}px; display: grid; justify-items: center; gap: var(--uui-size-space-3); }
    .ring svg { display: block; filter: drop-shadow(0 8px 18px color-mix(in srgb, var(--tone) 30%, transparent)); }
    .ring .track { stroke: color-mix(in srgb, var(--tone) 24%, var(--uui-color-surface)); }
    .ring .fill.empty { display: none; }
    .stop-a { stop-color: var(--ring-a, var(--tone)); }
    .stop-b { stop-color: var(--ring-b, var(--tone)); }
    .ring-text { position: absolute; top: 0; height: ${T.size}px; display: grid; place-content: center; text-align: center; gap: 2px; }
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
W([
  L({ attribute: !1 })
], N.prototype, "siteKey", 2);
W([
  L({ attribute: !1 })
], N.prototype, "settings", 2);
W([
  p()
], N.prototype, "_overview", 2);
W([
  p()
], N.prototype, "_error", 2);
W([
  p()
], N.prototype, "_progress", 2);
W([
  p()
], N.prototype, "_run", 2);
W([
  p()
], N.prototype, "_scanError", 2);
N = W([
  ee("esatto-a11y-overview")
], N);
var Fi = Object.defineProperty, Wi = Object.getOwnPropertyDescriptor, $t = (e) => {
  throw TypeError(e);
}, K = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? Wi(t, i) : t, r = e.length - 1, o; r >= 0; r--)
    (o = e[r]) && (a = (s ? o(t, i, a) : o(a)) || a);
  return s && a && Fi(t, i, a), a;
}, Bi = (e, t, i) => t.has(e) || $t("Cannot " + i), Vi = (e, t, i) => t.has(e) ? $t("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), z = (e, t, i) => (Bi(e, t, "access private method"), i), x, le, Se, Oe, xt, _t, kt, Ct, St;
const qi = "/App_Plugins/Esatto.Umbraco.Backoffice.Accessibility/axe.min.js", Gi = 1024;
let O = class extends te {
  constructor() {
    super(...arguments), Vi(this, x), this.siteKey = "", this.settings = null, this._pending = null, this._note = "", this._noteError = !1, this._rechecking = null, this._message = null;
  }
  firstUpdated() {
    this.renderRoot.querySelector("h3")?.focus();
  }
  render() {
    const e = this.group;
    return n`
      <div class="page">
        <div class="head">
          <uui-button look="secondary" compact label="Back to all issues" @click=${() => this.dispatchEvent(new CustomEvent("back"))}>
            <uui-icon name="icon-arrow-left" aria-hidden="true"></uui-icon>&nbsp;Back to all issues
          </uui-button>
          <h3 tabindex="-1">${e.title}</h3>
          <ul class="meta" aria-label="About this issue">
            ${e.criteria.length ? n`<li class="tag tone-notice">WCAG ${e.criteria.join(", ")}</li><li class="tag">Level ${ot(e.level)}</li>` : n`<li class="tag">Best practice</li>`}
            ${e.kind === "incomplete" ? n`<li class="tag tone-warning">Needs review</li>` : l}
            ${e.impact ? n`<li class="tag ${xe[e.impact] ?? ""}">Impact: ${e.impact}</li>` : l}
            <li class="chip"><uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${e.pageCount} ${e.pageCount === 1 ? "page" : "pages"} (${e.elementCount} ${e.elementCount === 1 ? "element" : "elements"})</li>
            <li class="chip"><uui-icon name=${ct[e.owner]} aria-hidden="true"></uui-icon>Who fixes: ${e.owner}</li>
          </ul>
        </div>
        ${this._message ? n`
          <div role="status" class="banner compact tone-good">
            <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span>
            <div><p>${this._message}</p></div>
          </div>` : l}

        <div class="explain">
          <section class="note"><h4><uui-icon name="icon-info" aria-hidden="true"></uui-icon>Why it matters</h4><p>${e.why}</p></section>
          <section class="note fix"><h4><uui-icon name="icon-wand" aria-hidden="true"></uui-icon>How to fix</h4><p>${e.howToFix}</p></section>
          ${e.caveats ? n`<section class="note"><h4><uui-icon name="icon-lightbulb-active" aria-hidden="true"></uui-icon>Caveats</h4><p>${e.caveats}</p></section>` : l}
        </div>
        ${e.kind === "incomplete" ? n`<p class="muted">The scanner couldn't decide these automatically. Check each one by hand; mark it as a false positive if it's fine.</p>` : l}
        ${ne(e.learnMoreUrl) ? n`<p><a class="learn" href=${ne(e.learnMoreUrl)} target="_blank" rel="noopener">${e.criteria.length ? `Read W3C's “Understanding ${e.criteria[0]}”` : "Read more about this rule"}<span class="visually-hidden"> (opens in a new tab)</span><uui-icon name="icon-arrow-right" aria-hidden="true"></uui-icon></a></p>` : l}

        <section aria-labelledby="where-heading">
          <h4 id="where-heading" class="section-label">Where it occurs</h4>
          <div class="occurrences">${e.pages.map((t) => z(this, x, Ct).call(this, t))}</div>
        </section>
        <div id="frames" aria-hidden="true"></div>
      </div>
      ${z(this, x, St).call(this)}`;
  }
};
x = /* @__PURE__ */ new WeakSet();
le = function() {
  this.dispatchEvent(new CustomEvent("changed", { bubbles: !0, composed: !0 }));
};
Se = function(e, t) {
  this._pending = { page: e, node: t }, this._note = "", this._noteError = !1, this.updateComplete.then(() => this.renderRoot.querySelector("dialog")?.showModal());
};
Oe = function() {
  this.renderRoot.querySelector("dialog")?.close(), this._pending = null;
};
xt = async function() {
  if (!this._pending) return;
  if (!this._note.trim()) {
    this._noteError = !0, this.renderRoot.querySelector("#fp-note")?.focus();
    return;
  }
  const { page: e, node: t } = this._pending, { error: i } = await C(this, pi(this.siteKey, {
    ruleId: this.group.ruleId,
    contentKey: e.contentKey,
    selector: t?.selector ?? null,
    note: this._note.trim()
  }));
  i || (z(this, x, Oe).call(this), this._message = "Marked as a false positive.", z(this, x, le).call(this));
};
_t = async function(e) {
  const { error: t } = await C(this, hi(e));
  t || (this._message = "False positive removed.", z(this, x, le).call(this));
};
kt = async function(e) {
  if (!this.settings) return;
  const t = this.settings.settings;
  this._rechecking = e.contentKey, this._message = null;
  const i = this.renderRoot.querySelector("#frames");
  for (const s of Ni(e)) {
    const a = await dt(s, i, { axeUrl: qi, tags: nt(t.version, t.level, t.scan.bestPractice), settleMs: 1e3, timeoutMs: 3e4 });
    if (a.kind !== "ok" || !a.result) {
      this._message = `Re-check failed at ${s.viewport} px: ${a.reason ?? a.kind}.`, this._rechecking = null, z(this, x, le).call(this);
      return;
    }
    const { error: r } = await C(this, ri(this.siteKey, a.result));
    if (r) {
      this._rechecking = null;
      return;
    }
  }
  this._rechecking = null, this._message = `${e.name} was re-checked.`, z(this, x, le).call(this);
};
Ct = function(e) {
  const t = this._rechecking === e.contentKey, i = ne(e.url) ? new URL(e.url).pathname : "";
  return n`
      <article class="card occurrence" aria-label=${e.name}>
        <div class="card-head">
          <span class="icon-badge tone-none" aria-hidden="true"><uui-icon name="icon-document"></uui-icon></span>
          <h4>${e.name}</h4>
          ${i ? n`<span class="chip url">${i}</span>` : l}
          <span class="chip end">${e.nodes.length} ${e.nodes.length === 1 ? "element" : "elements"}</span>
        </div>
        <div class="row actions">
          <a class="action" href=${bi(e.contentKey, e.culture)}><uui-icon name="icon-edit" aria-hidden="true"></uui-icon>Edit page in Umbraco</a>
          ${ne(e.url) ? n`<a class="action" href=${ne(e.url)} target="_blank" rel="noopener"><uui-icon name="icon-out" aria-hidden="true"></uui-icon>View on page<span class="visually-hidden"> (opens in a new tab)</span></a>` : l}
          <uui-button compact look="secondary" label="Re-check this page" ?disabled=${t} @click=${() => z(this, x, kt).call(this, e)}>${t ? "Re-checking…" : "Re-check this page"}</uui-button>
          ${e.nodes.some((s) => s.falsePositiveId) ? l : n`<uui-button compact look="secondary" label="Mark all on this page as false positive" @click=${() => z(this, x, Se).call(this, e, null)}></uui-button>`}
        </div>
        <ul class="nodes">
          ${e.nodes.map((s) => n`<li>
            <pre>${s.html}</pre>
            <p class="muted small">Selector: <code>${s.selector}</code></p>
            <div class="row">
              ${s.mediaKey ? n`<a class="action" href=${yi(s.mediaKey)}><uui-icon name="icon-picture" aria-hidden="true"></uui-icon>Edit image in Umbraco</a>` : l}
              ${s.falsePositiveId ? n`<uui-button compact look="secondary" label="Undo false positive" @click=${() => z(this, x, _t).call(this, s.falsePositiveId)}></uui-button>` : n`<uui-button compact look="secondary" label="Mark as false positive" @click=${() => z(this, x, Se).call(this, e, s)}></uui-button>`}
            </div>
          </li>`)}
        </ul>
      </article>`;
};
St = function() {
  const e = this._pending;
  return n`
      <dialog aria-labelledby="fp-title" @close=${() => this._pending = null}>
        ${e ? n`
          <form method="dialog" @submit=${(t) => {
    t.preventDefault(), z(this, x, xt).call(this);
  }}>
            <h3 id="fp-title">Mark as false positive</h3>
            <p>${e.node ? n`This element on <strong>${e.page.name}</strong>` : n`Every “${this.group.title}” finding on <strong>${e.page.name}</strong>`} will be hidden from the report and the score, now and in later scans.</p>
            <label for="fp-note">Why is this not a problem? (required)</label>
            <textarea id="fp-note" rows="3" maxlength=${Gi} aria-required="true" aria-invalid=${this._noteError ? "true" : "false"} aria-describedby=${this._noteError ? "fp-error" : l}
              .value=${this._note} @input=${(t) => {
    this._note = t.target.value, this._noteError = !1;
  }}></textarea>
            ${this._noteError ? n`<p id="fp-error" class="error">Write a short reason, so others know why.</p>` : l}
            <div class="row">
              <uui-button type="submit" look="primary" label="Mark as false positive"></uui-button>
              <uui-button look="secondary" label="Cancel" @click=${z(this, x, Oe)}></uui-button>
            </div>
          </form>` : l}
      </dialog>`;
};
O.styles = [ie, X`
    .head { display: grid; gap: var(--uui-size-space-3); justify-items: start; }
    .head h3 { font-size: 24px; font-weight: 800; letter-spacing: -0.01em; line-height: 1.25; }
    h3:focus, h4:focus { outline: none; }
    h3:focus-visible { outline: 2px solid var(--uui-color-focus); }
    .meta { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }

    .explain { display: grid; gap: var(--uui-size-space-3); grid-template-columns: repeat(auto-fit, minmax(min(100%, 18rem), 1fr)); }
    .note { display: grid; gap: var(--uui-size-space-2); align-content: start; padding: var(--uui-size-space-4); border-radius: var(--a11y-radius-sm); background: var(--a11y-wash); border: 1px solid var(--a11y-line); }
    .note.fix { background: color-mix(in srgb, var(--a11y-good) 7%, var(--uui-color-surface)); border-color: color-mix(in srgb, var(--a11y-good) 30%, var(--a11y-line)); }
    .note h4 { display: flex; gap: 6px; align-items: center; font-size: 13px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
    .note h4 uui-icon { color: var(--a11y-muted); }
    .learn, .action { display: inline-flex; gap: 6px; align-items: center; font-weight: 600; }

    .occurrences { display: grid; gap: var(--uui-size-space-3); }
    .occurrence .card-head h4 { font-size: 15px; font-weight: 700; }
    .actions { margin-bottom: var(--uui-size-space-4); }
    .nodes { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-4); }
    .nodes li { display: grid; gap: var(--uui-size-space-2); border-top: 1px solid var(--a11y-line); padding-top: var(--uui-size-space-4); }

    dialog {
      border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius); box-shadow: var(--a11y-shadow-raised);
      padding: var(--uui-size-space-6); max-width: 36rem; width: calc(100% - 2rem); background: var(--uui-color-surface); color: var(--uui-color-text);
    }
    dialog::backdrop { background: rgb(0 0 0 / 0.45); }
    dialog form { display: grid; gap: var(--uui-size-space-3); }
    dialog h3 { font-size: 18px; font-weight: 700; }
    dialog label { font-size: 13px; font-weight: 600; }
    textarea[aria-invalid='true'] { border-color: var(--uui-color-danger); }
    #frames { position: relative; height: 0; overflow: hidden; }
  `];
K([
  L({ attribute: !1 })
], O.prototype, "group", 2);
K([
  L({ attribute: !1 })
], O.prototype, "siteKey", 2);
K([
  L({ attribute: !1 })
], O.prototype, "settings", 2);
K([
  p()
], O.prototype, "_pending", 2);
K([
  p()
], O.prototype, "_note", 2);
K([
  p()
], O.prototype, "_noteError", 2);
K([
  p()
], O.prototype, "_rechecking", 2);
K([
  p()
], O.prototype, "_message", 2);
O = K([
  ee("esatto-a11y-issue-detail")
], O);
var ji = Object.defineProperty, Hi = Object.getOwnPropertyDescriptor, Et = (e) => {
  throw TypeError(e);
}, D = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? Hi(t, i) : t, r = e.length - 1, o; r >= 0; r--)
    (o = e[r]) && (a = (s ? o(t, i, a) : o(a)) || a);
  return s && a && ji(t, i, a), a;
}, Re = (e, t, i) => t.has(e) || Et("Cannot " + i), Ze = (e, t, i) => (Re(e, t, "read from private field"), i ? i.call(e) : t.get(e)), et = (e, t, i) => t.has(e) ? Et("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), zt = (e, t, i, s) => (Re(e, t, "write to private field"), t.set(e, i), i), w = (e, t, i) => (Re(e, t, "access private method"), i), Q, g, he, Ie, q, At, Pt, Tt, Ot, Rt;
const Xi = ["Perceivable", "Operable", "Understandable", "Robust"], Ji = ["critical", "serious", "moderate", "minor"], Ne = (e) => `${e.ruleId}|${e.kind}`;
let R = class extends te {
  constructor() {
    super(...arguments), et(this, g), this.siteKey = "", this.filter = {}, this.settings = null, this._filter = {}, this._groups = null, this._selected = null, this._detail = null, this._error = null, et(this, Q, null);
  }
  willUpdate(e) {
    e.has("filter") && (this._filter = { ...this.filter });
  }
  updated(e) {
    (e.has("siteKey") || e.has("filter")) && this.siteKey && w(this, g, he).call(this), e.has("_selected") && this._selected === null && Ze(this, Q) && (this.renderRoot.querySelector(`[data-group="${CSS.escape(Ze(this, Q))}"]`)?.focus(), zt(this, Q, null));
  }
  render() {
    if (this._error) return n`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (this._selected && !this._detail) return n`<uui-loader></uui-loader>`;
    const e = this._detail;
    return this._selected && e ? n`<esatto-a11y-issue-detail .group=${e} .siteKey=${this.siteKey} .settings=${this.settings}
        @back=${w(this, g, Tt)} @changed=${() => w(this, g, he).call(this)}></esatto-a11y-issue-detail>` : n`
      <div class="page">
        ${w(this, g, Ot).call(this)}
        ${this._groups === null ? n`<uui-loader></uui-loader>` : w(this, g, Rt).call(this, this._groups)}
      </div>`;
  }
};
Q = /* @__PURE__ */ new WeakMap();
g = /* @__PURE__ */ new WeakSet();
he = async function() {
  const { data: e, error: t } = await C(this, st(this.siteKey, this._filter), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load issues.";
    return;
  }
  this._error = null, this._groups = e, this._selected && !e.some((i) => Ne(i) === this._selected) && (this._selected = null), this._selected && await w(this, g, Ie).call(this);
};
Ie = async function() {
  const [e, t] = this._selected.split("|"), { data: i } = await C(this, st(this.siteKey, { ...this._filter, ruleId: e, kind: t }), { disableNotifications: !0 });
  this._detail = i?.[0] ?? null, this._detail || (this._selected = null);
};
q = function(e) {
  this._filter = { ...this._filter, ...e }, this._selected = null, w(this, g, he).call(this);
};
At = async function() {
  const { data: e, error: t } = await C(this, li(this.siteKey, this._filter));
  if (t || !e) return;
  const i = URL.createObjectURL(e), s = document.createElement("a");
  s.href = i, s.download = `accessibility-issues-${(/* @__PURE__ */ new Date()).toISOString().slice(0, 10)}.csv`, s.click(), setTimeout(() => URL.revokeObjectURL(i), 1e3);
};
Pt = async function(e) {
  this._selected = Ne(e), this._detail = null, await w(this, g, Ie).call(this);
};
Tt = function() {
  zt(this, Q, this._selected), this._selected = null, this._detail = null;
};
Ot = function() {
  const e = this._filter, t = (i, s, a, r, o) => n`
      <div class="field">
        <label for=${i}>${s}</label>
        <select id=${i} @change=${(c) => o(c.target.value)}>
          <option value="" ?selected=${!a}>All</option>
          ${r.map(([c, A]) => n`<option value=${c} ?selected=${a === c}>${A}</option>`)}
        </select>
      </div>`;
  return n`
      <form class="filters card" @submit=${(i) => i.preventDefault()} aria-label="Filter issues">
        ${t(
    "f-principle",
    "Principle",
    e.principle ? String(e.principle) : void 0,
    Xi.map((i, s) => [String(s + 1), `${s + 1}. ${i}`]),
    (i) => w(this, g, q).call(this, { principle: i ? Number(i) : void 0 })
  )}
        ${t("f-level", "Level", e.level, [["A", "A"], ["AA", "AA"], ["AAA", "AAA"]], (i) => w(this, g, q).call(this, { level: i || void 0 }))}
        ${t("f-owner", "Who fixes it", e.owner, [["Editor", "Editors"], ["Developer", "Developers"], ["Both", "Both"]], (i) => w(this, g, q).call(this, { owner: i || void 0 }))}
        ${t("f-impact", "Impact", e.impact, Ji.map((i) => [i, i]), (i) => w(this, g, q).call(this, { impact: i || void 0 }))}
        <div class="field checkbox">
          <input id="f-fp" type="checkbox" ?checked=${!!e.falsePositives} @change=${(i) => w(this, g, q).call(this, { falsePositives: i.target.checked })}>
          <label for="f-fp">Show false positives only</label>
        </div>
        ${e.criterion ? n`<div class="criterion"><span class="tag tone-notice">Criterion ${e.criterion}</span>
          <uui-button compact look="secondary" label="Clear criterion filter" @click=${() => w(this, g, q).call(this, { criterion: void 0 })}></uui-button></div>` : l}
        <div class="filter-actions">
          <uui-button look="outline" label="Export CSV" @click=${w(this, g, At)}><uui-icon name="icon-download-alt" aria-hidden="true"></uui-icon>&nbsp;Export CSV</uui-button>
        </div>
      </form>`;
};
Rt = function(e) {
  return e.length ? n`
      <section class="card" aria-labelledby="issues-heading">
        <div class="card-head">
          <span class="icon-badge tone-critical" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
          <h3 id="issues-heading">Issues</h3>
          <span class="chip end" role="status">${e.length} ${e.length === 1 ? "issue" : "issues"}</span>
        </div>
        <div class="table-wrap">
          <table>
            <caption class="visually-hidden">Issues, most severe first</caption>
            <thead><tr>
              <th scope="col">Issue</th><th scope="col">Criteria</th><th scope="col">Level</th>
              <th scope="col" class="num">Pages</th><th scope="col" class="num">Elements</th><th scope="col">Who fixes</th><th scope="col">Impact</th>
            </tr></thead>
            <tbody>
              ${e.map((t) => n`<tr class=${t.impact && xe[t.impact] || ""}>
                <td class="issue">
                  <button class="link" data-group=${Ne(t)} @click=${() => w(this, g, Pt).call(this, t)}>${t.title}</button>
                  ${t.kind === "incomplete" ? n`<span class="tag tone-warning">Needs review</span>` : l}
                  ${t.bestPractice ? n`<span class="tag">Best practice</span>` : l}
                </td>
                <td>${t.criteria.join(", ") || "–"}</td>
                <td><span class="tag">${ot(t.level)}</span></td>
                <td class="num">${t.pageCount}</td>
                <td class="num">${t.elementCount}</td>
                <td><span class="chip"><uui-icon name=${ct[t.owner]} aria-hidden="true"></uui-icon>${t.owner}</span></td>
                <td>${t.impact ? n`<span class="tag ${xe[t.impact] ?? ""}">${t.impact}</span>` : n`<span class="muted">–</span>`}</td>
              </tr>`)}
            </tbody>
          </table>
        </div>
      </section>` : n`
        <div class="card empty">
          <span class="icon-badge solid round tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span>
          <p>${this._filter.falsePositives ? "No false positives marked." : "No issues match. Either the site passes the automated checks for these filters, or it hasn't been scanned yet."}</p>
        </div>`;
};
R.styles = [ie, X`
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: flex-end; padding: var(--uui-size-space-4) var(--uui-size-space-5); }
    .field.checkbox { display: flex; align-items: center; gap: var(--uui-size-space-2); min-height: 40px; } /* = the selects, so they share a centre line */
    .field.checkbox input { width: 20px; height: 20px; margin: 0; }
    .criterion { display: flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; }
    .filter-actions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); margin-left: auto; }
    .empty { display: flex; gap: var(--uui-size-space-3); align-items: center; }
    /* The row's impact as a coloured edge on its first cell. */
    tbody td:first-child { box-shadow: inset 3px 0 0 var(--tone, transparent); }
    .issue .tag { margin-left: 6px; vertical-align: middle; }
    button.link {
      background: none; border: 0; padding: 4px 0; font: inherit; font-weight: 600; color: var(--uui-color-interactive);
      text-decoration: underline; text-underline-offset: 2px; cursor: pointer; text-align: left; min-height: 24px;
    }
    button.link:hover { color: var(--uui-color-interactive-emphasis); }
  `];
D([
  L({ attribute: !1 })
], R.prototype, "siteKey", 2);
D([
  L({ attribute: !1 })
], R.prototype, "filter", 2);
D([
  L({ attribute: !1 })
], R.prototype, "settings", 2);
D([
  p()
], R.prototype, "_filter", 2);
D([
  p()
], R.prototype, "_groups", 2);
D([
  p()
], R.prototype, "_selected", 2);
D([
  p()
], R.prototype, "_detail", 2);
D([
  p()
], R.prototype, "_error", 2);
R = D([
  ee("esatto-a11y-issues")
], R);
var Yi = Object.defineProperty, Qi = Object.getOwnPropertyDescriptor, It = (e) => {
  throw TypeError(e);
}, B = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? Qi(t, i) : t, r = e.length - 1, o; r >= 0; r--)
    (o = e[r]) && (a = (s ? o(t, i, a) : o(a)) || a);
  return s && a && Yi(t, i, a), a;
}, Zi = (e, t, i) => t.has(e) || It("Cannot " + i), es = (e, t, i) => t.has(e) ? It("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), k = (e, t, i) => (Zi(e, t, "access private method"), i), $, Me, Ee, Nt, Mt, Le, ge, Ue, Lt;
const Ut = [
  { id: "all", label: "All", test: () => !0 },
  { id: "unchecked", label: "Not checked", test: (e) => !e.status },
  { id: "scanner", label: "Not checked, scanner found nothing", test: (e) => !e.status && e.scannerChecked },
  { id: "failed", label: "Failed", test: (e) => e.status === "fail" },
  { id: "recheck", label: "Needs re-check", test: (e) => e.needsRecheck }
], ts = 2048, re = (e) => `c-${e.criterion.id.split(".").join("-")}`, Kt = [
  { id: "pass", label: "Pass" },
  { id: "fail", label: "Fail" },
  { id: "na", label: "Not applicable" }
], is = { pass: "tone-good", fail: "tone-critical", na: "tone-none" };
let M = class extends te {
  constructor() {
    super(...arguments), es(this, $), this.siteKey = "", this._items = null, this._view = "all", this._error = null, this._saved = null, this._confirmed = null, this._drafts = {};
  }
  updated(e) {
    e.has("siteKey") && this.siteKey && k(this, $, Me).call(this);
  }
  render() {
    if (this._error) return n`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._items) return n`<uui-loader></uui-loader>`;
    const e = this._items, t = e.filter((s) => s.status).length, i = k(this, $, ge).call(this);
    return n`
      <div class="page">
        <section class="card overview" aria-label="Manual checklist">
          <div class="progress-head">
            <span class="icon-badge tone-good" aria-hidden="true"><uui-icon name="icon-checkbox"></uui-icon></span>
            <div class="grow">
              <p class="summary" tabindex="-1"><strong>${t} of ${e.length}</strong> checked</p>
              <div class="progress-bar" aria-hidden="true"><span style=${pe({ width: `${e.length ? Math.round(t / e.length * 100) : 0}%` })}></span></div>
            </div>
          </div>
          <fieldset class="views">
            <legend class="visually-hidden">Show</legend>
            ${Ut.map((s) => n`
              <label class="view"><input type="radio" name="view" .checked=${this._view === s.id} @change=${() => this._view = s.id}>
                ${s.label} <span class="count">${e.filter(s.test).length}</span></label>`)}
          </fieldset>
          <p class="muted">These criteria need a person to judge them. Follow the steps, then record the result. “Checked by the scanner” means its automated rules found nothing wrong, so those are usually quick to confirm. Results become “needs re-check” after the interval set in Settings.</p>
        </section>
        <p role="status" class="visually-hidden">${this._saved ?? ""}</p>
        ${i.length ? n`<ul class="items">${Xt(i, (s) => s.criterion.id, (s) => k(this, $, Lt).call(this, s))}</ul>` : n`<div class="card empty"><span class="icon-badge solid round tone-good" aria-hidden="true"><uui-icon name="icon-check"></uui-icon></span><p>Nothing here.</p></div>`}
      </div>`;
  }
};
$ = /* @__PURE__ */ new WeakSet();
Me = async function() {
  const { data: e, error: t } = await C(this, ci(this.siteKey), { disableNotifications: !0 });
  if (t || !e) {
    this._error = "Could not load the checklist.";
    return;
  }
  this._error = null, this._items = e;
};
Ee = async function(e, t, i) {
  if (!t) return;
  const { error: s } = await C(this, di(this.siteKey, e.criterion.id, t, i));
  s || (k(this, $, Le).call(this, e.criterion.id), this._confirmed = { id: e.criterion.id, text: "Saved" }, await k(this, $, Ue).call(this, e, `${e.criterion.id} saved as ${Kt.find((a) => a.id === t).label}.`));
};
Nt = async function(e) {
  const { error: t } = await C(this, ui(this.siteKey, e.criterion.id));
  t || (this._confirmed = { id: e.criterion.id, text: "Cleared" }, await k(this, $, Ue).call(this, e, `${e.criterion.id} cleared; it's not checked any more.`));
};
Mt = function(e, t) {
  const i = t.trim();
  i === (e.note ?? "") ? k(this, $, Le).call(this, e.criterion.id) : this._drafts = { ...this._drafts, [e.criterion.id]: i };
};
Le = function(e) {
  if (!(e in this._drafts)) return;
  const { [e]: t, ...i } = this._drafts;
  this._drafts = i;
};
ge = function() {
  return (this._items ?? []).filter(Ut.find((e) => e.id === this._view).test);
};
Ue = async function(e, t) {
  const i = k(this, $, ge).call(this).findIndex((c) => c.criterion.id === e.criterion.id);
  if (this._saved = t, await k(this, $, Me).call(this), await this.updateComplete, this.renderRoot.activeElement) return;
  const s = k(this, $, ge).call(this), a = s.find((c) => c.criterion.id === e.criterion.id), r = a ?? s[Math.min(i, s.length - 1)];
  (a ? this.renderRoot.querySelector(`[aria-labelledby="${re(a)}"] button`) : r ? this.renderRoot.querySelector(`#${re(r)}`) : this.renderRoot.querySelector(".summary"))?.focus();
};
Lt = function(e) {
  const t = e.criterion, i = t.id in this._drafts, s = `${re(e)}-note-hint`;
  return n`
      <li>
        <article class="card item ${e.status ? is[e.status] : ""}">
          <div class="title-row">
            <h3 id=${re(e)} tabindex="-1">${t.id} ${t.title}</h3>
            <span class="tag">Level ${t.level}</span>
            ${t.since !== "V20" ? n`<span class="tag tone-notice">New in ${rt(t.since)}</span>` : l}
            ${e.needsRecheck ? n`<span class="tag tone-warning">Needs re-check</span>` : l}
            ${e.scannerChecked ? n`<span class="chip"><uui-icon name="icon-wand" aria-hidden="true"></uui-icon>Checked by the scanner</span>` : l}
          </div>
          <p><strong>How to test:</strong> ${t.howToTest}</p>
          ${e.automatedFindings ? n`<p>
            <a href="#" @click=${(a) => {
    a.preventDefault(), ye(this, "issues", { criterion: t.id });
  }}>
              The scanner found ${e.automatedFindings} ${e.automatedFindings === 1 ? "thing" : "things"} to look at</a></p>` : l}
          ${e.documentLinks ? n`<p class="muted">This site links ${e.documentLinks} ${e.documentLinks === 1 ? "document" : "documents"} (PDF/Office); they must be checked separately.</p>` : l}
          <details class="disclosure">
            <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>Why it matters and how to fix</summary>
            <div class="why">
              <p>${t.why}</p><p>${t.howToFix}</p><p class="muted">${t.caveats}</p>
              <p><a href=${t.understandingUrl} target="_blank" rel="noopener">Understanding ${t.id} (W3C)<span class="visually-hidden"> (opens in a new tab)</span></a></p>
            </div>
          </details>
          <div class="row" role="group" aria-labelledby=${re(e)}>
            ${Kt.map((a) => n`
              <button class="status ${a.id}" aria-pressed=${e.status === a.id ? "true" : "false"}
                @click=${() => k(this, $, Ee).call(this, e, a.id, i ? this._drafts[t.id] || null : e.note)}>${e.status === a.id ? n`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>` : l}${a.label}</button>`)}
            ${e.status ? n`<button class="clear" @click=${() => k(this, $, Nt).call(this, e)}>Clear result</button>` : l}
            ${e.updatedUtc ? n`<span class="muted small">${e.userName}, ${ze(e.updatedUtc, !1)}</span>` : l}
            ${this._confirmed?.id === t.id ? n`<span class="confirmed" aria-hidden="true"><uui-icon name="icon-check"></uui-icon> ${this._confirmed.text}</span>` : l}
          </div>
          <label class="note">
            <span>Note${e.status ? "" : " (saved with a result)"}</span>
            <textarea rows="2" maxlength=${ts} .value=${e.note ?? ""} aria-describedby=${i ? s : l}
              @input=${(a) => k(this, $, Mt).call(this, e, a.target.value)}
              @blur=${() => {
    e.status && i && k(this, $, Ee).call(this, e, e.status, this._drafts[t.id] || null);
  }}></textarea>
          </label>
          ${i ? n`
            <div id=${s} class="banner compact tone-warning unsaved">
              <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
              <div><p>${e.status ? "Not saved yet. It saves when you leave the note field." : "Not saved yet. Choose Pass, Fail or Not applicable to save it with the note."}</p></div>
            </div>` : l}
        </article>
      </li>`;
};
M.styles = [ie, X`
    .overview { display: grid; gap: var(--uui-size-space-4); }
    .progress-head { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    .progress-head .grow { flex: 1; display: grid; gap: var(--uui-size-space-2); max-width: 32rem; }
    .summary { font-size: 16px; }
    .summary strong { font-size: 22px; font-weight: 800; letter-spacing: -0.01em; font-variant-numeric: tabular-nums; }
    h3:focus, .summary:focus { outline: none; }
    h3:focus-visible, .summary:focus-visible { outline: 2px solid var(--uui-color-focus); }

    .views { border: 0; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); }
    .view {
      display: inline-flex; align-items: center; gap: 8px; min-height: 40px; padding: 0 var(--uui-size-space-3) 0 var(--uui-size-space-3);
      border: 1px solid var(--a11y-line); border-radius: 999px; background: var(--uui-color-surface); cursor: pointer; font-weight: 600;
      transition: border-color var(--a11y-ease), background-color var(--a11y-ease);
    }
    .view:hover { border-color: var(--uui-color-border-emphasis); }
    .view:has(input:checked) { border-color: var(--uui-color-selected); background: color-mix(in srgb, var(--uui-color-selected) 10%, var(--uui-color-surface)); }
    .view:has(input:focus-visible) { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    .view input { width: 16px; height: 16px; margin: 0; }
    .count { min-width: 1.5em; padding: 0 6px; border-radius: 999px; background: var(--a11y-wash); border: 1px solid var(--a11y-line); font-size: 12px; text-align: center; font-variant-numeric: tabular-nums; }

    .empty { display: flex; gap: var(--uui-size-space-3); align-items: center; }
    .items { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
    /* The recorded result as the card's edge: green pass, red fail, grey not applicable; none while unchecked. */
    .item { display: grid; gap: var(--uui-size-space-3); box-shadow: inset 4px 0 0 var(--tone, transparent), var(--a11y-shadow); }
    .title-row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); align-items: center; }
    .title-row h3 { font-size: 16px; font-weight: 700; margin-right: var(--uui-size-space-2); }
    .why { display: grid; gap: var(--uui-size-space-2); margin-top: var(--uui-size-space-3); max-width: 60rem; padding: var(--uui-size-space-4); background: var(--a11y-wash); border: 1px solid var(--a11y-line); border-radius: var(--a11y-radius-sm); }

    button.status {
      display: inline-flex; gap: 6px; align-items: center; min-height: 44px; min-width: 44px; padding: 0 var(--uui-size-space-5);
      font: inherit; font-weight: 600; cursor: pointer; border: 1px solid var(--uui-color-border-standalone); border-radius: 999px;
      background: var(--uui-color-surface); color: var(--uui-color-text);
      transition: background-color var(--a11y-ease), border-color var(--a11y-ease), box-shadow var(--a11y-ease);
    }
    button.status.pass { --tone: var(--a11y-good); --tone-on: var(--a11y-good-on); }
    button.status.fail { --tone: var(--a11y-critical); --tone-on: var(--a11y-critical-on); }
    button.status.na { --tone: var(--a11y-neutral); --tone-on: var(--a11y-neutral-on); }
    button.status:hover { border-color: var(--tone); background: color-mix(in srgb, var(--tone) 8%, var(--uui-color-surface)); }
    button.status[aria-pressed='true'] { background: var(--tone); color: var(--tone-on); border-color: var(--tone); box-shadow: 0 4px 12px -6px var(--tone); }
    button.clear {
      min-height: 44px; padding: 0 var(--uui-size-space-3); font: inherit; cursor: pointer; background: none; border: 0;
      color: var(--uui-color-interactive); text-decoration: underline; text-underline-offset: 2px;
    }
    button.clear:hover { color: var(--uui-color-interactive-emphasis); }
    .confirmed { display: inline-flex; align-items: center; gap: 4px; font-weight: 600; }
    .confirmed uui-icon { color: var(--a11y-good); }
    .note { display: grid; gap: 6px; max-width: 40rem; }
    .note > span { font-size: 13px; font-weight: 600; }
    .unsaved { max-width: 40rem; }
  `];
B([
  L({ attribute: !1 })
], M.prototype, "siteKey", 2);
B([
  p()
], M.prototype, "_items", 2);
B([
  p()
], M.prototype, "_view", 2);
B([
  p()
], M.prototype, "_error", 2);
B([
  p()
], M.prototype, "_saved", 2);
B([
  p()
], M.prototype, "_confirmed", 2);
B([
  p()
], M.prototype, "_drafts", 2);
M = B([
  ee("esatto-a11y-checklist")
], M);
var ss = Object.defineProperty, as = Object.getOwnPropertyDescriptor, Dt = (e) => {
  throw TypeError(e);
}, se = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? as(t, i) : t, r = e.length - 1, o; r >= 0; r--)
    (o = e[r]) && (a = (s ? o(t, i, a) : o(a)) || a);
  return s && a && ss(t, i, a), a;
}, Ke = (e, t, i) => t.has(e) || Dt("Cannot " + i), ns = (e, t, i) => (Ke(e, t, "read from private field"), t.get(e)), tt = (e, t, i) => t.has(e) ? Dt("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), rs = (e, t, i, s) => (Ke(e, t, "write to private field"), t.set(e, i), i), u = (e, t, i) => (Ke(e, t, "access private method"), i), fe, d, H, E, Ft, Wt, Z, G, ue;
const os = {
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
}, ls = [
  { key: "largerText", label: "Larger text" },
  { key: "spacing", label: "Bigger buttons and more spacing" },
  { key: "highContrast", label: "High contrast" },
  { key: "reduceMotion", label: "Reduce motion" },
  { key: "readableFont", label: "Readable font" },
  { key: "highlightLinks", label: "Highlight links" },
  { key: "readingGuide", label: "Reading guide" }
], Bt = (e) => JSON.parse(JSON.stringify(e));
let F = class extends te {
  constructor() {
    super(), tt(this, d), this.settings = null, this._draft = null, this._saving = !1, this._cultures = ["en", "sv"], this._newCulture = "", tt(this, fe), this.consumeContext(Jt, (e) => rs(this, fe, e));
  }
  willUpdate(e) {
    e.has("settings") && this.settings && (this._draft = Bt(this.settings.settings), this._cultures = [.../* @__PURE__ */ new Set(["en", "sv", ...Object.keys(this._draft.visitor.panel.texts ?? {})])]);
  }
  render() {
    const e = this._draft;
    if (!e) return n`<uui-loader></uui-loader>`;
    const t = e.visitor.panel;
    return n`
      <form class="page" @submit=${u(this, d, Ft)}>
        ${this.settings?.locked.length ? n`<p class="muted">Fields marked “Set in configuration” come from appsettings (<code>Esatto:Accessibility</code>) and can't be changed here.</p>` : l}

        <div class="cards">
        <section class="card stack" aria-labelledby="h-target">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-target"></uui-icon></span><h3 id="h-target">Target</h3></div>
          <div class="row">
            ${u(this, d, ue).call(this, "version", "WCAG version", e.version, [["V20", "2.0"], ["V21", "2.1"], ["V22", "2.2"]], (i) => u(this, d, E).call(this, (s) => s.version = i))}
            ${u(this, d, ue).call(this, "level", "Level", e.level, [["A", "A"], ["AA", "AA"], ["AAA", "AAA"]], (i) => u(this, d, E).call(this, (s) => s.level = i))}
          </div>
          <p class="muted">The EU legal requirement today (European Accessibility Act, EN 301 549) is WCAG 2.1 AA. 2.2 AA adds six criteria, including minimum target size.</p>
          ${e.level === "AAA" ? n`<p class="warning" role="note"><uui-icon name="icon-alert"></uui-icon> W3C does not recommend AAA as a requirement for entire sites: some AAA criteria cannot be met for all content.</p>` : l}
        </section>

        <section class="card" aria-labelledby="h-scan">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-search"></uui-icon></span><h3 id="h-scan">Scan</h3></div>
          <div class="stack">
            <div class="field">
              <label for="exclude">Never scan these paths (one per line, e.g. /search)</label>
              <textarea id="exclude" rows="3" ?disabled=${u(this, d, H).call(this, "scan.excludePaths")} .value=${e.scan.excludePaths.join(`
`)}
                @change=${(i) => u(this, d, E).call(this, (s) => s.scan.excludePaths = i.target.value.split(`
`).map((a) => a.trim()).filter(Boolean))}></textarea>
              ${u(this, d, Z).call(this, "scan.excludePaths")}
            </div>
            ${u(this, d, G).call(this, "scan.mobileWidth", "Also scan at mobile width (320 px)", e.scan.mobileWidth, (i) => u(this, d, E).call(this, (s) => s.scan.mobileWidth = i), "Doubles the scan time.")}
            ${u(this, d, G).call(this, "scan.bestPractice", "Include axe best-practice rules", e.scan.bestPractice, (i) => u(this, d, E).call(this, (s) => s.scan.bestPractice = i), "Shown separately in Issues; never counted in the score.")}
            <div class="field">
              <label for="recheck">Ask to re-check manual results after (months)</label>
              <input id="recheck" type="number" min="1" max="24" ?disabled=${u(this, d, H).call(this, "scan.recheckMonths")} .value=${String(e.scan.recheckMonths)}
                @change=${(i) => u(this, d, E).call(this, (s) => s.scan.recheckMonths = Number(i.target.value) || 6)}>
              ${u(this, d, Z).call(this, "scan.recheckMonths")}
            </div>
          </div>
        </section>

        <section class="card wide" aria-labelledby="h-visitor">
          <div class="card-head"><span class="icon-badge tone-notice" aria-hidden="true"><uui-icon name="icon-globe"></uui-icon></span><h3 id="h-visitor">Visitor features (public website)</h3></div>
          <div class="stack">
            <p class="notice"><uui-icon name="icon-info"></uui-icon> The preferences button doesn't make the site compliant. It's an extra on top of a site that already passes.</p>
            <fieldset>
              <legend>Follow visitors' browser and OS settings</legend>
              ${u(this, d, G).call(this, "visitor.followOsMotion", "Reduced motion", e.visitor.followOsMotion, (i) => u(this, d, E).call(this, (s) => s.visitor.followOsMotion = i))}
              ${u(this, d, G).call(this, "visitor.followOsContrast", "More contrast", e.visitor.followOsContrast, (i) => u(this, d, E).call(this, (s) => s.visitor.followOsContrast = i), "Off by default: generic contrast CSS is what most often clashes with a design. Test the site with it on before enabling.")}
            </fieldset>
            <fieldset>
              <legend>Accessibility preferences button</legend>
              ${u(this, d, G).call(this, "visitor.panel.enabled", "Show the preferences button", t.enabled, (i) => u(this, d, E).call(this, (s) => s.visitor.panel.enabled = i))}
              <div class="indent">
                ${ls.map((i) => u(this, d, G).call(this, `visitor.panel.${i.key}`, i.label, t[i.key], (s) => u(this, d, E).call(this, (a) => a.visitor.panel[i.key] = s), void 0, !t.enabled))}
                ${u(this, d, ue).call(this, "visitor.panel.position", "Button position", t.position, [["bottom-right", "Bottom right"], ["bottom-left", "Bottom left"]], (i) => u(this, d, E).call(this, (s) => s.visitor.panel.position = i), !t.enabled)}
              </div>
            </fieldset>
            <details class="disclosure">
              <summary><uui-icon class="chevron" name="icon-navigation-right" aria-hidden="true"></uui-icon>Panel texts per language</summary>
              <p class="muted">Leave a field empty to use the built-in text (English and Swedish ship with the package).</p>
              ${this._cultures.map((i) => u(this, d, Wt).call(this, i))}
              <div class="row">
                <div class="field">
                  <label for="new-culture">Add a language (e.g. da, de)</label>
                  <input id="new-culture" .value=${this._newCulture} @input=${(i) => this._newCulture = i.target.value.trim().toLowerCase()}>
                </div>
                <uui-button look="secondary" label="Add language" ?disabled=${!/^[a-z]{2}(-[a-z]{2})?$/.test(this._newCulture)}
                  @click=${() => {
      this._cultures = [.../* @__PURE__ */ new Set([...this._cultures, this._newCulture])], this._newCulture = "";
    }}></uui-button>
              </div>
            </details>
          </div>
        </section>
        </div>

        <div class="row">
          <uui-button type="submit" look="primary" color="positive" label="Save settings" .state=${this._saving ? "waiting" : void 0}></uui-button>
        </div>
      </form>`;
  }
};
fe = /* @__PURE__ */ new WeakMap();
d = /* @__PURE__ */ new WeakSet();
H = function(e) {
  const t = e.toLowerCase();
  return (this.settings?.locked ?? []).some((i) => i.toLowerCase() === t);
};
E = function(e) {
  const t = Bt(this._draft);
  e(t), this._draft = t;
};
Ft = async function(e) {
  if (e.preventDefault(), !this._draft) return;
  this._saving = !0;
  const { data: t, error: i } = await C(this, ti(this._draft));
  this._saving = !1, !(i || !t) && (ns(this, fe)?.peek("positive", { data: { headline: "Settings saved", message: "The public site picks them up within a minute." } }), this.dispatchEvent(new CustomEvent("saved", { detail: t })));
};
Wt = function(e) {
  const t = this._draft.visitor.panel.texts?.[e] ?? {};
  return n`
      <div class="table-wrap"><table>
        <caption>${e}</caption>
        <thead><tr><th scope="col">Text</th><th scope="col">Override</th></tr></thead>
        <tbody>
          ${Object.entries(os).map(([i, s]) => {
    const a = `t-${e}-${i}`, r = `visitor.panel.texts.${e}.${i}`;
    return n`<tr>
              <th scope="row"><label for=${a}>${i}</label></th>
              <td>
                <input id=${a} placeholder=${s} ?disabled=${u(this, d, H).call(this, r)} .value=${t[i] ?? ""}
                  @change=${(o) => u(this, d, E).call(this, (c) => {
      const A = o.target.value.trim();
      c.visitor.panel.texts ??= {};
      const P = c.visitor.panel.texts[e] ??= {};
      A ? P[i] = A : delete P[i];
    })}>
                ${u(this, d, Z).call(this, r)}
              </td>
            </tr>`;
  })}
        </tbody>
      </table></div>`;
};
Z = function(e) {
  return u(this, d, H).call(this, e) ? n`<span class="lock"><uui-icon name="icon-lock"></uui-icon> Set in configuration</span>` : l;
};
G = function(e, t, i, s, a, r = !1) {
  const o = `c-${e.split(".").join("-")}`, c = a ? `${o}-hint` : void 0;
  return n`
      <div class="check">
        <input id=${o} type="checkbox" .checked=${i} ?disabled=${r || u(this, d, H).call(this, e)} aria-describedby=${c ?? l}
          @change=${(A) => s(A.target.checked)}>
        <label for=${o}>${t}</label>
        ${u(this, d, Z).call(this, e)}
        ${a ? n`<p id=${c} class="muted hint">${a}</p>` : l}
      </div>`;
};
ue = function(e, t, i, s, a, r = !1) {
  const o = `s-${e.split(".").join("-")}`;
  return n`
      <div class="field">
        <label for=${o}>${t}</label>
        <select id=${o} ?disabled=${r || u(this, d, H).call(this, e)} @change=${(c) => a(c.target.value)}>
          ${s.map(([c, A]) => n`<option value=${c} ?selected=${c === i}>${A}</option>`)}
        </select>
        ${u(this, d, Z).call(this, e)}
      </div>`;
};
F.styles = [ie, X`
    .cards { display: grid; gap: var(--a11y-gap); grid-template-columns: repeat(auto-fit, minmax(min(100%, 26rem), 1fr)); align-items: stretch; }
    .cards > .wide { grid-column: 1 / -1; }
    .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); align-items: center; min-height: 44px; }
    .check input { width: 20px; height: 20px; margin: 0; }
    .check > label { font-weight: 600; }
    .check .hint, .check .lock { grid-column: 2; margin: 0; }
    .hint { font-size: 13px; }
    .indent { padding-left: var(--uui-size-space-6); display: grid; }
    fieldset { border: 0; padding: 0; margin: 0; }
    legend { font-weight: 700; margin-bottom: var(--uui-size-space-2); }
    select, input:not([type='checkbox']), textarea { width: 100%; max-width: 32rem; }
    .lock { color: var(--a11y-muted); font-size: 13px; display: inline-flex; gap: 4px; align-items: center; }
    .warning, .notice {
      display: flex; gap: var(--uui-size-space-3); align-items: flex-start; padding: var(--uui-size-space-3) var(--uui-size-space-4);
      border-radius: var(--a11y-radius-sm); color: var(--uui-color-text);
      background: color-mix(in srgb, var(--tone) 9%, var(--uui-color-surface));
      border: 1px solid color-mix(in srgb, var(--tone) 40%, var(--a11y-line)); box-shadow: inset 4px 0 0 var(--tone);
    }
    .warning { --tone: var(--a11y-warning); }
    .notice { --tone: var(--a11y-notice); }
    .warning uui-icon, .notice uui-icon { flex: none; margin-top: 2px; color: color-mix(in srgb, var(--tone) 80%, var(--uui-color-text)); }
    details .muted, details .table-wrap, details > .row { margin-top: var(--uui-size-space-3); }
    caption { text-align: left; font-weight: 700; padding: var(--uui-size-space-3) var(--uui-size-space-4); }
    table input { width: 100%; max-width: none; }
  `];
se([
  L({ attribute: !1 })
], F.prototype, "settings", 2);
se([
  p()
], F.prototype, "_draft", 2);
se([
  p()
], F.prototype, "_saving", 2);
se([
  p()
], F.prototype, "_cultures", 2);
se([
  p()
], F.prototype, "_newCulture", 2);
F = se([
  ee("esatto-a11y-settings")
], F);
var cs = Object.defineProperty, ds = Object.getOwnPropertyDescriptor, Vt = (e) => {
  throw TypeError(e);
}, V = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? ds(t, i) : t, r = e.length - 1, o; r >= 0; r--)
    (o = e[r]) && (a = (s ? o(t, i, a) : o(a)) || a);
  return s && a && cs(t, i, a), a;
}, us = (e, t, i) => t.has(e) || Vt("Cannot " + i), ps = (e, t, i) => t.has(e) ? Vt("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), qt = (e, t, i) => (us(e, t, "access private method"), i), ve, Gt, jt;
const hs = [
  { id: "overview", label: "Overview" },
  { id: "issues", label: "Issues" },
  { id: "checklist", label: "Manual checklist" },
  { id: "settings", label: "Settings" }
];
let I = class extends te {
  constructor() {
    super(), ps(this, ve), this._knowledge = null, this._sites = [], this._siteKey = "", this._settings = null, this._tab = "overview", this._filter = {}, this._error = null, this.addEventListener(at, (e) => {
      const { tab: t, filter: i } = e.detail;
      this._filter = i ?? {}, this._tab = t;
    });
  }
  async connectedCallback() {
    super.connectedCallback();
    const [e, t, i] = await Promise.all([
      C(this, Yt(), { disableNotifications: !0 }),
      C(this, Qt(), { disableNotifications: !0 }),
      C(this, ei(), { disableNotifications: !0 })
    ]);
    if (e.error || t.error || i.error || !e.data || !t.data || !i.data) {
      this._error = "Could not load the accessibility dashboard.";
      return;
    }
    this._knowledge = e.data, this._sites = t.data, this._siteKey = t.data[0]?.key ?? "", this._settings = i.data;
  }
  render() {
    if (this._error) return n`<uui-box><p class="error">${this._error}</p></uui-box>`;
    if (!this._settings || !this._knowledge) return n`<uui-loader></uui-loader>`;
    if (!this._sites.length)
      return n`<uui-box headline="Accessibility"><p>There is no published site to scan yet. Publish a root page with a template first.</p></uui-box>`;
    const e = hs.filter((t) => t.id !== "settings" || this._settings.canEdit);
    return n`
      <div class="header">
        <div class="brand">
          <span class="logo" aria-hidden="true"><uui-icon name="icon-users"></uui-icon></span>
          <div>
            <h2>Accessibility</h2>
            <p class="muted small">WCAG checks on every published page, and a checklist for what only a person can judge.</p>
          </div>
        </div>
        ${this._sites.length > 1 ? n`
          <uui-select label="Site" .options=${this._sites.map((t) => ({ name: t.name, value: t.key, selected: t.key === this._siteKey }))}
            @change=${(t) => this._siteKey = t.target.value}></uui-select>` : l}
      </div>
      <uui-tab-group>
        ${e.map((t) => n`<uui-tab id="tab-${t.id}" aria-controls="tabpanel" label=${t.label} ?active=${this._tab === t.id} @click=${() => {
      this._filter = {}, this._tab = t.id;
    }}>${t.label}</uui-tab>`)}
      </uui-tab-group>
      <div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${this._tab}">${qt(this, ve, jt).call(this)}</div>`;
  }
};
ve = /* @__PURE__ */ new WeakSet();
Gt = function(e) {
  this._settings = e.detail;
};
jt = function() {
  switch (this._tab) {
    case "issues":
      return n`<esatto-a11y-issues .siteKey=${this._siteKey} .filter=${this._filter} .settings=${this._settings}></esatto-a11y-issues>`;
    case "checklist":
      return n`<esatto-a11y-checklist .siteKey=${this._siteKey}></esatto-a11y-checklist>`;
    case "settings":
      return n`<esatto-a11y-settings .settings=${this._settings} @saved=${qt(this, ve, Gt)}></esatto-a11y-settings>`;
    default:
      return n`<esatto-a11y-overview .siteKey=${this._siteKey} .settings=${this._settings}></esatto-a11y-overview>`;
  }
};
I.styles = [ie, X`
    :host { padding: var(--uui-size-layout-1); max-width: 1440px; }
    .header { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; justify-content: space-between; margin-bottom: var(--uui-size-space-5); }
    .brand { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    .brand h2 { font-size: 24px; font-weight: 700; letter-spacing: -0.01em; line-height: 1.2; }
    .logo {
      display: grid; place-items: center; width: 44px; height: 44px; border-radius: 12px; font-size: 22px; color: #fff;
      background: linear-gradient(135deg, #0e9f6e 0%, #2f6fde 100%); box-shadow: 0 6px 16px -6px rgb(14 159 110 / 0.6);
    }
    uui-tab-group { border-bottom: 1px solid var(--a11y-line); }
    .panel { padding-top: var(--uui-size-space-6); }
  `];
V([
  p()
], I.prototype, "_knowledge", 2);
V([
  p()
], I.prototype, "_sites", 2);
V([
  p()
], I.prototype, "_siteKey", 2);
V([
  p()
], I.prototype, "_settings", 2);
V([
  p()
], I.prototype, "_tab", 2);
V([
  p()
], I.prototype, "_filter", 2);
V([
  p()
], I.prototype, "_error", 2);
I = V([
  ee("esatto-accessibility-dashboard")
], I);
const ys = I;
export {
  I as EsattoAccessibilityDashboardElement,
  ys as default
};
//# sourceMappingURL=accessibility-dashboard.js.map
