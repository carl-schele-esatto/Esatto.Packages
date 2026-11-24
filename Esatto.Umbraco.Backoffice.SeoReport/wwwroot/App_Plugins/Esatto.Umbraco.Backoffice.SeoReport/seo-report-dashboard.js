import { css as w, property as h, customElement as T, nothing as c, html as l, live as W, state as B, styleMap as Os, query as ir } from "@umbraco-cms/backoffice/external/lit";
import { UMB_CURRENT_USER_CONTEXT as cn } from "@umbraco-cms/backoffice/current-user";
import { UmbLitElement as k } from "@umbraco-cms/backoffice/lit-element";
import { umbHttpClient as Ki } from "@umbraco-cms/backoffice/http-client";
const un = "Umb.Section.Settings";
function dn(e) {
  return e === void 0 ? void 0 : e.includes(un);
}
const hn = [
  "notConfigured",
  "keyUnreadable",
  "keyInvalid",
  "apiDisabled",
  "noAccess",
  "propertyNotFound",
  "noData",
  "quota",
  "unknown",
  "ga4PropertyInvalid",
  "searchConsoleInvalid"
], pn = [
  "periodInvalid",
  "periodOutOfRange",
  "dateInvalid",
  "dateOutOfRange",
  "alreadyRunning",
  "requestInvalid"
], fn = /* @__PURE__ */ new Set([...hn, ...pn]);
function gn(e) {
  return typeof e == "string" && fn.has(e);
}
function sr(e) {
  return gn(e) ? e : "unknown";
}
class Ot extends Error {
  constructor(t, i, s, r) {
    super(`The request failed (${t || "no answer"}, ${i}).`), this.name = "ApiError", this.status = t, this.code = i, this.args = s, this.traceId = r;
  }
}
const mn = (e) => e instanceof Ot, Ct = (e) => typeof e == "object" && e !== null && !Array.isArray(e);
function vn(e) {
  if (!Ct(e)) return;
  const t = {};
  for (const [i, s] of Object.entries(e))
    (s === null || ["string", "number", "boolean"].includes(typeof s)) && (t[i] = s);
  return Object.keys(t).length > 0 ? t : void 0;
}
function N(e, t) {
  if (mn(e)) return e;
  const i = Ct(e) && Ct(e.problemDetails) ? e.problemDetails : e;
  if (!Ct(i)) return new Ot(t ?? 0, "unknown");
  const s = typeof i.status == "number" ? i.status : 0;
  return new Ot(
    t ?? s,
    sr(i.code),
    vn(i.args),
    typeof i.traceId == "string" ? i.traceId : void 0
  );
}
const Ui = "/umbraco/management/api/v1/esatto/seo-report", Gi = () => [{ scheme: "bearer", type: "http" }], rr = { "Content-Type": "application/json" };
async function Wi(e) {
  let t;
  try {
    t = await e();
  } catch (i) {
    throw N(i);
  }
  if (t.error !== void 0) throw N(t.error, t.response?.status);
  return t.data;
}
function Fe(e) {
  const t = Object.entries(e).filter(([, i]) => i).map(([i, s]) => `${i}=${encodeURIComponent(s)}`);
  return t.length > 0 ? `?${t.join("&")}` : "";
}
const oe = (e) => Wi(() => Ki.get({ url: `${Ui}/${e}`, security: Gi(), throwOnError: !1 })), Bi = (e, t) => Wi(
  () => Ki.post({ url: `${Ui}/${e}`, security: Gi(), throwOnError: !1, ...t === void 0 ? {} : { body: t, headers: rr } })
), Hi = (e, t) => Wi(() => Ki.put({ url: `${Ui}/${e}`, body: t, headers: rr, security: Gi(), throwOnError: !1 })), bn = () => oe("status"), yn = (e) => oe(`report${Fe({ period: e })}`), $n = (e) => oe(`analysis${Fe({ period: e })}`), xn = (e) => oe(`daily${Fe({ month: e })}`), wn = (e) => oe(`daily/sources${Fe({ date: e })}`), _n = () => oe("fetch/progress"), Tn = (e) => Bi(`refresh${Fe({ period: e })}`), ar = () => Bi("fetch"), kn = (e, t) => Hi(`note${Fe({ period: e })}`, { text: t }), Cn = (e) => Hi("targets", { terms: e }), Sn = () => oe("settings"), En = (e) => Hi("settings", e), Ln = (e) => Bi("settings/test", e), Pn = () => oe("user-groups"), C = w`
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
  .visually-hidden {
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden;
    clip: rect(0 0 0 0); white-space: nowrap; border: 0;
  }
  h2, h3, h4 { margin: 0; }
  p { margin: 0; }
  code, pre { font-family: var(--uui-font-monospace, monospace); font-size: 0.9em; }
  code { padding: 1px 6px; border-radius: 6px; background: var(--seo-wash); border: 1px solid var(--seo-line); }
  a { color: var(--uui-color-interactive); text-underline-offset: 2px; }
  a:hover { color: var(--uui-color-interactive-emphasis); }
  :focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }

  /* Surfaces */
  .card {
    background: var(--uui-color-surface); color: var(--uui-color-text);
    border: 1px solid var(--seo-line); border-radius: var(--seo-radius); box-shadow: var(--seo-shadow);
    padding: var(--uui-size-space-5) var(--uui-size-space-6);
  }
  .card-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; margin-bottom: var(--uui-size-space-4); }
  .card-head h3 { font-size: 16px; font-weight: 700; }
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
  .banner .actions { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); margin-top: var(--uui-size-space-2); }

  /* Form controls */
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label { font-size: 13px; font-weight: 600; }
  select {
    font: inherit; min-height: 40px; padding: 0 var(--uui-size-space-3); box-sizing: border-box;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  select:hover { border-color: var(--uui-color-border-emphasis); }
  select:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  select:disabled { opacity: 0.65; cursor: not-allowed; }

  @media (prefers-reduced-motion: reduce) {
    *, *::before, *::after { transition: none !important; animation: none !important; }
  }
`;
class Dn {
  #a;
  #f;
  #l;
  #e;
  #i;
  // Bumped by every stop and every end, so a read that was started under an earlier one is ignored when it answers.
  #s = 0;
  #n = !1;
  #u = !1;
  #t = 0;
  constructor(t) {
    this.#a = t, this.#f = t.intervalMs ?? 2e3, this.#l = t.maxIntervalMs ?? 3e4, this.#e = t.maxFailures ?? 8;
  }
  /** Whether it is waiting for, or reading, the next progress. */
  get active() {
    return this.#n;
  }
  /** Whether it has been disposed: it will not start again. */
  get disposed() {
    return this.#u;
  }
  /** Starts following the run: the first read is one interval from now. Does nothing while it is already going, or once it is disposed. */
  start() {
    this.#n || this.#u || (this.#n = !0, this.#t = 0, this.#o(this.#s, this.#f));
  }
  /** Stops following, cancels the timer and ignores a read that is on its way. It can be started again. */
  stop() {
    this.#d();
  }
  /** Stops for good, as stop() does, and makes every later start() do nothing. For an element that has left the page. */
  dispose() {
    this.#u = !0, this.#d();
  }
  #d() {
    this.#n = !1, this.#s++, clearTimeout(this.#i), this.#i = void 0;
  }
  #o(t, i) {
    this.#i = setTimeout(() => {
      this.#h(t);
    }, i);
  }
  async #h(t) {
    this.#i = void 0;
    let i;
    try {
      i = await this.#a.load();
    } catch (s) {
      if (t !== this.#s) return;
      if (this.#t++, this.#a.onError?.(s, this.#t), this.#t >= this.#e) {
        this.#d(), this.#a.onGiveUp?.(s);
        return;
      }
      this.#o(t, Math.min(this.#f * 2 ** this.#t, this.#l));
      return;
    }
    if (t === this.#s) {
      if (this.#t = 0, !i.running) {
        this.#d(), this.#a.onFinished(i);
        return;
      }
      this.#a.onProgress(i), this.#o(t, this.#f);
    }
  }
}
function Nn(e) {
  return !e?.series?.some((t) => t.values.some((i) => i != null));
}
function Rn(e) {
  return (e?.segments?.length ?? 0) > 0;
}
function On(e) {
  return e?.traffic?.search?.annotations?.filter((t) => t.severity === "warning").length ?? 0;
}
const zn = (e) => On(e) > 0, An = "organic_sessions", Mn = "organic_engagement_rate";
function nr(e) {
  const t = e.find((r) => r.key === An) ?? e[0] ?? null, i = e.find((r) => r.key === Mn && r.format === "percent"), s = i && typeof i.value == "number" && i.value >= 0 && i.value <= 100 ? i : null;
  return { main: t, ring: s };
}
function Fn(e) {
  const { main: t, ring: i } = nr(e);
  return e.filter((s) => s !== t && s !== i);
}
function In(e) {
  const t = e?.change_percent;
  if (typeof t != "number" || !Number.isFinite(t)) return null;
  const i = Math.round(t * 10) / 10, r = (e.direction === "up" || e.direction === "down") && i !== 0 ? i > 0 ? "up" : "down" : "flat";
  return { direction: r, arrow: r === "up" ? "↑" : r === "down" ? "↓" : "→", change: (r === "flat" ? i : Math.abs(i)) + 0 };
}
function Vi(e) {
  return e !== null && e.data_status !== "noData";
}
function Kn(e, t, i = !1) {
  return e.setup !== "ready" ? "notConnected" : e.fetch.running ? "progress" : t === null && i ? "error" : t === null && !e.upToDate ? "progress" : Vi(t) ? "ready" : "noData";
}
function Un(e, t) {
  const i = ["analysis", "traffic", "queries", "topPages"];
  return zn(e) && i.push("toFix"), i.push("comparison"), t && i.push("settings"), i;
}
function Gn(e, t) {
  return Vi(e) ? Un(e, t) : t ? ["settings"] : [];
}
function Wn(e, t, i, s = !1) {
  return s && t.includes("settings") ? "settings" : t.includes(e) ? e : i === "notConnected" || i === "error" ? null : t[0] ?? null;
}
function Bn(e, t) {
  return { tab: t, pinned: t === "settings" && e.pinned };
}
function Hn(e) {
  return e.tab === "settings" && e.pinned ? e : { tab: "settings", pinned: !0 };
}
function or(e) {
  const { done: t, total: i } = e;
  return e.running ? i <= 0 ? { phase: "starting", done: t, total: i, percent: null, currentPeriod: null } : { phase: "running", done: t, total: i, percent: Math.min(100, Math.round(t / i * 100)), currentPeriod: e.currentPeriod } : { phase: "idle", done: t, total: i, percent: null, currentPeriod: null };
}
function Vn(e) {
  return e.errorCode ? "failed" : e.total <= 0 ? "nothingToFetch" : "fetched";
}
function lr(e) {
  if (e.errorCode) return e.errorCode;
  const t = e.lastFailureUtc ? Date.parse(e.lastFailureUtc) : Number.NaN;
  if (!e.lastErrorCode || Number.isNaN(t)) return null;
  const i = e.lastSuccessUtc ? Date.parse(e.lastSuccessUtc) : Number.NaN;
  return Number.isNaN(i) || t > i ? e.lastErrorCode : null;
}
function qi(e) {
  return [...new Set(e)].sort().reverse();
}
function cr(e, t, i, s) {
  return !e && i !== t && s.includes(i);
}
function ur(e, t) {
  return e || qi(t)[0] || "";
}
const qn = {
  status: null,
  report: null,
  note: null,
  fetchedUtc: null,
  period: "",
  pendingPeriod: null,
  archiveRevision: 0,
  loading: !0,
  switching: !1,
  refreshing: !1,
  starting: !1,
  problem: null,
  readFailed: !1,
  outcome: null,
  lost: !1
};
class jn {
  #a;
  #f;
  #l;
  #e = qn;
  #i = null;
  #s = !1;
  // Bumped by connect, disconnect and load: an awaited call that started under an earlier one is dropped when it returns.
  #n = 0;
  // Bumped by every read of a report and by disconnect: only the newest read is used.
  #u = 0;
  // What "Try again" does: the action that failed.
  #t = null;
  // The newest month has been read during the run that is going (see #setFetch): once per run.
  #d = !1;
  constructor(t, i, s = {}) {
    this.#a = t, this.#f = i, this.#l = s;
  }
  get state() {
    return this.#e;
  }
  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------
  /** The element is on the page: starts reading. Resolves when the first read is over. */
  connect() {
    return this.#i?.dispose(), this.#s = !0, this.#i = new Dn({
      load: () => this.#a.getFetchProgress(),
      onProgress: (t) => this.#y(t),
      onFinished: (t) => {
        this.#x(t);
      },
      onGiveUp: () => this.#c({ lost: !0 }),
      ...this.#l
    }), this.load();
  }
  /** The element has left the page: nothing that is on its way may touch anything, and nothing may keep polling. */
  disconnect() {
    this.#s = !1, this.#n++, this.#u++, this.#i?.dispose(), this.#i = null;
  }
  #o(t) {
    return this.#s && t === this.#n;
  }
  // ---- Reading ---------------------------------------------------------------------------------------------------------------------
  /** Reads the status, and the newest report when the package is set up and has months. Replaces any load that is on its way. */
  async load() {
    const t = ++this.#n;
    this.#d = !1, this.#c({ loading: !0, problem: null, readFailed: !1, switching: !1, refreshing: !1, starting: !1, pendingPeriod: null });
    try {
      const i = await this.#a.getStatus();
      if (!this.#o(t)) return;
      this.#p(i), i.setup === "ready" && i.periods.length > 0 ? await this.#h(void 0, t) : this.#g({ report: null, note: null, fetchedUtc: null, period: null });
    } catch (i) {
      this.#o(t) && this.#b(i, () => {
        this.load();
      });
    } finally {
      this.#o(t) && this.#c({ loading: !1 });
    }
  }
  // Reads one month's report (none: the newest) and shows it. Throws what the read threw. An answer that a newer read has replaced, or that
  // arrives after the element left, is dropped.
  async #h(t, i, s = !1) {
    const r = ++this.#u;
    this.#c({ switching: !0 });
    try {
      const a = await this.#a.getReport(t);
      r === this.#u && this.#o(i) && this.#g(a, s);
    } finally {
      r === this.#u && this.#o(i) && this.#c({ switching: !1 });
    }
  }
  // `archiveChanged`: the response is the month read again after the archive changed (a refresh, the end of a backfill), as opposed to a month
  // that was merely chosen.
  #g(t, i = !1) {
    this.#c({
      report: t.report,
      note: t.note,
      fetchedUtc: t.fetchedUtc,
      period: t.period ?? "",
      readFailed: !1,
      ...i ? { archiveRevision: this.#e.archiveRevision + 1 } : {}
    });
  }
  /** Shows the report of another month. Ignored while something else is going on and for a month that is not archived. */
  async selectPeriod(t) {
    const i = this.#e, s = i.loading || i.switching || i.refreshing;
    if (!cr(s, i.pendingPeriod ?? i.period, t, i.status?.periods ?? [])) return;
    const r = this.#n;
    this.#c({ problem: null, pendingPeriod: t });
    try {
      await this.#h(t, r);
    } catch (a) {
      this.#o(r) && this.#b(a, () => {
        this.selectPeriod(t);
      });
    } finally {
      this.#o(r) && this.#c({ pendingPeriod: null });
    }
  }
  // ---- Acting: a person's click ---------------------------------------------------------------------------------------------------------
  /** Fetches the selected month from Google (about fifteen seconds), shows it, and reads the status again. */
  async refresh() {
    const t = this.#e, i = ur(t.period, t.status?.periods ?? []);
    if (t.refreshing || t.switching || !i) return;
    const s = this.#n;
    this.#c({ refreshing: !0, problem: null, outcome: null });
    try {
      const r = await this.#a.refreshReport(i);
      if (!this.#o(s)) return;
      this.#g(r, !0);
      const a = await this.#a.getStatus();
      if (!this.#o(s)) return;
      this.#p(a);
    } catch (r) {
      this.#o(s) && this.#v(r, () => {
        this.refresh();
      });
    } finally {
      this.#o(s) && this.#c({ refreshing: !1 });
    }
  }
  /** Starts the backfill of the missing months and follows it. */
  async fetchNow() {
    if (this.#e.starting) return;
    const t = this.#n;
    this.#c({ starting: !0, problem: null, outcome: null, lost: !1 });
    try {
      const i = await this.#a.startFetch();
      if (!this.#o(t)) return;
      await this.#m(i.progress);
    } catch (i) {
      if (!this.#o(t)) return;
      const s = N(i);
      s.code === "alreadyRunning" && this.#e.status ? this.#r() : this.#v(s, () => {
        this.fetchNow();
      });
    } finally {
      this.#o(t) && this.#c({ starting: !1 });
    }
  }
  // Right after the 202 the run is "starting" (running, no month counted): show it and poll, never read it as "nothing to fetch". A run
  // that is over already (it had nothing to do) is finished at once.
  async #m(t) {
    const i = this.#e.status;
    i && (t.running ? this.#p({ ...i, fetch: t }) : await this.#x(t));
  }
  // A run is going that this model did not see start (a 409 alreadyRunning): mark it running, and the poller reads how it goes.
  #r() {
    const t = this.#e.status;
    t && this.#p({ ...t, fetch: { ...t.fetch, running: !0 } });
  }
  /**
   * Follows a run another part of the dashboard started (the Settings tab's Fetch now) with the one poller, as fetchNow follows its own: `progress`
   * is what the start answered, null when the start found a run going already.
   */
  async followRun(t) {
    this.#s && (this.#c({ outcome: null, lost: !1 }), t ? await this.#m(t) : this.#r());
  }
  /**
   * Reads the status again WITHOUT the loading screen, which would take the open tab, and what is being typed in it, off the page: the Settings
   * tab saved settings (the setup may be ready now, the display name may have changed, and the save may have started a run, which the poller then
   * follows). The newest report is read when none is shown yet and the archive has months; a report that is shown stays.
   */
  async reloadStatus() {
    if (!this.#s) return;
    const t = this.#n;
    this.#c({ problem: null });
    try {
      const i = await this.#a.getStatus();
      if (!this.#o(t)) return;
      this.#p(i), i.setup === "ready" && i.periods.length > 0 && this.#e.report === null && await this.#h(void 0, t);
    } catch (i) {
      this.#o(t) && this.#b(i, () => {
        this.reloadStatus();
      });
    }
  }
  /** Follows the backfill again after contact was lost. */
  followAgain() {
    this.#c({ lost: !1 }), this.#i?.start();
  }
  dismissOutcome() {
    this.#c({ outcome: null });
  }
  /** Repeats what failed. */
  retry() {
    this.#t?.();
  }
  /**
   * A note was saved (the Analysis tab does that): keeps the note this model holds in step, so that a tab that is opened again shows what was
   * saved and not what the report was read with. `note` is what the server kept: trimmed, or null when it was removed.
   *
   * ONLY THE SHOWN MONTH'S NOTE IS HELD. A note for another month (the month was changed while the save was on its way) is ignored: the server has
   * it, and the next read of that month brings it. After the element has left nothing is touched.
   */
  setNote(t, i) {
    !this.#s || t !== this.#e.period || this.#c({ note: i });
  }
  // ---- The status and the run ----------------------------------------------------------------------------------------------------------------
  // The ONE place a status read from the server is stored: it starts the poller when the status says a run is going. A status that could be
  // read ends the error state of a read that failed before it: what follows (a report read, or none for an empty archive) decides again, so
  // a read that is mended can never leave the error state behind with no problem in it.
  #p(t) {
    this.#c({ status: t, readFailed: !1 }), t.fetch.running && this.#s && this.#i?.start();
  }
  // The progress of the run that is being followed: only the run's part of the status changes, and the poller is already going. Once the run
  // has archived its first month (the newest: a backfill goes newest first) and no report is shown, that month is read, once, so it shows under
  // the progress card while the older months load.
  #y(t) {
    const i = this.#e.status;
    i && this.#c({ status: { ...i, fetch: t } }), t.running && t.done >= 1 && this.#e.report === null && !this.#d && this.#$();
  }
  // One read of the newest month while the run goes on. Its month joins the months the picker offers (the status is read again when the run
  // ends). A failure is shown beside the progress card and is not the error state: the run is still going, and its end reads everything again.
  async #$() {
    this.#d = !0;
    const t = this.#n;
    try {
      await this.#h(void 0, t, !0);
      const { status: i, period: s } = this.#e;
      this.#o(t) && i && s && !i.periods.includes(s) && this.#c({ status: { ...i, periods: [...i.periods, s].sort() } });
    } catch (i) {
      this.#o(t) && this.#v(i, () => {
        this.#$();
      });
    }
  }
  async #x(t) {
    this.#d = !1, this.#y(t), this.#c({ outcome: { kind: Vn(t), done: t.done, errorCode: t.errorCode } }), await this.#w();
  }
  // The archive has grown: read the status and the selected month again (none: the newest), WITHOUT the loading screen, and so does Try again
  // after it failed (load() would take the open tab off the page, and with it what is being typed on the Settings tab).
  async #w() {
    const t = this.#n;
    try {
      const i = await this.#a.getStatus();
      if (!this.#o(t)) return;
      this.#p(i), i.setup === "ready" && i.periods.length > 0 && await this.#h(this.#e.period || void 0, t, !0), this.#o(t) && this.#c({ problem: null });
    } catch (i) {
      this.#o(t) && this.#b(i, () => {
        this.#_();
      });
    }
  }
  async #_() {
    this.#c({ problem: null }), await this.#w();
  }
  // ---- Failure ---------------------------------------------------------------------------------------------------------------------------------
  #v(t, i) {
    this.#t = i, this.#c({ problem: N(t) });
  }
  // A failed read: with no report to show instead it is the error state, never "no report".
  #b(t, i) {
    this.#v(t, i), this.#c({ readFailed: this.#e.report === null });
  }
  #c(t) {
    this.#e = { ...this.#e, ...t }, this.#f();
  }
}
const Yn = "esattoSeoReport_", Xn = ["kpi", "chart", "series", "annotation", "insight", "narrative", "opportunity", "target", "error", "ui"], Zn = ["Label", "Description", "Title", "Subtitle", "Text"], Jn = /^[A-Za-z0-9]+(_[A-Za-z0-9]+)*$/, dr = /^([A-Za-z0-9]+(?:_[A-Za-z0-9]+)*)_([0-9]{4})$/;
function Vt(e, t, i) {
  if (!Xn.includes(e)) throw new RangeError(`Unknown key area '${e}'.`);
  if (!Zn.includes(i)) throw new RangeError(`Unknown key part '${i}'.`);
  if (!Jn.test(t)) throw new RangeError(`'${t}' is not a valid key id; use letters and digits joined by single underscores.`);
  const r = (e === "series" ? dr.exec(t)?.[1] ?? t : t).split("_").map((a) => a[0].toUpperCase() + a.slice(1));
  return e + r.join("") + i;
}
function hr(e) {
  const t = dr.exec(e);
  return t ? Number(t[2]) : null;
}
function Qn(e) {
  const t = hr(e);
  return { key: Vt("series", e, "Label"), args: t === null ? void 0 : { year: t } };
}
const eo = {
  dataStatusNote: { area: "ui", parts: ["Text"] },
  // Report.data_status_note
  sourceNote: { area: "ui", parts: ["Text"] },
  // SourceStatus.note
  trend: { area: "kpi", parts: ["Text"] },
  // Trend.text (any KPI)
  annotation: { area: "annotation", parts: ["Title", "Text"] },
  // Annotation.text
  insight: { area: "insight", parts: ["Text"] },
  // ChartBlock.insights[], SearchBlock.insights[]
  narrative: { area: "narrative", parts: ["Text"] },
  // Statement.text (the analysis' summary)
  opportunity: { area: "opportunity", parts: ["Title", "Text"] },
  // Opportunity.text: the key is the kind, both parts get the arguments
  targetAdvice: { area: "target", parts: ["Text"] }
  // TargetRow.advice
}, to = {
  kpi: { area: "kpi", parts: ["Label", "Description"] },
  // TrafficSection.kpis[].key
  segmentMetric: { area: "kpi", parts: ["Label"] },
  // SearchSegment.metrics[].key
  chart: { area: "chart", parts: ["Title", "Subtitle", "Description"] },
  // ChartBlock.id
  searchBlock: { area: "chart", parts: ["Title", "Subtitle"] },
  // SearchBlock.id
  source: { area: "ui", parts: ["Label"] }
  // SourceStatus.id
};
function io(e, t) {
  const { area: i, parts: s } = eo[e];
  return { keys: Object.fromEntries(s.map((r) => [r, Vt(i, t.key, r)])), args: t.args ?? {} };
}
function so(e, t) {
  const { area: i, parts: s } = to[e];
  return Object.fromEntries(s.map((r) => [r, Vt(i, t, r)]));
}
const ro = (e) => e.replace(/[A-Z]/g, (t, i) => (i > 0 ? "_" : "") + t.toLowerCase());
function zs(e, t) {
  return Vt("error", ro(e), t);
}
function o(e, t, i) {
  return e.localize.term(Yn + t, i ?? {});
}
function q(e, t, i, s = "Text") {
  if (!i) return "";
  try {
    const { keys: r, args: a } = io(t, i), n = r[s];
    return n ? o(e, n, a) : "";
  } catch (r) {
    if (r instanceof RangeError) return "";
    throw r;
  }
}
function j(e, t, i, s, r) {
  try {
    const a = so(t, i)[s];
    return a ? o(e, a, r) : "";
  } catch (a) {
    if (a instanceof RangeError) return "";
    throw a;
  }
}
function le(e, t, i) {
  const s = sr(t);
  return { title: o(e, zs(s, "Title"), i), text: o(e, zs(s, "Text"), i) };
}
const L = (e) => e.localize.lang(), pr = /^(\d{4})-(0[1-9]|1[0-2])$/, ao = /^(\d{4})-(\d{2})-(\d{2})$/, Ie = (e) => typeof e == "number" && Number.isFinite(e);
function V(e, t) {
  try {
    return t(e);
  } catch {
    return t("en");
  }
}
function et(e, t) {
  if (!e) return "";
  const i = pr.exec(e);
  if (!i) return e;
  const s = new Date(Date.UTC(Number(i[1]), Number(i[2]) - 1, 1));
  return V(t, (r) => new Intl.DateTimeFormat(r, { month: "long", year: "numeric", timeZone: "UTC" }).format(s));
}
function fr(e, t) {
  if (!e) return "";
  const i = pr.exec(e);
  if (!i) return e;
  const s = new Date(Date.UTC(Number(i[1]), Number(i[2]) - 1, 1));
  return V(t, (r) => new Intl.DateTimeFormat(r, { month: "short", year: "numeric", timeZone: "UTC" }).format(s));
}
function gr(e, t, i) {
  if (e == null || e === "") return "";
  const s = typeof e == "number" ? e : /^\d{1,2}$/.test(e) ? Number(e) : Number.NaN;
  if (!Number.isInteger(s) || s < 1 || s > 12) return String(e);
  const r = new Date(Date.UTC(2e3, s - 1, 1));
  return V(t, (a) => new Intl.DateTimeFormat(a, { month: i, timeZone: "UTC" }).format(r));
}
const no = (e, t) => gr(e, t, "short"), oo = (e, t) => gr(e, t, "long");
function O(e, t, i = 2) {
  return Ie(e) ? V(t, (s) => new Intl.NumberFormat(s, { maximumFractionDigits: i }).format(e)) : "";
}
function lo(e, t) {
  return Ie(e) ? V(t, (i) => new Intl.NumberFormat(i, { notation: "compact", maximumFractionDigits: 1 }).format(e)) : "";
}
function Pe(e, t, i) {
  return Ie(e) ? V(t, (s) => new Intl.NumberFormat(s, { minimumFractionDigits: i, maximumFractionDigits: i }).format(e)) : "";
}
function Ke(e, t, i = {}) {
  return Ie(e) ? V(
    t,
    (s) => new Intl.NumberFormat(s, {
      style: "percent",
      minimumFractionDigits: i.fixed ? 1 : 0,
      maximumFractionDigits: Math.max(i.digits ?? 1, i.fixed ? 1 : 0),
      signDisplay: i.signed ? "exceptZero" : "auto"
    }).format(e / 100)
  ) : "";
}
function co(e) {
  if (!Ie(e) || e < 0) return "";
  const t = Math.round(e);
  return `${Math.floor(t / 60)}:${String(t % 60).padStart(2, "0")}`;
}
function dt(e, t, i) {
  if (!Ie(e)) return "";
  switch (t) {
    case "integer":
      return O(e, i, 0);
    case "signed":
      return V(i, (s) => new Intl.NumberFormat(s, { maximumFractionDigits: 2, signDisplay: "exceptZero" }).format(e));
    case "percent":
      return Ke(e, i);
    case "duration":
      return co(e);
    default:
      return O(e, i);
  }
}
function mr(e) {
  if (!e) return null;
  const t = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(e) ? e : `${e}Z`);
  return Number.isNaN(t.getTime()) ? null : t;
}
function uo(e, t, i) {
  const s = mr(e);
  return s ? V(t, (r) => new Intl.DateTimeFormat(r, { dateStyle: "medium", timeStyle: "short", timeZone: i }).format(s)) : "";
}
const gi = 60, mi = 60 * gi, ri = 24 * mi;
function ho(e, t, i = /* @__PURE__ */ new Date()) {
  const s = mr(e);
  if (!s) return "";
  const r = Math.floor((i.getTime() - s.getTime()) / 1e3);
  if (r < -300 || r > 30 * ri) return "";
  const [a, n] = r < gi ? [0, "second"] : r < mi ? [-Math.floor(r / gi), "minute"] : r < ri ? [-Math.floor(r / mi), "hour"] : [-Math.floor(r / ri), "day"];
  return V(t, (u) => new Intl.RelativeTimeFormat(u, { numeric: "auto" }).format(a, n));
}
function vr(e, t, i) {
  if (!e) return "";
  const s = ao.exec(e);
  if (!s) return e;
  const [r, a, n] = [Number(s[1]), Number(s[2]), Number(s[3])], u = new Date(Date.UTC(r, a - 1, n));
  return u.getUTCFullYear() !== r || u.getUTCMonth() !== a - 1 || u.getUTCDate() !== n ? e : V(t, (d) => new Intl.DateTimeFormat(d, { ...i, timeZone: "UTC" }).format(u));
}
const vi = (e, t) => vr(e, t, { day: "numeric", month: "short" }), zt = (e, t) => vr(e, t, { weekday: "long", day: "numeric", month: "long", year: "numeric" }), As = { state: "none", ranked: "", lists: [] }, ke = (e) => typeof e == "number" && Number.isFinite(e);
function po(e, t, i) {
  if (!i || !ke(i.delta)) return { text: o(e, "uiNoValueText"), missing: !0 };
  const s = L(e), r = dt(i.delta, "signed", s), a = ke(i.changePercent) ? o(e, "uiMoveDeltaText", { delta: r, percent: Ke(i.changePercent, s, { signed: !0, fixed: !0 }) }) : o(e, "uiMoveNewText", { delta: r }), n = ke(i.positionBefore) && ke(t.position);
  return {
    text: a,
    detail: n ? o(e, "uiMovePositionText", { before: Pe(i.positionBefore, s, 1), now: Pe(t.position, s, 1) }) : void 0
  };
}
function Ms(e, t, i, s) {
  const r = L(e);
  return {
    kind: t,
    title: o(e, t === "losses" ? "uiMovementLossesTitle" : "uiMovementGainsTitle"),
    columns: [
      { label: o(e, "uiColumnPageLabel") },
      { label: o(e, "uiColumnImpressionsLabel"), numeric: !0 },
      { label: o(e, "uiColumnPositionLabel"), numeric: !0 },
      ...s.map((a) => ({ label: o(e, "uiMoveAgainstLabel", { period: a.period }) }))
    ],
    rows: i.map((a) => ({
      cells: [
        { text: a.path || a.url, title: a.url, strong: !0 },
        ke(a.impressions) ? { text: O(a.impressions, r) } : { text: o(e, "uiNoValueText"), missing: !0 },
        ke(a.position) ? { text: Pe(a.position, r, 1) } : { text: o(e, "uiNoValueText"), missing: !0 },
        ...s.map((n) => po(e, a, n.of(a)))
      ]
    }))
  };
}
function fo(e, t) {
  if (!t) return As;
  const i = [
    ...t.previous ? [{ period: t.previous.period, of: (n) => n.vsPrevious }] : [],
    ...t.yearAgo ? [{ period: t.yearAgo.period, of: (n) => n.vsYearAgo }] : []
  ];
  if (i.length === 0) return As;
  const r = t.rankedBy === "yearAgo" ? o(e, "uiMovementRankedYearAgoText", { period: t.yearAgo?.period ?? null }) : o(e, "uiMovementRankedPreviousText", { period: t.previous?.period ?? null }), a = [
    ...t.losses?.length ? [Ms(e, "losses", t.losses, i)] : [],
    ...t.gains?.length ? [Ms(e, "gains", t.gains, i)] : []
  ];
  return { state: a.length === 0 ? "empty" : "ready", ranked: r, lists: a };
}
const br = /* @__PURE__ */ new Map([
  ["missing", { labelKey: "targetMissingLabel", icon: "icon-block" }],
  ["deep", { labelKey: "targetDeepLabel", icon: "icon-arrow-down" }],
  ["near", { labelKey: "targetNearLabel", icon: "icon-arrow-up" }],
  ["title", { labelKey: "targetTitleLabel", icon: "icon-mouse-cursor" }],
  ["performing", { labelKey: "targetPerformingLabel", icon: "icon-check" }]
]);
[...br.keys()];
const go = (e) => typeof e == "number" && Number.isFinite(e), yr = (e) => ({ text: o(e, "uiNoValueText"), missing: !0 });
function bi(e, t, i) {
  return go(t) ? { text: i(t) } : yr(e);
}
function mo(e, t) {
  const i = br.get(t);
  return i ? { text: o(e, i.labelKey), icon: i.icon } : { text: o(e, "uiTargetUnknownLabel"), icon: "icon-info" };
}
function vo(e) {
  return [
    { label: o(e, "uiColumnQueryLabel") },
    { label: o(e, "uiColumnStatusLabel") },
    { label: o(e, "uiColumnImpressionsLabel"), numeric: !0 },
    { label: o(e, "uiColumnClicksLabel"), numeric: !0 },
    { label: o(e, "uiColumnCtrLabel"), numeric: !0 },
    { label: o(e, "uiColumnPositionLabel"), numeric: !0 },
    { label: o(e, "uiColumnMatchesLabel"), numeric: !0 },
    { label: o(e, "uiColumnAdviceLabel"), wide: !0 }
  ];
}
function bo(e, t) {
  const i = L(e);
  return t.map((s) => {
    const r = s.matches > 0, a = (n, u) => r ? bi(e, n, u) : yr(e);
    return {
      cells: [
        {
          text: s.term,
          title: s.term,
          strong: !0,
          detail: r && s.example ? o(e, "uiTargetExampleText", { example: s.example }) : void 0
        },
        mo(e, s.status),
        a(s.impressions, (n) => O(n, i)),
        a(s.clicks, (n) => O(n, i)),
        a(s.ctrPercent, (n) => Ke(n, i, { digits: 2 })),
        a(s.position, (n) => Pe(n, i, 1)),
        bi(e, s.matches, (n) => O(n, i)),
        { text: q(e, "targetAdvice", s.advice) }
      ]
    };
  });
}
const $r = (e) => (e?.rows ?? []).map((t) => t.term);
function yo(e, t) {
  const i = t?.rows ?? [];
  return i.length === 0 ? { state: "noTerms", terms: [], rowsFetched: t?.rowsFetched ?? 0, columns: [], rows: [] } : { state: "ready", terms: $r(t), rowsFetched: t?.rowsFetched ?? 0, columns: vo(e), rows: bo(e, i) };
}
function $o(e, t) {
  const i = t?.months ?? [], s = t?.rows ?? [];
  if (i.length === 0 || s.length === 0) return null;
  const r = L(e);
  return {
    columns: [
      { label: o(e, "uiColumnQueryLabel") },
      ...i.map((a) => ({ label: fr(a.period, r), numeric: !0 }))
    ],
    rows: s.map((a) => ({
      cells: [
        { text: a.term, title: a.term, strong: !0 },
        ...i.map((n, u) => bi(e, a.positions?.[u], (d) => Pe(d, r, 1)))
      ]
    }))
  };
}
function At(e) {
  return e?.hasAnalysis === !0;
}
function xo(e) {
  return e.analysis ? At(e.analysis) ? "ready" : "none" : e.problem ? "failed" : e.loading ? "loading" : "idle";
}
function wo(e, t) {
  return (t ?? []).map((i) => ({ key: i.text?.key ?? "", text: q(e, "narrative", i.text) })).filter((i) => i.text !== "");
}
const _o = [
  { upTo: 2, level: "small", key: "uiEffortSmallLabel" },
  { upTo: 3, level: "medium", key: "uiEffortMediumLabel" },
  { upTo: Number.POSITIVE_INFINITY, level: "large", key: "uiEffortLargeLabel" }
];
function To(e, t) {
  if (typeof t != "number" || !Number.isFinite(t)) return null;
  const i = _o.find((s) => t <= s.upTo);
  return { level: i.level, label: o(e, i.key) };
}
function ko(e, t) {
  return (t ?? []).map((i) => ({
    kind: i.kind,
    title: q(e, "opportunity", i.text, "Title"),
    text: q(e, "opportunity", i.text, "Text"),
    effort: To(e, i.effort)
  })).filter((i) => i.title !== "" || i.text !== "");
}
function Co(e, t) {
  return {
    statements: wo(e, t.summary),
    targets: yo(e, t.targets),
    history: $o(e, t.targetHistory),
    movement: fo(e, t.movement),
    opportunities: ko(e, t.opportunities)
  };
}
const yi = 1e3;
function xr(e) {
  const t = (e ?? "").trim(), i = Math.max(0, t.length - yi);
  return { length: t.length, excess: i, over: i > 0, trimmed: t };
}
const De = 50, Mt = 100, So = /\r\n|\r|\n/;
function ji(e) {
  return (e ?? "").split(So).map((t) => t.trim()).filter((t) => t.length > 0);
}
const wr = (e) => e.split(/\s+/).filter(Boolean).join(" "), _r = { tidy: (e) => wr(e).toLowerCase(), key: (e) => e }, Eo = { tidy: wr, key: (e) => e.toLowerCase() };
function Lo(e, t = _r) {
  const i = /* @__PURE__ */ new Set(), s = [];
  let r = 0;
  for (const a of ji(e)) {
    const n = t.tidy(a), u = t.key(n);
    n.length === 0 || i.has(u) || (i.add(u), n.length > Mt ? r += 1 : s.push(n));
  }
  return { fitting: s, tooLong: r };
}
function Tr(e, t) {
  const { fitting: i, tooLong: s } = Lo(e, t);
  return { count: i.length, kept: Math.min(i.length, De), tooMany: i.length > De, tooLong: s };
}
function Po(e) {
  return Tr(e, _r);
}
function Do(e) {
  return Tr(e, Eo);
}
function kr(e) {
  return (e ?? []).join(`
`);
}
const Cr = "note-saved", $i = { saving: !1, saved: !1, revision: 0, problem: null, refused: !1 }, Fs = {
  period: "",
  analysis: null,
  loading: !1,
  problem: null,
  targetsStale: !1,
  terms: null,
  note: $i,
  targets: $i
};
class No {
  #a;
  #f;
  #l;
  #e = Fs;
  #i = !1;
  // Bumped by connect and disconnect: an awaited call that started under an earlier one is dropped when it returns.
  #s = 0;
  // Bumped by every read of an analysis and by disconnect: only the newest is used.
  #n = 0;
  // Bumped when the month changes (and by disconnect): a note save that began under an earlier one says nothing to the editor.
  #u = 0;
  // The month asked for: remembered while the model is not connected, and what Try again reads.
  #t = "";
  #d = /* @__PURE__ */ new Map();
  #o = null;
  #h = null;
  constructor(t, i, s) {
    this.#a = t, this.#f = i, this.#l = s;
  }
  get state() {
    return this.#e;
  }
  /** The target queries the editor shows: the list as the server kept it after a save, else the terms of the analysis. */
  get terms() {
    const t = this.#e.analysis;
    return this.#e.terms ?? $r(At(t) ? t.targets : null);
  }
  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------
  /** The element is on the page: reads the month it was given, if it holds nothing. */
  connect() {
    this.#i = !0, this.#s++, this.#t && this.#e.analysis === null && !this.#e.loading && this.#g(this.#t, !1);
  }
  /** The element has left the page: nothing that is on its way may touch anything, and nothing it held is kept. */
  disconnect() {
    this.#i = !1, this.#s++, this.#n++, this.#u++, this.#d.clear(), this.#e = Fs;
  }
  // ---- Reading -------------------------------------------------------------------------------------------------------------------
  /** The month shown has changed (or is shown for the first time). Resolves when the analysis of it is shown. */
  async show(t) {
    if (t === this.#t && (this.#e.analysis !== null || this.#e.loading) || (this.#t = t, this.#u++, !this.#i)) return;
    if (this.#p({ note: { ...$i, revision: this.#e.note.revision } }), !t) {
      this.#n++, this.#p({ period: "", analysis: null, loading: !1, problem: null, targetsStale: !1, terms: null });
      return;
    }
    const i = this.#d.get(t);
    if (i) {
      this.#n++, this.#p({ period: t, analysis: i, loading: !1, problem: null, targetsStale: !1, terms: null });
      return;
    }
    await this.#g(t, !1);
  }
  // Reads one month and shows it. `keep`: what is held stays on the screen meanwhile (the target queries changed: the cards stay and the part that
  // depends on the terms is marked as out of date); otherwise the month before is not shown while the next is read.
  async #g(t, i) {
    const s = ++this.#n, r = this.#s, a = () => this.#i && r === this.#s && s === this.#n;
    this.#p({ period: t, loading: !0, problem: null, analysis: i ? this.#e.analysis : null });
    try {
      const n = await this.#a.getAnalysis(t);
      if (!a()) return;
      if (At(n) && n.period !== t) {
        this.#p({ loading: !1, problem: new Ot(0, "unknown") });
        return;
      }
      this.#d.set(t, n), this.#p({ analysis: n, loading: !1, problem: null, targetsStale: !1, terms: null });
    } catch (n) {
      a() && this.#p({ loading: !1, problem: N(n) });
    }
  }
  /**
   * The archive has changed under the month shown (it was refreshed from Google, or a backfill added months): every analysis held was computed
   * from the archive as it was, so none is kept, and the month shown is read again. What is held stays on the screen meanwhile: it is the same
   * month, and the cards, and the keyboard's place in them, do not go away.
   */
  async invalidate() {
    this.#d.clear(), !(!this.#i || !this.#t) && await this.#g(this.#t, this.#e.analysis !== null);
  }
  /** "Try again" after a read that failed: reads the month again. */
  async retry() {
    !this.#i || !this.#t || !this.#e.problem || await this.#g(this.#t, this.#e.analysis !== null);
  }
  // ---- The month's note -----------------------------------------------------------------------------------------------------------
  /**
   * Saves the note of `period`, the month it was written for. A text over the limit is refused without a request. What the server kept (trimmed,
   * null when the text was blank) goes to the shell, which holds the note of the month shown; the editor says Saved only if the month is still
   * the one shown.
   */
  async saveNote(t, i) {
    if (!this.#i || this.#e.note.saving) return;
    const s = xr(i);
    if (s.over) {
      this.#m({ refused: !0, saved: !1, problem: null });
      return;
    }
    const r = this.#s, a = this.#u, n = () => this.#i && r === this.#s;
    this.#o = () => this.saveNote(t, i), this.#m({ saving: !0, saved: !1, refused: !1, problem: null });
    try {
      const u = await this.#a.putNote(t, s.trimmed);
      if (!n()) return;
      this.#l.noteSaved(t, u.note ?? null), a === this.#u && this.#m({ saving: !1, saved: !0, revision: this.#e.note.revision + 1 });
    } catch (u) {
      n() && a === this.#u && this.#m({ saving: !1, problem: N(u) });
    }
  }
  /** The text of the note was edited: what was said about the last save no longer applies. */
  noteEdited() {
    const { saved: t, refused: i, problem: s } = this.#e.note;
    (t || i || s) && this.#m({ saved: !1, refused: !1, problem: null });
  }
  /** "Try again" after a save that failed. */
  async retryNote() {
    this.#e.note.problem && await this.#o?.();
  }
  // ---- The target queries ------------------------------------------------------------------------------------------------------
  /**
   * Saves the target queries (the terms as typed, one per line; the server normalizes them), shows the list it kept, clears the analyses held and
   * reads the month shown NOW again: the list belongs to no month.
   */
  async saveTargets(t) {
    if (!this.#i || this.#e.targets.saving) return;
    const i = this.#s, s = () => this.#i && i === this.#s;
    this.#h = () => this.saveTargets(t), this.#r({ saving: !0, saved: !1, problem: null });
    let r;
    try {
      r = (await this.#a.putTargets(ji(t))).terms ?? [];
    } catch (a) {
      s() && this.#r({ saving: !1, problem: N(a) });
      return;
    }
    s() && (this.#d.clear(), this.#p({
      targets: { ...this.#e.targets, saving: !1, saved: !0, revision: this.#e.targets.revision + 1 },
      terms: r,
      targetsStale: !0
    }), this.#t ? await this.#g(this.#t, this.#e.analysis !== null) : this.#p({ targetsStale: !1 }));
  }
  /** The text of the target queries was edited: what was said about the last save no longer applies. */
  termsEdited() {
    const { saved: t, problem: i } = this.#e.targets;
    (t || i) && this.#r({ saved: !1, problem: null });
  }
  /** "Try again" after a save that failed. */
  async retryTargets() {
    this.#e.targets.problem && await this.#h?.();
  }
  // ---- Keeping the state --------------------------------------------------------------------------------------------------------------
  #m(t) {
    this.#p({ note: { ...this.#e.note, ...t } });
  }
  #r(t) {
    this.#p({ targets: { ...this.#e.targets, ...t } });
  }
  #p(t) {
    this.#e = { ...this.#e, ...t }, this.#f();
  }
}
const Ft = {
  analysis: "uiTabAnalysisLabel",
  traffic: "uiTabTrafficLabel",
  queries: "uiTabQueriesLabel",
  topPages: "uiTabTopPagesLabel",
  toFix: "uiTabToFixLabel",
  comparison: "uiTabComparisonLabel",
  settings: "uiTabSettingsLabel"
}, Sr = "esatto-seo-report-navigate";
function Ro(e, t) {
  e.dispatchEvent(new CustomEvent(Sr, { detail: { tab: t }, bubbles: !0, composed: !0 }));
}
const It = 16 * 1024, Oo = It + 1024;
function zo(e) {
  let t = 0;
  for (let i = 0; i < e.length; i++) {
    const s = e.charCodeAt(i);
    s < 128 ? t += 1 : s < 2048 ? t += 2 : s >= 55296 && s <= 56319 && (e.charCodeAt(i + 1) & 64512) === 56320 ? (t += 4, i++) : t += 3;
  }
  return t;
}
const Ao = (e) => typeof e == "object" && e !== null && !Array.isArray(e), Mo = (e) => typeof e == "string" && e.trim().length > 0, Fo = (e) => typeof e == "string" && e.includes("-----BEGIN") && e.includes("PRIVATE KEY-----");
function Io(e) {
  try {
    return JSON.parse(e.trim().replace(/,(\s*[}\]])/g, "$1"));
  } catch {
    return;
  }
}
function Ko(e) {
  if (!e || e.trim().length === 0) return { email: null, problem: null };
  if (e.length > It || zo(e) > It) return { email: null, problem: "tooLarge" };
  const t = Io(e);
  return Ao(t) ? t.type !== "service_account" ? { email: null, problem: "notServiceAccount" } : Mo(t.client_email) ? Fo(t.private_key) ? { email: t.client_email.trim(), problem: null } : { email: null, problem: "noPrivateKey" } : { email: null, problem: "noClientEmail" } : { email: null, problem: "notJson" };
}
async function Uo(e) {
  if (e.size > Oo) return { ok: !1, problem: "tooLarge" };
  try {
    return { ok: !0, text: await e.text() };
  } catch {
    return { ok: !1, problem: "unreadable" };
  }
}
const re = "sc-domain:", Is = 2048, Go = 253, Wo = 63, Bo = 65535, xi = (e) => e >= 9 && e <= 13 || e === 32 || e === 133 || e === 160 || e === 5760 || e >= 8192 && e <= 8202 || e === 8232 || e === 8233 || e === 8239 || e === 8287 || e === 12288, Ho = (e) => e <= 31 || e >= 127 && e <= 159, Vo = /^[\p{L}\p{Nd}]$/u;
function qo(e) {
  let t = 0, i = e.length;
  for (; t < i && xi(e.charCodeAt(t)); ) t++;
  for (; i > t && xi(e.charCodeAt(i - 1)); ) i--;
  return e.slice(t, i);
}
const Se = (e, t) => e.length >= t.length && e.slice(0, t.length).toUpperCase() === t.toUpperCase(), Er = (e) => Array.from(e.split(""), (t) => {
  const i = t.toLowerCase();
  return i.length === 1 ? i : i[0];
}).join(""), jo = (e) => e.length > 0 && e.length <= Wo && e[0] !== "-" && e[e.length - 1] !== "-" && e.split("").every((t) => t === "-" || Vo.test(t));
function Lr(e) {
  if (e.length === 0 || e.length > Go) return !1;
  const t = e.split(".");
  return t.length >= 2 && t.every(jo);
}
function Ks(e) {
  const t = e.endsWith("/") ? e.slice(0, -1) : e;
  return Lr(t) ? re + Er(t) : null;
}
function Yo(e) {
  const t = e.split(".");
  if (t.length > 4) return null;
  const i = [];
  for (const a of t)
    if (/^0[xX][0-9a-fA-F]+$/.test(a)) i.push(parseInt(a.slice(2), 16));
    else if (/^0[0-7]*$/.test(a)) i.push(a.length > 1 ? parseInt(a, 8) : 0);
    else if (/^[1-9][0-9]*$/.test(a)) i.push(parseInt(a, 10));
    else return null;
  const s = i[i.length - 1];
  if (i.slice(0, -1).some((a) => a > 255) || s > 256 ** (5 - i.length) - 1) return null;
  let r = s;
  return i.slice(0, -1).forEach((a, n) => r += a * 256 ** (3 - n)), [r / 2 ** 24, r / 2 ** 16, r / 2 ** 8, r].map((a) => Math.floor(a) % 256).join(".");
}
function Xo(e) {
  try {
    return new URL(`http://${e}/`).hostname;
  } catch {
    return null;
  }
}
const Kt = (e) => e !== void 0 && /^[0-9a-fA-F]$/.test(e), Zo = /^[A-Za-z0-9\-._~]$/, Jo = /* @__PURE__ */ new Set(['"', "<", ">", "`", "{", "}", "|", "^"]), Pr = (e) => `%${e.toString(16).toUpperCase().padStart(2, "0")}`, Qo = (e) => e >= 194 && e <= 223 ? 2 : e >= 224 && e <= 239 ? 3 : e >= 240 && e <= 244 ? 4 : 0;
function el(e, t, i) {
  const s = Qo(i);
  if (s === 0) return null;
  const r = [];
  for (let a = 0; a < s; a++) {
    const n = t + a * 3;
    if (e[n] !== "%" || !Kt(e[n + 1]) || !Kt(e[n + 2])) return null;
    r.push(parseInt(e.slice(n + 1, n + 3), 16));
  }
  try {
    new TextDecoder("utf-8", { fatal: !0 }).decode(new Uint8Array(r));
  } catch {
    return null;
  }
  return { text: r.map(Pr).join(""), length: s * 3 };
}
function tl(e) {
  let t = "";
  for (let i = 0; i < e.length; ) {
    const s = e[i];
    if (s === "%") {
      if (!Kt(e[i + 1]) || !Kt(e[i + 2])) {
        t += "%25", i += 1;
        continue;
      }
      const n = parseInt(e.slice(i + 1, i + 3), 16), u = n >= 128 ? el(e, i, n) : null;
      if (n < 128 && Zo.test(String.fromCharCode(n))) t += String.fromCharCode(n);
      else if (u) {
        t += u.text, i += u.length;
        continue;
      } else t += e.slice(i, i + 3);
      i += 3;
      continue;
    }
    const r = e.codePointAt(i), a = r > 65535 ? 2 : 1;
    if (r > 127)
      try {
        t += encodeURIComponent(String.fromCodePoint(r));
      } catch {
        return null;
      }
    else t += Jo.has(s) ? Pr(r) : s;
    i += a;
  }
  return t;
}
function il(e) {
  let t = e;
  const i = [];
  for (; t.length > 0; )
    if (t.startsWith("../")) t = t.slice(3);
    else if (t.startsWith("./")) t = t.slice(2);
    else if (t.startsWith("/./")) t = t.slice(2);
    else if (t === "/.") t = "/";
    else if (t.startsWith("/../"))
      t = t.slice(3), i.pop();
    else if (t === "/..")
      t = "/", i.pop();
    else if (t === "." || t === "..") t = "";
    else {
      const s = t.indexOf("/", t.startsWith("/") ? 1 : 0), r = s < 0 ? t : t.slice(0, s);
      i.push(r), t = t.slice(r.length);
    }
  return i.join("");
}
function sl(e, t) {
  let i, s = null, r = "";
  if (e.startsWith("[")) {
    const n = e.indexOf("]");
    if (n < 0) return null;
    const u = e.slice(n + 1);
    u.startsWith(":") ? s = u.slice(1) : u !== "" && (r = `/${u}`);
    const d = Xo(e.slice(0, n + 1));
    if (!d) return null;
    i = d;
  } else {
    const n = e.lastIndexOf(":"), u = Er(n < 0 ? e : e.slice(0, n));
    n >= 0 && (s = e.slice(n + 1));
    const d = Yo(u);
    if (!d && !Lr(u)) return null;
    i = d ?? u;
  }
  if (s === null || s === "") return { authority: i, pathPrefix: r };
  if (!/^[0-9]+$/.test(s) || Number(s) > Bo) return null;
  const a = Number(s);
  return { authority: a === t ? i : `${i}:${a}`, pathPrefix: r };
}
function rl(e) {
  if (e.includes("?") || e.includes("#")) return null;
  const t = e.indexOf("://"), i = e.slice(0, t).toLowerCase(), s = e.slice(t + 3), r = s.indexOf("/"), a = r < 0 ? s : s.slice(0, r);
  if (a.includes("@")) return null;
  const n = sl(a, i === "https" ? 443 : 80);
  if (!n) return null;
  const u = tl(n.pathPrefix + (r < 0 ? "" : s.slice(r)).replace(/\\/g, "/"));
  if (u === null) return null;
  const d = il(u || "/") || "/";
  return `${i}://${n.authority}${d.endsWith("/") ? d : `${d}/`}`;
}
function Dr(e) {
  const t = qo(e ?? "");
  if (t.length === 0 || t.length > Is) return null;
  for (let s = 0; s < t.length; s++) {
    const r = t.charCodeAt(s);
    if (xi(r) || Ho(r)) return null;
  }
  const i = Se(t, re) ? Ks(t.slice(re.length)) : Se(t, "http://") || Se(t, "https://") ? rl(t) : Ks(t);
  return i !== null && i.length <= Is ? i : null;
}
function al(e, t) {
  const i = (t ?? "").trim();
  return i ? e === "domain" && !Se(i, re) ? re + i : i : "";
}
function Nr(e, t) {
  const i = al(e, t);
  return i === "" ? "" : Dr(i) ?? i;
}
function nl(e) {
  const t = (e ?? "").trim();
  return t ? Se(t, re) ? { kind: "domain", text: t.slice(re.length) } : { kind: "prefix", text: t } : { kind: "domain", text: "" };
}
function ol(e, t) {
  const i = (t ?? "").trim();
  if (!i) return null;
  const s = /^https?:\/\//i.test(i);
  return e === "domain" ? s ? "looksLikeUrl" : null : s || Se(i, re) ? null : "missingScheme";
}
function ll(e, t) {
  const i = Nr(e, t);
  if (!i) return { sent: "", stored: null, warning: null, line: "none" };
  const s = Dr(i), r = ol(e, t);
  return { sent: i, stored: s, warning: r, line: s === null ? "refused" : r ? "warning" : "preview" };
}
const cl = {
  ga4PropertyId: "ga4PropertyId",
  searchConsoleKind: "searchConsoleSite",
  searchConsoleText: "searchConsoleSite",
  brandTerms: "brandTerms",
  displayName: "displayName",
  findingsUserGroups: "findingsUserGroups",
  autoFetch: "autoFetch"
};
function $t(e) {
  const t = nl(e.searchConsoleSite);
  return {
    ga4PropertyId: e.ga4PropertyId ?? "",
    searchConsoleKind: t.kind,
    searchConsoleText: t.text,
    brandTerms: kr(e.brandTerms),
    displayName: e.displayName ?? "",
    findingsUserGroups: [...e.findingsUserGroups ?? []],
    autoFetch: e.autoFetch
  };
}
function Yi(e, t) {
  return e.some((i) => i.toLowerCase() === t.toLowerCase());
}
const ai = (e) => e.trim() === "" ? null : e.trim();
function Rr(e, t, i) {
  const s = (r, a) => Yi(i, r) ? t[r] : a;
  return {
    ga4PropertyId: s("ga4PropertyId", ai(e.ga4PropertyId)),
    searchConsoleSite: s("searchConsoleSite", ai(Nr(e.searchConsoleKind, e.searchConsoleText))),
    brandTerms: s("brandTerms", ji(e.brandTerms)),
    displayName: s("displayName", ai(e.displayName)),
    findingsUserGroups: s("findingsUserGroups", [...e.findingsUserGroups]),
    autoFetch: s("autoFetch", e.autoFetch)
  };
}
function ul(e, t) {
  return e.ga4PropertyId === t.ga4PropertyId && e.searchConsoleKind === t.searchConsoleKind && e.searchConsoleText === t.searchConsoleText && e.brandTerms === t.brandTerms && e.displayName === t.displayName && e.autoFetch === t.autoFetch && e.findingsUserGroups.length === t.findingsUserGroups.length && e.findingsUserGroups.every((i, s) => i === t.findingsUserGroups[s]);
}
function dl(e, t, i) {
  const s = Yi(e, t);
  return { locked: s, readonly: s || i };
}
function hl(e, t, i, s) {
  const r = Rr(e, t, i), a = { ga4PropertyId: r.ga4PropertyId, searchConsoleSite: r.searchConsoleSite };
  return s && s.trim().length > 0 && (a.serviceAccountJson = s), a;
}
function pl(e, t) {
  return e.source === "configuration" ? e.unreadable ? "configuredBroken" : "configured" : e.unreadable ? "unreadable" : e.hasKey ? t ? "replacing" : "saved" : "none";
}
function Xi(e) {
  return e === "none" || e === "replacing" || e === "unreadable";
}
const fl = ["none", "configuration", "database"], ni = (e) => Array.isArray(e) ? e.filter((t) => typeof t == "string") : [], xt = (e) => typeof e == "string" ? e : null;
function oi(e) {
  const t = e.settings ?? {}, i = e.key ?? {};
  return {
    settings: {
      ga4PropertyId: xt(t.ga4PropertyId),
      searchConsoleSite: xt(t.searchConsoleSite),
      brandTerms: ni(t.brandTerms),
      displayName: xt(t.displayName),
      findingsUserGroups: ni(t.findingsUserGroups),
      autoFetch: t.autoFetch !== !1
    },
    locked: ni(e.locked),
    key: {
      hasKey: i.hasKey === !0,
      source: fl.includes(i.source) ? i.source : "none",
      serviceAccountEmail: xt(i.serviceAccountEmail),
      unreadable: i.unreadable === !0
    }
  };
}
function gl(e, t) {
  const i = new Set(t.map((n) => n.toLowerCase())), s = e.map((n) => ({ alias: n.alias, name: n.name, selected: i.has(n.alias.toLowerCase()), known: !0 })), r = new Set(e.map((n) => n.alias.toLowerCase())), a = t.filter((n) => !r.has(n.toLowerCase()));
  return [...s, ...a.map((n) => ({ alias: n, name: n, selected: !0, known: !1 }))];
}
const ml = {
  passed: { tone: "good", icon: "icon-check", statusKey: "uiTestPassedLabel" },
  noData: { tone: "notice", icon: "icon-info", statusKey: "uiTestNoDataLabel" },
  notTested: { tone: "none", icon: "icon-block", statusKey: "uiTestNotTestedLabel" },
  failed: { tone: "critical", icon: "icon-alert", statusKey: "uiTestFailedLabel" }
}, vl = [
  { id: "key", labelKey: "uiTestKeyLabel" },
  { id: "ga4", labelKey: "uiTestGa4Label" },
  { id: "searchConsole", labelKey: "uiTestSearchConsoleLabel" }
];
function bl(e) {
  return e.ok ? e.errorCode === "noData" ? "noData" : "passed" : e.errorCode ? "failed" : "notTested";
}
function yl(e, t, i) {
  const s = t.serviceAccountEmail ?? i;
  return vl.map(({ id: r, labelKey: a }) => {
    const n = t[r], u = bl(n), d = ml[u], _ = { ...n.args ?? {}, email: s }, I = u === "failed" || u === "noData" ? le(e, n.errorCode, _) : { title: u === "notTested" ? o(e, "uiTestNotTestedText") : "", text: "" };
    return { id: r, label: o(e, a), kind: u, tone: d.tone, icon: d.icon, status: o(e, d.statusKey), ...I };
  });
}
const Or = "fetch-started", Ce = { busy: !1, done: !1, problem: null }, zr = { busy: !1, result: null, problem: null }, $l = ["ga4PropertyId", "searchConsoleKind", "searchConsoleText"], Us = {
  loading: !1,
  loadProblem: null,
  saved: null,
  form: null,
  groups: null,
  groupsLoading: !1,
  groupsProblem: null,
  replacing: !1,
  confirmingRemove: !1,
  hasDraftKey: !1,
  draftEmail: null,
  draftProblem: null,
  draftRevision: 0,
  save: Ce,
  remove: Ce,
  test: zr,
  fetch: { busy: !1, alreadyRunning: !1, problem: null }
}, Gs = { hasDraftKey: !1, draftEmail: null, draftProblem: null };
class xl {
  #a;
  #f;
  #l;
  #e = Us;
  #i = !1;
  // Bumped by connect and disconnect: an awaited call that started under an earlier one is dropped when it returns.
  #s = 0;
  // THE ONLY PLACE THE ENTERED KEY IS HELD. See the file's header.
  #n = null;
  // Counts the changes of what a connection test reads (the GA4 ID, the Search Console property, the key): a result belongs to the count it was
  // started under, and is dropped (or cleared) once the count has moved on. See #connectionChanged.
  #u = 0;
  constructor(t, i, s) {
    this.#a = t, this.#f = i, this.#l = s;
  }
  get state() {
    return this.#e;
  }
  /** What the key field shows (settings-view.ts, keyModeOf); `none` until the settings have been read. */
  get keyMode() {
    const t = this.#e.saved;
    return t ? pl(t.key, this.#e.replacing) : "none";
  }
  /** The form differs from what is saved, or a key was entered. */
  get dirty() {
    const { saved: t, form: i, hasDraftKey: s } = this.#e;
    return s || t !== null && i !== null && !ul(i, $t(t.settings));
  }
  /** Whether an entered key is held at all: what the tests check after the key should have been forgotten. Never the key itself. */
  get holdsDraftKey() {
    return this.#n !== null;
  }
  /**
   * Whether the person is in the middle of something here: an action on its way, unsaved changes or an entered key, Replace or a removal waiting
   * for its confirmation. The tab tells the shell, which then keeps the tab open whatever the status does next.
   */
  get engaged() {
    const { save: t, remove: i, test: s, fetch: r, confirmingRemove: a, replacing: n } = this.#e;
    return t.busy || i.busy || s.busy || r.busy || a || n || this.dirty;
  }
  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------
  /** The element is on the page: reads the settings and the user groups, unless they are held already. Resolves when both are read. */
  async connect() {
    const t = this.#i;
    this.#i = !0, t || this.#s++, !(this.#e.saved !== null || this.#e.loading) && await Promise.all([this.#d(), this.#o()]);
  }
  /** The element has left the page: the key is forgotten, nothing that is on its way may touch anything, and nothing it held is kept. */
  disconnect() {
    this.#i = !1, this.#s++, this.#n = null, this.#e = Us;
  }
  #t(t) {
    return this.#i && t === this.#s;
  }
  // ---- Reading -------------------------------------------------------------------------------------------------------------------
  async #d() {
    const t = this.#s;
    this.#r({ loading: !0, loadProblem: null });
    try {
      const i = oi(await this.#a.getSettings());
      this.#t(t) && this.#r({ loading: !1, saved: i, form: $t(i.settings) });
    } catch (i) {
      this.#t(t) && this.#r({ loading: !1, loadProblem: N(i) });
    }
  }
  async #o() {
    const t = this.#s;
    this.#r({ groupsLoading: !0, groupsProblem: null });
    try {
      const i = await this.#a.getUserGroups();
      this.#t(t) && this.#r({ groupsLoading: !1, groups: Array.isArray(i) ? i : [] });
    } catch (i) {
      this.#t(t) && this.#r({ groupsLoading: !1, groupsProblem: N(i) });
    }
  }
  /** "Try again" after the settings could not be read. */
  async retryLoad() {
    this.#i && this.#e.loadProblem && await this.#d();
  }
  /** "Try again" after the user groups could not be read. */
  async retryGroups() {
    this.#i && this.#e.groupsProblem && await this.#o();
  }
  // ---- Editing -------------------------------------------------------------------------------------------------------------------
  #h() {
    return this.#e.save.busy || this.#e.remove.busy;
  }
  /** Changes values of the form. A value of a field configuration locks is ignored, and so is every edit while a save is on its way. */
  edit(t) {
    const { form: i, saved: s } = this.#e;
    if (!i || !s || this.#h()) return;
    const r = Object.fromEntries(
      Object.entries(t).filter(([u]) => {
        const d = cl[u];
        return d !== void 0 && !Yi(s.locked, d);
      })
    );
    if (Object.keys(r).length === 0) return;
    const a = { ...i, ...r }, n = $l.some((u) => a[u] !== i[u]) ? this.#g() : {};
    this.#r({ form: a, save: { ...this.#e.save, done: !1 }, ...n });
  }
  /**
   * What a connection test reads has changed: a result shown is about values that are no longer on the screen, and a test on its way will answer
   * for them. The result is cleared, a test on its way is ended (its answer is dropped when it comes: see test()), and the edited values can be
   * tested at once.
   */
  #g() {
    return this.#u++, { test: zr };
  }
  /**
   * What is in the key box (typed, pasted or read from a file). Held privately; the state says only that there is one, whose it looks like and
   * what is wrong with it. Ignored where the field has no box (a saved key not being replaced, a configured key) and while a save is on its way.
   */
  setDraftKey(t) {
    if (!this.#e.saved || this.#h() || !Xi(this.keyMode)) return;
    if (t.trim().length === 0) {
      this.#n = null, this.#r({ ...Gs, ...this.#g() });
      return;
    }
    this.#n = t;
    const i = Ko(t);
    this.#r({
      hasDraftKey: !0,
      draftEmail: i.email,
      draftProblem: i.problem,
      save: { ...this.#e.save, done: !1 },
      ...this.#g()
    });
  }
  // Forgets the entered key, and has the key field empty its box. The key a test would use changes with it.
  #m() {
    return this.#n = null, { ...Gs, draftRevision: this.#e.draftRevision + 1, ...this.#g() };
  }
  /** Replace: the box opens beside the saved key, which stays until a new one is saved. */
  replaceKey() {
    this.keyMode === "saved" && !this.#h() && this.#r({ replacing: !0, confirmingRemove: !1, ...this.#g() });
  }
  /** Cancel after Replace: what was entered is forgotten. */
  cancelReplace() {
    this.#e.replacing && !this.#h() && this.#r({ replacing: !1, ...this.#m() });
  }
  /** Discard changes: the form as it is saved, the key box empty. */
  revert() {
    const t = this.#e.saved;
    !t || this.#h() || this.#r({
      form: $t(t.settings),
      replacing: !1,
      confirmingRemove: !1,
      save: Ce,
      remove: Ce,
      ...this.#m()
    });
  }
  // ---- Save -----------------------------------------------------------------------------------------------------------------------
  /**
   * Saves the form and the entered key (none: the key stays as it is). The server checks the connection with what it saved before it answers,
   * which can take up to a minute, and may start the backfill; the answer is the settings in force, and the form is rebuilt from it. A failure
   * keeps everything that was typed, the key included. "Try again" is Save again.
   */
  async save() {
    const { saved: t, form: i } = this.#e;
    if (!t || !i || this.#h()) return;
    const s = this.#s, r = { settings: Rr(i, t.settings, t.locked), serviceAccountJson: this.#n, removeKey: !1 };
    this.#r({ save: { busy: !0, done: !1, problem: null }, remove: Ce });
    try {
      const a = oi(await this.#a.putSettings(r));
      if (!this.#t(s)) return this.#l.saved();
      this.#r({
        saved: a,
        form: $t(a.settings),
        replacing: !1,
        confirmingRemove: !1,
        save: { busy: !1, done: !0, problem: null },
        ...this.#m()
      }), this.#l.saved();
    } catch (a) {
      this.#t(s) && this.#r({ save: { busy: !1, done: !1, problem: N(a) } });
    }
  }
  // ---- Remove ---------------------------------------------------------------------------------------------------------------------
  /** Remove: asks for a confirmation first. Only a key saved here can be removed. */
  askRemove() {
    this.#e.saved?.key.source === "database" && !this.#h() && this.#r({ confirmingRemove: !0, replacing: !1, remove: Ce });
  }
  cancelRemove() {
    this.#e.confirmingRemove && this.#r({ confirmingRemove: !1 });
  }
  /**
   * Removes the saved key, once it is confirmed. The settings are sent as they are SAVED, not as the form holds them: Remove removes the key and
   * nothing else, and what is being typed stays in the form, unsaved.
   */
  async removeKey() {
    const t = this.#e.saved;
    if (!t || !this.#e.confirmingRemove || this.#h()) return;
    const i = this.#s;
    this.#r({ remove: { busy: !0, done: !1, problem: null }, save: { ...this.#e.save, done: !1 } });
    try {
      const s = oi(await this.#a.putSettings({ settings: t.settings, serviceAccountJson: null, removeKey: !0 }));
      if (!this.#t(i)) return this.#l.saved();
      this.#r({ saved: s, replacing: !1, confirmingRemove: !1, remove: { busy: !1, done: !0, problem: null }, ...this.#m() }), this.#l.saved();
    } catch (s) {
      this.#t(i) && this.#r({ remove: { busy: !1, done: !1, problem: N(s) } });
    }
  }
  // ---- Test connection ------------------------------------------------------------------------------------------------------------
  /**
   * Tests the values in the form, before they are saved: the entered key when there is one (it stays in the box), the key in force when the box is
   * empty. The result before is forgotten while the next is on its way, so its arrival is news. An answer for values that have changed since the
   * test started (the GA4 ID, the Search Console property, the key: #connectionChanged) is about nothing on the screen and is dropped.
   */
  async test() {
    const { saved: t, form: i } = this.#e;
    if (!t || !i || this.#e.test.busy) return;
    const s = this.#s, r = this.#u, a = () => this.#t(s) && r === this.#u, n = hl(i, t.settings, t.locked, this.#n);
    this.#r({ test: { busy: !0, result: null, problem: null } });
    try {
      const u = await this.#a.testConnection(n);
      a() && this.#r({ test: { busy: !1, result: u, problem: null } });
    } catch (u) {
      a() && this.#r({ test: { busy: !1, result: null, problem: N(u) } });
    }
  }
  // ---- Fetch now ------------------------------------------------------------------------------------------------------------------
  /**
   * Starts the backfill of the missing months and hands the run to the shell, which follows it. A run that is going already (409 alreadyRunning) is
   * information, and the shell follows that one; any other refusal (nothing set up, a key that cannot be read) is a problem.
   */
  async fetchNow() {
    if (!this.#e.saved || this.#e.fetch.busy) return;
    const t = this.#s;
    this.#r({ fetch: { busy: !0, alreadyRunning: !1, problem: null } });
    try {
      const i = await this.#a.startFetch();
      if (!this.#t(t)) return;
      this.#r({ fetch: { busy: !1, alreadyRunning: !1, problem: null } }), this.#l.fetchStarted(i.progress);
    } catch (i) {
      if (!this.#t(t)) return;
      const s = N(i);
      s.code === "alreadyRunning" ? (this.#r({ fetch: { busy: !1, alreadyRunning: !0, problem: null } }), this.#l.fetchStarted(null)) : this.#r({ fetch: { busy: !1, alreadyRunning: !1, problem: s } });
    }
  }
  // ---- Keeping the state ----------------------------------------------------------------------------------------------------------
  #r(t) {
    this.#e = { ...this.#e, ...t }, this.#f();
  }
}
var wl = Object.defineProperty, _l = Object.getOwnPropertyDescriptor, Ar = (e) => {
  throw TypeError(e);
}, Zi = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? _l(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && wl(t, i, r), r;
}, Mr = (e, t, i) => t.has(e) || Ar("Cannot " + i), Tl = (e, t, i) => (Mr(e, t, "read from private field"), i ? i.call(e) : t.get(e)), Ws = (e, t, i) => t.has(e) ? Ar("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Bs = (e, t, i) => (Mr(e, t, "access private method"), i), Ji, St, Fr, Ir;
let tt = class extends k {
  constructor() {
    super(...arguments), Ws(this, St), this.fetch = null, this.busy = !1, Ws(this, Ji, () => this.dispatchEvent(new CustomEvent("fetch-now")));
  }
  render() {
    if (!this.fetch) return c;
    const e = or(this.fetch);
    return e.phase === "idle" ? Bs(this, St, Ir).call(this, this.fetch) : Bs(this, St, Fr).call(this, e);
  }
};
Ji = /* @__PURE__ */ new WeakMap();
St = /* @__PURE__ */ new WeakSet();
Fr = function(e) {
  const t = e.phase === "starting";
  return l`
      <section class="card tone-notice" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-sync"></uui-icon></span>
          <h3 id="title">${o(this, "uiProgressTitle")}</h3>
        </div>
        <div class="stack" role="status" aria-live="polite">
          <p>
            ${t ? o(this, "uiProgressStartingText") : o(this, "uiProgressText", { done: e.done, total: e.total, period: e.currentPeriod })}
          </p>
          <progress
            max="100"
            value=${e.percent ?? c}
            aria-label=${o(this, "uiProgressTitle")}
          ></progress>
          <p class="muted small">${o(this, "uiProgressHintText")}</p>
        </div>
      </section>
    `;
};
Ir = function(e) {
  const t = lr(e), i = t ? le(this, t) : null;
  return l`
      <section class="card" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge solid round tone-none" aria-hidden="true"><uui-icon name="icon-cloud-download"></uui-icon></span>
          <h3 id="title">${o(this, "uiNotFetchedTitle")}</h3>
        </div>
        <div class="stack">
          <p>${o(this, "uiNotFetchedText")}</p>
          ${i ? l`<div class="banner tone-warning" role="status">
                <div>
                  <h4>${o(this, "uiFetchFailedTitle")}</h4>
                  <p><strong>${i.title}</strong> ${i.text}</p>
                </div>
              </div>` : c}
          <uui-button look="primary" label=${o(this, "uiFetchNowLabel")} ?disabled=${this.busy} @click=${Tl(this, Ji)}>
            ${o(this, "uiFetchNowLabel")}
          </uui-button>
        </div>
      </section>
    `;
};
tt.styles = [
  C,
  w`
      progress { width: 100%; height: 10px; accent-color: var(--uui-color-interactive); }
      h4 { font-size: 14px; font-weight: 700; }
    `
];
Zi([
  h({ attribute: !1 })
], tt.prototype, "fetch", 2);
Zi([
  h({ type: Boolean })
], tt.prototype, "busy", 2);
tt = Zi([
  T("esatto-seo-report-fetch-card")
], tt);
var kl = Object.defineProperty, Cl = Object.getOwnPropertyDescriptor, Kr = (e) => {
  throw TypeError(e);
}, Qi = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Cl(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && kl(t, i, r), r;
}, Sl = (e, t, i) => t.has(e) || Kr("Cannot " + i), El = (e, t, i) => t.has(e) ? Kr("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Ll = (e, t, i) => (Sl(e, t, "access private method"), i), wi, Ur;
const Hs = {
  key: "uiMissingKeyLabel",
  ga4PropertyId: "uiMissingGa4PropertyIdLabel",
  searchConsoleSite: "uiMissingSearchConsoleSiteLabel"
};
let it = class extends k {
  constructor() {
    super(...arguments), El(this, wi), this.status = null;
  }
  render() {
    if (!this.status) return c;
    const { title: e, text: t } = le(this, this.status.setup === "keyUnreadable" ? "keyUnreadable" : "notConfigured"), i = this.status.missing.filter((s) => s in Hs);
    return l`
      <section class="card tone-notice" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-link"></uui-icon></span>
          <h3 id="title">${e}</h3>
        </div>
        <div class="stack">
          <p>${t}</p>
          ${i.length > 0 ? l`
                <div>
                  <p id="missing" class="muted small">${o(this, "uiMissingLabel")}</p>
                  <ul aria-labelledby="missing">
                    ${i.map((s) => l`<li>${o(this, Hs[s])}</li>`)}
                  </ul>
                </div>
              ` : c}
          ${Ll(this, wi, Ur).call(this)}
        </div>
      </section>
    `;
  }
};
wi = /* @__PURE__ */ new WeakSet();
Ur = function() {
  return this.canOpenSettings === !0 ? l`<uui-button look="primary" label=${o(this, Ft.settings)} @click=${() => Ro(this, "settings")}>
        ${o(this, Ft.settings)}
      </uui-button>` : l`<p class="muted">${o(this, this.canOpenSettings === !1 ? "uiAskAdministratorText" : "uiFindSettingsText")}</p>`;
};
it.styles = [C, w`ul { margin: var(--uui-size-space-2) 0 0; padding-left: var(--uui-size-space-5); }`];
Qi([
  h({ attribute: !1 })
], it.prototype, "status", 2);
Qi([
  h({ attribute: !1 })
], it.prototype, "canOpenSettings", 2);
it = Qi([
  T("esatto-seo-report-not-connected")
], it);
function qt(e) {
  if (!e) return c;
  const { pill: t, note: i } = e;
  return l`
    <span class="trend">
      ${t ? l`<span class="pill ${t.direction}"
            ><span aria-hidden="true">${t.arrow}</span><span class="visually-hidden">${t.words} </span>${t.figure}</span
          >` : c}
      ${i ? l`<span class="muted">${i}</span>` : c}
    </span>
  `;
}
const jt = w`
  .trend { font-size: 0.8rem; display: inline-flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: var(--uui-size-space-2); }
  .pill {
    display: inline-flex; gap: 4px; align-items: center; padding: 1px 8px; border-radius: 999px; font-weight: 700; white-space: nowrap;
    font-variant-numeric: tabular-nums; color: var(--uui-color-text);
    background: var(--seo-wash); border: 1px solid var(--seo-line);
  }
  .pill.up { background: color-mix(in srgb, var(--seo-good) 14%, var(--uui-color-surface)); border-color: color-mix(in srgb, var(--seo-good) 45%, var(--seo-line)); }
  .pill.down { background: color-mix(in srgb, var(--seo-critical) 14%, var(--uui-color-surface)); border-color: color-mix(in srgb, var(--seo-critical) 45%, var(--seo-line)); }
`, Pl = {
  organic_sessions: "icon-search",
  organic_engagement_rate: "icon-hand-pointer",
  organic_session_seconds: "icon-time",
  organic_key_events: "icon-message",
  total_sessions: "icon-users",
  gsc_clicks: "icon-mouse-cursor",
  clicks_vs_sessions: "icon-split"
}, Dl = (e) => Pl[e] ?? "icon-chart", Gr = (e) => e === "up" ? "good" : e === "down" ? "critical" : "notice";
function Yt(e, t) {
  const i = In(t), s = q(e, "trend", t?.text);
  if (!i && !s) return null;
  const r = i?.direction === "up" ? "uiTrendUpLabel" : i?.direction === "down" ? "uiTrendDownLabel" : "uiTrendFlatLabel";
  return {
    pill: i ? {
      direction: i.direction,
      arrow: i.arrow,
      words: o(e, r),
      // "flat" has no direction of its own and keeps its sign.
      figure: Ke(i.change, L(e), { fixed: !0, signed: i.direction === "flat" })
    } : null,
    note: s
  };
}
function Nl(e, t) {
  const i = dt(t.value, t.format, L(e)), s = Yt(e, t.trend);
  return {
    label: j(e, "kpi", t.key, "Label"),
    description: j(e, "kpi", t.key, "Description"),
    value: i || o(e, "uiNoValueText"),
    hasValue: i !== "",
    icon: Dl(t.key),
    tone: Gr(s?.pill?.direction),
    trend: s
  };
}
var Rl = Object.defineProperty, Ol = Object.getOwnPropertyDescriptor, Wr = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Ol(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Rl(t, i, r), r;
};
let Ut = class extends k {
  constructor() {
    super(...arguments), this.kpi = null;
  }
  render() {
    if (!this.kpi) return c;
    const e = Nl(this, this.kpi);
    return l`
      <div class="tile tone-${e.tone}">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${e.icon}></uui-icon></span>
        <div class="tile-text">
          <span class="tile-value ${e.hasValue ? "" : "none"}">${e.value}</span>
          <span class="tile-label">${e.label}</span>
          ${e.description ? l`<span class="tile-desc">${e.description}</span>` : c}
          ${qt(e.trend)}
        </div>
      </div>
    `;
  }
};
Ut.styles = [
  C,
  jt,
  w`
      /* The tile fills its grid cell: the host is the cell's content, and the percentage below needs a definite height to resolve against. */
      :host { display: block; height: 100%; }
      .tile {
        display: flex; gap: var(--uui-size-space-4); align-items: flex-start; padding: var(--uui-size-space-4) var(--uui-size-space-5);
        height: 100%; box-sizing: border-box;
        border-radius: var(--seo-radius); border: 1px solid color-mix(in srgb, var(--tone) 35%, var(--seo-line));
        background: linear-gradient(135deg, color-mix(in srgb, var(--tone) 14%, var(--uui-color-surface)), var(--uui-color-surface) 78%);
        box-shadow: var(--uui-shadow-depth-1);
      }
      .tile .icon-badge { width: 44px; height: 44px; font-size: 22px; box-shadow: 0 6px 14px -6px var(--tone); }
      .tile-text { display: grid; gap: 2px; flex: 1; min-width: 0; justify-items: start; }
      .tile-value { font-size: 30px; font-weight: 800; line-height: 1.1; letter-spacing: -0.02em; font-variant-numeric: tabular-nums; }
      .tile-value.none { font-size: 18px; font-weight: 600; color: var(--seo-muted); letter-spacing: 0; }
      .tile-label { font-size: 13px; font-weight: 600; color: var(--seo-muted); }
      .tile-desc { font-size: 0.75rem; line-height: 1.35; color: var(--seo-muted); }
    `
];
Wr([
  h({ attribute: !1 })
], Ut.prototype, "kpi", 2);
Ut = Wr([
  T("esatto-seo-report-kpi-tile")
], Ut);
var zl = Object.defineProperty, Al = Object.getOwnPropertyDescriptor, Br = (e) => {
  throw TypeError(e);
}, Ue = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Al(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && zl(t, i, r), r;
}, Ml = (e, t, i) => t.has(e) || Br("Cannot " + i), de = (e, t, i) => (Ml(e, t, "read from private field"), i ? i.call(e) : t.get(e)), li = (e, t, i) => t.has(e) ? Br("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), he, Te, _i, Ti;
let ae = class extends k {
  constructor() {
    super(...arguments), li(this, he), this.periods = [], this.period = "", this.fetchedUtc = null, this.refreshing = !1, this.busy = !1, li(this, _i, (e) => {
      const t = e.target;
      if (!cr(de(this, he, Te), this.period, t.value, this.periods)) {
        t.value = this.period;
        return;
      }
      this.dispatchEvent(new CustomEvent("period-change", { detail: t.value, bubbles: !0, composed: !0 }));
    }), li(this, Ti, () => {
      de(this, he, Te) || this.dispatchEvent(new CustomEvent("refresh", { bubbles: !0, composed: !0 }));
    });
  }
  render() {
    const e = L(this), t = this.periods.length > 1, i = uo(this.fetchedUtc, e), s = ho(this.fetchedUtc, e) || i;
    return l`
      <div class="bar">
        ${t ? l`<div class="field">
              <label for="month">${o(this, "uiMonthPickerLabel")}</label>
              <select id="month" aria-disabled=${de(this, he, Te) ? "true" : "false"} aria-busy=${de(this, he, Te) ? "true" : "false"} @change=${de(this, _i)}>
                ${this.periods.map(
      // live(): compared with what the list shows now, not with what was bound last, so it follows the shell after a month was refused.
      (r) => l`<option value=${r} .selected=${W(r === this.period)}>${et(r, e)}</option>`
    )}
              </select>
            </div>` : c}
        <ul class="meta">
          ${!t && this.period ? l`<li class="chip"><uui-icon name="icon-calendar" aria-hidden="true"></uui-icon>${o(this, "uiReportPeriodText", { period: this.period })}</li>` : c}
          ${s ? l`<li class="chip" title=${i}>
                <uui-icon name="icon-time" aria-hidden="true"></uui-icon>${o(this, "uiFetchedText", { when: s })}
              </li>` : c}
        </ul>
        <button
          type="button"
          class="refresh"
          aria-disabled=${de(this, he, Te) ? "true" : "false"}
          aria-busy=${this.refreshing ? "true" : "false"}
          @click=${de(this, Ti)}
        >
          ${this.refreshing ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : c}${o(this, "uiRefreshLabel")}
        </button>
      </div>
      ${this.refreshing ? l`<p class="muted small" role="status" aria-live="polite">${o(this, "uiRefreshingText", { period: this.period })}</p>` : c}
    `;
  }
};
he = /* @__PURE__ */ new WeakSet();
Te = function() {
  return this.busy || this.refreshing;
};
_i = /* @__PURE__ */ new WeakMap();
Ti = /* @__PURE__ */ new WeakMap();
ae.styles = [
  C,
  w`
      .bar { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: end; }
      .meta { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2); align-items: center; }
      select { min-width: 12rem; }
      select[aria-disabled='true'] { opacity: 0.65; cursor: progress; }

      /* A real button, so that aria-disabled and aria-busy mean something (uui-button hands neither to its inner button), in the look of
         the backoffice's outline button. The spinner is how busy is SHOWN: never colour alone. */
      .refresh {
        display: inline-flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; box-sizing: border-box;
        padding: 0 var(--uui-size-space-5); font: inherit; font-weight: 700; cursor: pointer;
        border: 1px solid var(--uui-color-interactive); border-radius: var(--seo-radius-sm);
        background: transparent; color: var(--uui-color-interactive);
        transition: background-color var(--seo-ease), color var(--seo-ease);
      }
      .refresh:hover { background: var(--uui-color-surface-emphasis); color: var(--uui-color-interactive-emphasis); }
      .refresh[aria-disabled='true'] { cursor: progress; opacity: 0.8; }
      .refresh[aria-disabled='true']:hover { background: transparent; color: var(--uui-color-interactive); }
      .refresh uui-loader-circle { font-size: 16px; }
    `
];
Ue([
  h({ attribute: !1 })
], ae.prototype, "periods", 2);
Ue([
  h()
], ae.prototype, "period", 2);
Ue([
  h({ attribute: !1 })
], ae.prototype, "fetchedUtc", 2);
Ue([
  h({ type: Boolean })
], ae.prototype, "refreshing", 2);
Ue([
  h({ type: Boolean })
], ae.prototype, "busy", 2);
ae = Ue([
  T("esatto-seo-report-period-bar")
], ae);
var Fl = Object.defineProperty, Il = Object.getOwnPropertyDescriptor, Hr = (e) => {
  throw TypeError(e);
}, xe = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Il(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Fl(t, i, r), r;
}, Kl = (e, t, i) => t.has(e) || Hr("Cannot " + i), Ul = (e, t, i) => t.has(e) ? Hr("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), ki = (e, t, i) => (Kl(e, t, "access private method"), i), Ze, Vr, qr, jr;
const J = { size: 168, stroke: 14 };
let ee = class extends k {
  constructor() {
    super(...arguments), Ul(this, Ze), this.report = null, this.period = "", this.periods = [], this.fetchedUtc = null, this.refreshing = !1, this.busy = !1;
  }
  render() {
    if (!this.report) return c;
    const e = this.report.traffic.kpis, t = Fn(e);
    return l`
      ${ki(this, Ze, Vr).call(this, e)}
      ${t.length > 0 ? l`<section aria-labelledby="tiles-title" class=${this.busy ? "busy" : ""}>
            <h3 id="tiles-title" class="section-label">${o(this, "uiKeyFiguresLabel")}</h3>
            <ul class="tiles">${t.map((i) => l`<li><esatto-seo-report-kpi-tile .kpi=${i}></esatto-seo-report-kpi-tile></li>`)}</ul>
          </section>` : c}
      ${ki(this, Ze, jr).call(this)}
    `;
  }
};
Ze = /* @__PURE__ */ new WeakSet();
Vr = function(e) {
  const t = L(this), { main: i, ring: s } = nr(e), r = Yt(this, i?.trend), a = i ? dt(i.value, i.format, t) : "";
  return l`
      <section class="card hero tone-${Gr(r?.pill?.direction)} ${this.busy ? "busy" : ""}" aria-label=${o(this, "uiOverviewLabel")}>
        ${s ? ki(this, Ze, qr).call(this, s) : l`<span class="hero-badge icon-badge solid round tone-notice" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>`}
        <div class="hero-body">
          ${i ? l`<p class="eyebrow">${j(this, "kpi", i.key, "Label")}</p>` : c}
          <h3 id="hero-title" class="hero-title">${a || o(this, "uiNoValueText")}</h3>
          ${qt(r)}
          ${i ? l`<p class="muted small">${j(this, "kpi", i.key, "Description")}</p>` : c}
          <esatto-seo-report-period-bar
            .periods=${this.periods}
            .period=${this.period}
            .fetchedUtc=${this.fetchedUtc}
            ?refreshing=${this.refreshing}
            ?busy=${this.busy}
          ></esatto-seo-report-period-bar>
        </div>
      </section>
    `;
};
qr = function(e) {
  const t = L(this), i = (J.size - J.stroke) / 2, s = 2 * Math.PI * i, r = Math.max(0, Math.min(100, e.value ?? 0)), a = J.size / 2, n = j(this, "kpi", e.key, "Label"), u = dt(e.value, e.format, t);
  return l`
      <figure class="ring" role="img" aria-label=${o(this, "uiRingAltText", { label: n, value: u })}>
        <svg width=${J.size} height=${J.size} viewBox="0 0 ${J.size} ${J.size}" aria-hidden="true">
          <defs>
            <linearGradient id="ring-gradient" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0%" class="stop-a"></stop>
              <stop offset="100%" class="stop-b"></stop>
            </linearGradient>
          </defs>
          <circle class="track" cx=${a} cy=${a} r=${i} stroke-width=${J.stroke} fill="none"></circle>
          <circle
            class="fill ${r ? "" : "empty"}"
            cx=${a}
            cy=${a}
            r=${i}
            stroke-width=${J.stroke}
            fill="none"
            stroke="url(#ring-gradient)"
            stroke-dasharray="${r / 100 * s} ${s}"
            stroke-linecap="round"
            transform="rotate(-90 ${a} ${a})"
          ></circle>
        </svg>
        <div class="ring-text" aria-hidden="true">
          <span class="ring-value">${u}</span>
          <span class="muted small">${o(this, "uiRingOfVisitsText")}</span>
        </div>
        <figcaption aria-hidden="true"><span class="tag tone-notice">${n}</span></figcaption>
      </figure>
    `;
};
jr = function() {
  const e = (this.report?.sources ?? []).map((t) => ({
    source: j(this, "source", t.id, "Label"),
    note: q(this, "sourceNote", t.note)
  })).filter((t) => t.note);
  return e.length === 0 ? c : l`
      <ul class="notes muted small" aria-label=${o(this, "uiDataNotesLabel")}>
        ${e.map((t) => l`<li>${o(this, "uiSourceNoteText", t)}</li>`)}
      </ul>
    `;
};
ee.styles = [
  C,
  jt,
  w`
      :host { display: grid; gap: var(--seo-gap); }
      .busy { opacity: 0.5; transition: opacity var(--seo-ease); }

      /* The card is tinted by the main figure's trend, as SEO Health tints its score card. */
      .hero {
        display: flex; flex-wrap: wrap; gap: var(--uui-size-layout-2); align-items: center; padding: var(--uui-size-space-6);
        background: radial-gradient(120% 160% at 0% 0%, color-mix(in srgb, var(--tone) 16%, var(--uui-color-surface)) 0%, var(--uui-color-surface) 62%);
        border-color: color-mix(in srgb, var(--tone) 30%, var(--seo-line));
      }
      .hero-body { flex: 1; min-width: 18rem; display: grid; gap: var(--uui-size-space-3); justify-items: start; }
      .hero-badge { width: 96px; height: 96px; font-size: 44px; }
      .eyebrow { margin: 0; font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--seo-muted); }
      .hero-title { margin: -6px 0 0; font-size: 44px; font-weight: 800; letter-spacing: -0.02em; line-height: 1.15; font-variant-numeric: tabular-nums; }

      /* The ring is a share of a whole, in blue: it is not a grade. */
      .ring {
        --tone: var(--seo-notice); --ring-a: #60a5fa; --ring-b: #6d4ce0;
        position: relative; margin: 0; width: 168px; display: grid; justify-items: center; gap: var(--uui-size-space-3);
      }
      .ring svg { display: block; filter: drop-shadow(0 8px 18px color-mix(in srgb, var(--tone) 30%, transparent)); }
      .ring .track { stroke: color-mix(in srgb, var(--tone) 18%, var(--uui-color-surface)); }
      .ring .fill.empty { display: none; }
      .stop-a { stop-color: var(--ring-a); }
      .stop-b { stop-color: var(--ring-b); }
      .ring-text { position: absolute; top: 0; height: 168px; display: grid; place-content: center; text-align: center; gap: 2px; }
      .ring-value { font-size: 36px; font-weight: 800; line-height: 1; letter-spacing: -0.03em; font-variant-numeric: tabular-nums; }

      .section-label { margin: 0 0 var(--uui-size-space-3); font-size: 12px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--seo-muted); }
      .tiles { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--seo-gap); grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); }
      .notes { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-1); }
    `
];
xe([
  h({ attribute: !1 })
], ee.prototype, "report", 2);
xe([
  h()
], ee.prototype, "period", 2);
xe([
  h({ attribute: !1 })
], ee.prototype, "periods", 2);
xe([
  h({ attribute: !1 })
], ee.prototype, "fetchedUtc", 2);
xe([
  h({ type: Boolean })
], ee.prototype, "refreshing", 2);
xe([
  h({ type: Boolean })
], ee.prototype, "busy", 2);
ee = xe([
  T("esatto-seo-report-overview")
], ee);
var Gl = Object.defineProperty, Wl = Object.getOwnPropertyDescriptor, Yr = (e) => {
  throw TypeError(e);
}, Xt = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Wl(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Gl(t, i, r), r;
}, Bl = (e, t, i) => t.has(e) || Yr("Cannot " + i), Hl = (e, t, i) => (Bl(e, t, "read from private field"), i ? i.call(e) : t.get(e)), Vl = (e, t, i) => t.has(e) ? Yr("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Ci;
let Ne = class extends k {
  constructor() {
    super(...arguments), this.error = null, this.retryable = !0, Vl(this, Ci, () => this.dispatchEvent(new CustomEvent("retry")));
  }
  render() {
    if (!this.error) return c;
    const { title: e, text: t } = le(this, this.error.code, { ...this.error.args, ...this.extraArgs });
    return l`
      <div class="banner tone-critical" role="alert">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
        <div>
          <h3>${e}</h3>
          <p>${t}</p>
          ${this.error.traceId ? l`<p class="muted small">${o(this, "uiProblemReferenceText", { traceId: this.error.traceId })}</p>` : c}
          ${this.retryable ? l`<div class="actions">
                <uui-button look="outline" label=${o(this, "uiTryAgainLabel")} @click=${Hl(this, Ci)}>${o(this, "uiTryAgainLabel")}</uui-button>
              </div>` : c}
        </div>
      </div>
    `;
  }
};
Ci = /* @__PURE__ */ new WeakMap();
Ne.styles = [C, w`h3 { font-size: 15px; font-weight: 700; }`];
Xt([
  h({ attribute: !1 })
], Ne.prototype, "error", 2);
Xt([
  h({ attribute: !1 })
], Ne.prototype, "extraArgs", 2);
Xt([
  h({ type: Boolean })
], Ne.prototype, "retryable", 2);
Ne = Xt([
  T("esatto-seo-report-problem")
], Ne);
var ql = Object.defineProperty, jl = Object.getOwnPropertyDescriptor, Zt = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? jl(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && ql(t, i, r), r;
};
let Re = class extends k {
  constructor() {
    super(...arguments), this.caption = "", this.columns = [], this.rows = [];
  }
  render() {
    return this.rows.length === 0 ? l`<p class="muted">${o(this, "uiNoDataText")}</p>` : l`
      <div class="scroll" role="region" aria-label=${this.caption} tabindex="0">
        <table>
          <caption class="visually-hidden">${this.caption}</caption>
          <thead>
            <tr>
              ${this.columns.map((e) => l`<th scope="col" class=${`${e.numeric ? "num" : ""} ${e.wide ? "wide" : ""}`.trim()}>${e.label}</th>`)}
            </tr>
          </thead>
          <tbody>
            ${this.rows.map(
      (e) => l`<tr>
                ${e.cells.map((t, i) => {
        const s = this.columns[i], r = `${s?.numeric ? "num" : ""} ${s?.wide ? "wide" : ""} ${t.missing ? "muted" : ""}`.trim(), a = t.strong ? l`<strong class="name" title=${t.title ?? c}>${t.text}</strong>` : l`${t.text}`, n = t.icon ? l`<span class="with-icon"><uui-icon name=${t.icon} aria-hidden="true"></uui-icon>${a}</span>` : a, u = t.detail ? l`<span class="detail muted">${t.detail}</span>` : c;
        return i === 0 ? l`<th scope="row" class=${r}>${n}${u}</th>` : l`<td class=${r}>${n}${u}${t.trend ? qt(t.trend) : c}</td>`;
      })}
              </tr>`
    )}
          </tbody>
        </table>
      </div>
    `;
  }
};
Re.styles = [
  C,
  jt,
  w`
      .scroll { overflow-x: auto; }
      table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
      th, td { padding: var(--uui-size-space-3) var(--uui-size-space-4); text-align: left; border-bottom: 1px solid var(--seo-line); vertical-align: top; }
      thead th {
        font-size: 12px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; color: var(--seo-muted);
        border-bottom: 2px solid var(--seo-line); white-space: nowrap;
      }
      tbody th { font-weight: 400; }
      tbody tr:last-child th, tbody tr:last-child td { border-bottom: 0; }
      tbody tr:hover { background: var(--seo-wash); }
      .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
      /* Running text (advice) gets room and wraps, instead of being squeezed beside the figures. */
      .wide { min-width: 22rem; }
      td.num .detail { text-align: right; }
      .detail { display: block; margin-top: 2px; font-size: 12px; line-height: 1.4; white-space: normal; }
      .with-icon { display: inline-flex; gap: 6px; align-items: center; white-space: nowrap; }
      .with-icon uui-icon { flex: none; }
      /* A trend under a figure keeps to the figure's right edge. */
      td.num .trend { display: flex; justify-content: flex-end; margin-top: 2px; }
      /* A long name (a page's path, a search query) is cut with an ellipsis; the whole of it stays in the text and in the title. */
      .name { display: inline-block; max-width: 40ch; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; vertical-align: bottom; font-weight: 600; }
      .scroll:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    `
];
Zt([
  h()
], Re.prototype, "caption", 2);
Zt([
  h({ attribute: !1 })
], Re.prototype, "columns", 2);
Zt([
  h({ attribute: !1 })
], Re.prototype, "rows", 2);
Re = Zt([
  T("esatto-seo-report-data-table")
], Re);
function ce(e, t, i) {
  return l`
    <div class="card-head">
      <span class="icon-badge" aria-hidden="true"><uui-icon name=${e}></uui-icon></span>
      <h3 id=${t}>${i}</h3>
    </div>
  `;
}
function Yl(e, t) {
  return l`
    <section class="card" aria-labelledby="summary-title">
      ${ce("icon-lightbulb-active", "summary-title", o(e, "uiSummaryTitle"))}
      ${t.length > 0 ? l`<ul class="says">${t.map((i) => l`<li>${i.text}</li>`)}</ul>` : l`<p class="muted">${o(e, "uiSummaryEmptyText")}</p>`}
    </section>
  `;
}
function Xl(e, t) {
  return l`
    <section class="card" aria-labelledby="movement-title">
      ${ce("icon-chart-curve", "movement-title", o(e, "uiMovementTitle"))}
      ${t.state === "none" ? l`<p class="muted">${o(e, "uiMovementNoneText")}</p>` : l`
            <p class="help muted">${o(e, "uiMovementHelpText")}</p>
            <p class="help muted">${t.ranked}</p>
            ${t.state === "empty" ? l`<p class="muted">${o(e, "uiMovementEmptyText")}</p>` : t.lists.map(
    (i) => l`
                    <h4>${i.title}</h4>
                    <esatto-seo-report-data-table .caption=${i.title} .columns=${i.columns} .rows=${i.rows}></esatto-seo-report-data-table>
                  `
  )}
          `}
    </section>
  `;
}
function Zl(e, t) {
  return t.length === 0 ? c : l`
    <section class="card" aria-labelledby="opportunities-title">
      ${ce("icon-wand", "opportunities-title", o(e, "uiOpportunitiesTitle"))}
      <p class="help muted">${o(e, "uiOpportunitiesHelpText")}</p>
      <ul class="wins">
        ${t.map(
    (i) => l`
            <li class="win">
              <div class="win-head">
                <strong>${i.title}</strong>
                ${i.effort ? l`<span class="chip"><uui-icon name="icon-time" aria-hidden="true"></uui-icon>${i.effort.label}</span>` : c}
              </div>
              <p class="muted">${i.text}</p>
            </li>
          `
  )}
      </ul>
    </section>
  `;
}
const Jl = w`
  h4 { font-size: 14px; font-weight: 700; margin: var(--uui-size-space-5) 0 var(--uui-size-space-3); }
  .help { margin-bottom: var(--uui-size-space-3); }
  .says { margin: 0; padding-left: 1.2em; display: grid; gap: var(--uui-size-space-3); }
  .says li { line-height: 1.6; max-width: 80ch; }
  .wins { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
  .win { padding: var(--uui-size-space-3) var(--uui-size-space-4); border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm); background: var(--seo-wash); }
  .win-head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-4); align-items: center; }
  .win p { margin-top: var(--uui-size-space-2); line-height: 1.5; max-width: 80ch; }
`;
function ht(e) {
  return e.length === 0 ? c : l`
    <ul class="notes-list">
      ${e.map(
    (t) => l`
          <li class="note ${t.severity}">
            <span class="note-kind"><uui-icon name=${t.icon} aria-hidden="true"></uui-icon>${t.label}</span>
            <div class="note-body">
              ${t.title ? l`<strong>${t.title}</strong> ` : c}${t.text}
            </div>
          </li>
        `
  )}
    </ul>
  `;
}
const Jt = w`
  .notes-list { list-style: none; margin: var(--uui-size-space-4) 0 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
  .note {
    --tone: var(--seo-line);
    display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-4); align-items: baseline;
    padding: var(--uui-size-space-4) var(--uui-size-space-5); border-radius: var(--seo-radius-sm); font-size: 0.9rem;
    color: var(--uui-color-text); background: var(--seo-wash);
    border: 1px solid color-mix(in srgb, var(--tone) 35%, var(--seo-line)); box-shadow: inset 4px 0 0 var(--tone);
  }
  .note.warning { --tone: var(--seo-warning); background: color-mix(in srgb, var(--seo-warning) 8%, var(--uui-color-surface)); }
  .note.insight { --tone: var(--seo-notice); background: color-mix(in srgb, var(--seo-notice) 7%, var(--uui-color-surface)); }
  /* The kind in words, beside its icon: the text carries it, the colour only repeats it. */
  .note-kind {
    display: inline-flex; gap: 5px; align-items: center; font-size: 0.75rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase;
    color: var(--seo-muted);
  }
  .note-body { flex: 1 1 24rem; min-width: 0; line-height: 1.5; }
`;
function Gt({ icon: e, title: t, subtitle: i, help: s, columns: r, rows: a, notes: n }) {
  return l`
    <section class="card">
      <div class="card-head">
        <span class="icon-badge" aria-hidden="true"><uui-icon name=${e}></uui-icon></span>
        <span class="card-titles">
          <h3>${t}</h3>
          ${i ? l`<span class="muted">${i}</span>` : c}
        </span>
      </div>
      ${s ? l`<p class="help muted">${s}</p>` : c}
      <esatto-seo-report-data-table .caption=${t} .columns=${r} .rows=${a}></esatto-seo-report-data-table>
      ${n ? ht(n) : c}
    </section>
  `;
}
const es = w`
  .card-titles { display: grid; gap: 2px; }
  .help { margin-bottom: var(--uui-size-space-4); max-width: 70ch; font-size: 0.85rem; line-height: 1.5; }
`;
function Xr(e, t, i) {
  return l`
    <div class="save-row">
      <button type="button" class="save" aria-disabled=${t.saving ? "true" : "false"} aria-busy=${t.saving ? "true" : "false"} @click=${i}>
        ${t.saving ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : c}${o(e, "uiSaveLabel")}
      </button>
      <span class="saved" role="status" aria-live="polite">${t.saved ? l`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${o(e, "uiSavedText")}` : c}</span>
    </div>
  `;
}
const pt = w`
  .editor { display: grid; gap: var(--uui-size-space-3); }
  .editor > label { font-size: 13px; font-weight: 600; line-height: 1.5; }
  .help { font-size: 0.85rem; line-height: 1.5; max-width: 70ch; }
  textarea {
    font: inherit; line-height: 1.5; box-sizing: border-box; width: 100%; min-height: 6rem; padding: var(--uui-size-space-3); resize: vertical;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  textarea:hover { border-color: var(--uui-color-border-emphasis); }
  textarea:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  /* Too long: a heavier border, and the counter and the message say it in words with an icon. */
  textarea[aria-invalid='true'] { border-width: 2px; border-color: var(--seo-critical); }

  .counters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-4); align-items: center; font-size: 13px; }
  .counter { color: var(--seo-muted); font-variant-numeric: tabular-nums; }
  .counter.over { color: var(--uui-color-text); font-weight: 700; }
  .counter uui-icon, .error uui-icon, .warning uui-icon { margin-right: 4px; vertical-align: -2px; }
  .warning { font-size: 13px; font-weight: 600; margin: 0; }
  .error { font-size: 13px; font-weight: 700; margin: 0; padding: var(--uui-size-space-3) var(--uui-size-space-4); border-radius: var(--seo-radius-sm); border: 1px solid var(--seo-critical); box-shadow: inset 4px 0 0 var(--seo-critical); }

  .save-row { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
  .save {
    display: inline-flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; box-sizing: border-box;
    padding: 0 var(--uui-size-space-5); font: inherit; font-weight: 700; cursor: pointer;
    border: 1px solid var(--uui-color-default); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-default); color: var(--uui-color-default-contrast);
    transition: background-color var(--seo-ease);
  }
  .save:hover { background: var(--uui-color-default-emphasis); border-color: var(--uui-color-default-emphasis); }
  .save[aria-disabled='true'] { cursor: progress; opacity: 0.8; }
  .save[aria-disabled='true']:hover { background: var(--uui-color-default); border-color: var(--uui-color-default); }
  .save uui-loader-circle { font-size: 16px; }
  .saved { display: inline-flex; align-items: center; gap: 4px; min-height: 24px; font-weight: 700; }
`;
var Ql = Object.defineProperty, ec = Object.getOwnPropertyDescriptor, Zr = (e) => {
  throw TypeError(e);
}, ft = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? ec(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Ql(t, i, r), r;
}, Jr = (e, t, i) => t.has(e) || Zr("Cannot " + i), ci = (e, t, i) => (Jr(e, t, "read from private field"), t.get(e)), ui = (e, t, i) => t.has(e) ? Zr("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), tc = (e, t, i, s) => (Jr(e, t, "write to private field"), t.set(e, i), i), Et, Si, Ei;
const ic = { saving: !1, saved: !1, revision: 0, problem: null, refused: !1 };
let fe = class extends k {
  constructor() {
    super(...arguments), this.note = null, this.period = "", this.saveState = ic, this._draft = "", ui(this, Et, -1), ui(this, Si, (e) => {
      this._draft = e.target.value, this.dispatchEvent(new CustomEvent("edit"));
    }), ui(this, Ei, () => {
      this.saveState.saving || this.dispatchEvent(new CustomEvent("save", { detail: { text: this._draft } }));
    });
  }
  willUpdate(e) {
    (e.has("note") || e.has("period") || this.saveState.revision !== ci(this, Et)) && (tc(this, Et, this.saveState.revision), this._draft = this.note ?? "");
  }
  render() {
    const e = xr(this._draft), { refused: t, problem: i } = this.saveState;
    return l`
      <div class="editor">
        <label for="note">${o(this, "uiMonthNoteLabel")}</label>
        <textarea
          id="note"
          rows="4"
          aria-describedby="note-help note-counter note-error"
          aria-invalid=${e.over ? "true" : "false"}
          ?readonly=${this.saveState.saving}
          .value=${W(this._draft)}
          @input=${ci(this, Si)}
        ></textarea>
        <div class="counters">
          <span class="counter ${e.over ? "over" : ""}" id="note-counter">
            ${e.over ? l`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>` : c}${o(this, "uiNoteCounterText", { length: e.length, max: yi })}
          </span>
        </div>
        <p class="help muted" id="note-help">${o(this, "uiMonthNoteHelpText")}</p>
        <div id="note-error" role="alert">
          ${t ? l`<p class="error"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiNoteTooLongText", { over: e.excess, max: yi })}</p>` : c}
        </div>
        ${Xr(this, this.saveState, ci(this, Ei))}
        ${i ? l`<esatto-seo-report-problem .error=${i} @retry=${() => this.dispatchEvent(new CustomEvent("retry"))}></esatto-seo-report-problem>` : c}
      </div>
    `;
  }
};
Et = /* @__PURE__ */ new WeakMap();
Si = /* @__PURE__ */ new WeakMap();
Ei = /* @__PURE__ */ new WeakMap();
fe.styles = [C, pt];
ft([
  h({ attribute: !1 })
], fe.prototype, "note", 2);
ft([
  h()
], fe.prototype, "period", 2);
ft([
  h({ attribute: !1 })
], fe.prototype, "saveState", 2);
ft([
  B()
], fe.prototype, "_draft", 2);
fe = ft([
  T("esatto-seo-report-note-editor")
], fe);
var sc = Object.defineProperty, rc = Object.getOwnPropertyDescriptor, Qr = (e) => {
  throw TypeError(e);
}, Qt = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? rc(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && sc(t, i, r), r;
}, ea = (e, t, i) => t.has(e) || Qr("Cannot " + i), wt = (e, t, i) => (ea(e, t, "read from private field"), t.get(e)), _t = (e, t, i) => t.has(e) ? Qr("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Vs = (e, t, i, s) => (ea(e, t, "write to private field"), t.set(e, i), i), Lt, Pt, Li, Pi;
const ac = { saving: !1, saved: !1, revision: 0, problem: null, refused: !1 };
let Oe = class extends k {
  constructor() {
    super(...arguments), this.terms = [], this.saveState = ac, this._draft = "", _t(this, Lt, null), _t(this, Pt, -1), _t(this, Li, (e) => {
      this._draft = e.target.value, this.dispatchEvent(new CustomEvent("edit"));
    }), _t(this, Pi, () => {
      this.saveState.saving || this.dispatchEvent(new CustomEvent("save", { detail: { text: this._draft } }));
    });
  }
  willUpdate() {
    const e = kr(this.terms);
    (e !== wt(this, Lt) || this.saveState.revision !== wt(this, Pt)) && (Vs(this, Lt, e), Vs(this, Pt, this.saveState.revision), this._draft = e);
  }
  render() {
    const e = Po(this._draft), { problem: t } = this.saveState;
    return l`
      <div class="editor">
        <label for="terms">${o(this, "uiTargetsEditorLabel")}</label>
        <textarea
          id="terms"
          rows="6"
          aria-describedby="terms-help terms-counter terms-warnings"
          ?readonly=${this.saveState.saving}
          .value=${W(this._draft)}
          @input=${wt(this, Li)}
        ></textarea>
        <div class="counters">
          <span class="counter ${e.tooMany ? "over" : ""}" id="terms-counter">
            ${e.tooMany ? l`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>` : c}${o(this, "uiTermsCounterText", { count: e.count, max: De })}
          </span>
        </div>
        <div id="terms-warnings">
          ${e.tooMany ? l`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiTermsTooManyText", { max: De })}</p>` : c}
          ${e.tooLong > 0 ? l`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiTermsTooLongText", { count: e.tooLong, max: Mt })}</p>` : c}
        </div>
        <p class="help muted" id="terms-help">${o(this, "uiTargetsEditorHelpText")}</p>
        ${Xr(this, this.saveState, wt(this, Pi))}
        ${t ? l`<esatto-seo-report-problem .error=${t} @retry=${() => this.dispatchEvent(new CustomEvent("retry"))}></esatto-seo-report-problem>` : c}
      </div>
    `;
  }
};
Lt = /* @__PURE__ */ new WeakMap();
Pt = /* @__PURE__ */ new WeakMap();
Li = /* @__PURE__ */ new WeakMap();
Pi = /* @__PURE__ */ new WeakMap();
Oe.styles = [C, pt];
Qt([
  h({ attribute: !1 })
], Oe.prototype, "terms", 2);
Qt([
  h({ attribute: !1 })
], Oe.prototype, "saveState", 2);
Qt([
  B()
], Oe.prototype, "_draft", 2);
Oe = Qt([
  T("esatto-seo-report-terms-editor")
], Oe);
var nc = Object.defineProperty, oc = Object.getOwnPropertyDescriptor, ta = (e) => {
  throw TypeError(e);
}, gt = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? oc(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && nc(t, i, r), r;
}, ia = (e, t, i) => t.has(e) || ta("Cannot " + i), F = (e, t, i) => (ia(e, t, "read from private field"), i ? i.call(e) : t.get(e)), qs = (e, t, i) => t.has(e) ? ta("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Ee = (e, t, i) => (ia(e, t, "access private method"), i), R, se, ts, sa, ra, aa, na;
let ge = class extends k {
  constructor() {
    super(...arguments), qs(this, se), this.period = "", this.note = null, this.revision = 0, this._version = 0, qs(this, R, new No({ getAnalysis: $n, putNote: kn, putTargets: Cn }, () => this._version += 1, {
      noteSaved: (e, t) => this.dispatchEvent(new CustomEvent(Cr, { detail: { period: e, note: t }, bubbles: !0, composed: !0 }))
    }));
  }
  connectedCallback() {
    super.connectedCallback(), F(this, R).connect();
  }
  disconnectedCallback() {
    F(this, R).disconnect(), super.disconnectedCallback();
  }
  willUpdate(e) {
    e.has("period") ? F(this, R).show(this.period) : e.has("revision") && e.get("revision") !== void 0 && F(this, R).invalidate();
  }
  // ---- Showing ------------------------------------------------------------------------------------------------------------------
  render() {
    const e = F(this, R).state;
    switch (xo(e)) {
      case "idle":
        return c;
      case "loading":
        return l`
          <section class="card">
            <p class="muted loading" role="status"><uui-loader-circle aria-hidden="true"></uui-loader-circle>${o(this, "uiAnalysisLoadingText")}</p>
          </section>
        `;
      case "failed":
        return Ee(this, se, ts).call(this, e);
      case "none":
        return l`<section class="card"><p class="muted">${o(this, "uiAnalysisNoneText")}</p></section>`;
      default:
        return At(e.analysis) ? Ee(this, se, sa).call(this, e, Co(this, e.analysis)) : c;
    }
  }
};
R = /* @__PURE__ */ new WeakMap();
se = /* @__PURE__ */ new WeakSet();
ts = function(e) {
  return e.problem ? l`<esatto-seo-report-problem .error=${e.problem} @retry=${() => {
    F(this, R).retry();
  }}></esatto-seo-report-problem>` : c;
};
sa = function(e, t) {
  const i = e.targetsStale;
  return l`
      <div class="page">
        ${Ee(this, se, ts).call(this, e)} ${Yl(this, t.statements)} ${Ee(this, se, ra).call(this, e)} ${Ee(this, se, aa).call(this, e, t, i)}
        ${t.history && !i ? Gt({
    icon: "icon-target",
    title: o(this, "uiHistoryTitle"),
    help: o(this, "uiHistoryHelpText"),
    columns: t.history.columns,
    rows: t.history.rows
  }) : c}
        ${Xl(this, t.movement)} ${Zl(this, t.opportunities)}
      </div>
    `;
};
ra = function(e) {
  return l`
      <section class="card" aria-labelledby="note-title">
        ${ce("icon-notepad", "note-title", o(this, "uiMonthNoteTitle"))}
        <esatto-seo-report-note-editor
          .note=${this.note}
          .period=${this.period}
          .saveState=${e.note}
          @save=${(t) => {
    F(this, R).saveNote(this.period, t.detail.text);
  }}
          @edit=${() => F(this, R).noteEdited()}
          @retry=${() => {
    F(this, R).retryNote();
  }}
        ></esatto-seo-report-note-editor>
      </section>
    `;
};
aa = function(e, t, i) {
  const s = o(this, "uiTargetsTitle");
  return l`
      <section class="card" aria-labelledby="targets-title">
        ${ce("icon-target", "targets-title", s)}
        <p class="help muted">${o(this, "uiTargetsHelpText", { label: o(this, "targetMissingLabel"), rowsFetched: !i && t.targets.rowsFetched > 0 ? t.targets.rowsFetched : null })}</p>
        <esatto-seo-report-terms-editor
          .terms=${F(this, R).terms}
          .saveState=${e.targets}
          @save=${(r) => {
    F(this, R).saveTargets(r.detail.text);
  }}
          @edit=${() => F(this, R).termsEdited()}
          @retry=${() => {
    F(this, R).retryTargets();
  }}
        ></esatto-seo-report-terms-editor>
        <div class="targets-table">${Ee(this, se, na).call(this, e, t, i, s)}</div>
      </section>
    `;
};
na = function(e, t, i, s) {
  return i ? e.loading ? l`<p class="muted" role="status">${o(this, "uiAnalysisUpdatingText")}</p>` : c : t.targets.state === "noTerms" ? l`<p class="muted">${o(this, "uiTargetsEmptyText")}</p>` : l`<esatto-seo-report-data-table .caption=${s} .columns=${t.targets.columns} .rows=${t.targets.rows}></esatto-seo-report-data-table>`;
};
ge.styles = [
  C,
  es,
  Jl,
  w`
      .loading { display: flex; align-items: center; gap: var(--uui-size-space-3); }
      .targets-table { margin-top: var(--uui-size-space-5); }
    `
];
gt([
  h()
], ge.prototype, "period", 2);
gt([
  h({ attribute: !1 })
], ge.prototype, "note", 2);
gt([
  h({ type: Number })
], ge.prototype, "revision", 2);
gt([
  B()
], ge.prototype, "_version", 2);
ge = gt([
  T("esatto-seo-report-analysis")
], ge);
function lc(e) {
  return e === "warning" ? "warning" : "info";
}
const js = {
  warning: "icon-alert",
  info: "icon-info",
  insight: "icon-lightbulb"
}, Ys = {
  warning: "uiNoteWarningLabel",
  info: "uiNoteInfoLabel",
  insight: "uiNoteInsightLabel"
};
function is(e, t, i, s = "include") {
  const r = [];
  for (const a of t ?? []) {
    const n = lc(a.severity);
    if (s === "exclude" && n === "warning" || s === "only" && n !== "warning") continue;
    const u = q(e, "annotation", a.text, "Title"), d = q(e, "annotation", a.text, "Text");
    !u && !d || r.push({ kind: "annotation", severity: n, icon: js[n], label: o(e, Ys[n]), title: u, text: d });
  }
  if (s !== "only")
    for (const a of i ?? []) {
      const n = q(e, "insight", a);
      n && r.push({ kind: "insight", severity: "insight", icon: js.insight, label: o(e, Ys.insight), title: "", text: n });
    }
  return r;
}
const cc = /^(0[1-9]|1[0-2])$/, uc = /^\d{4}-(0[1-9]|1[0-2])$/, dc = /^\d{4}-\d{2}-\d{2}$/;
function ss(e) {
  return e.length === 0 ? "raw" : e.every((t) => cc.test(t)) ? "month" : e.every((t) => uc.test(t)) ? "period" : e.every((t) => dc.test(t)) ? "day" : "raw";
}
function Di(e, t, i) {
  const s = e.labels ?? [];
  switch (ss(s)) {
    case "month":
      return s.map((r) => i === "axis" ? no(r, t) : oo(r, t));
    case "period":
      return s.map((r) => i === "axis" ? fr(r, t) : et(r, t));
    case "day":
      return s.map((r) => i === "axis" ? vi(r, t) : zt(r, t));
    default:
      return [...s];
  }
}
function ei(e, t) {
  try {
    const { key: i, args: s } = Qn(t.key);
    return o(e, i, s);
  } catch (i) {
    if (i instanceof RangeError) return t.key;
    throw i;
  }
}
function Je(e, t, i, s) {
  return t?.id ? j(e, "chart", t.id, i, s) : "";
}
function oa(e, t) {
  const i = t?.labels ?? [];
  if (i.length === 0 || (t.series ?? []).length === 0) return "";
  const s = L(e);
  let r, a;
  switch (ss(i)) {
    case "month": {
      const n = t.series.map((u) => hr(u.key)).filter((u) => u !== null);
      if (n.length === 0) return "";
      r = String(Math.min(...n)), a = String(Math.max(...n));
      break;
    }
    case "period":
      r = et(i[0], s), a = et(i[i.length - 1], s);
      break;
    case "day":
      r = vi(i[0], s), a = vi(i[i.length - 1], s);
      break;
    default:
      return "";
  }
  return o(e, "uiChartRangeText", { from: r, to: a });
}
function hc(e, t, i) {
  return [Je(e, t, "Title", i), Je(e, t, "Subtitle", i), oa(e, t)].filter(Boolean).join(". ");
}
const pc = {
  month: "uiColumnMonthLabel",
  period: "uiColumnMonthLabel",
  day: "uiColumnDayLabel",
  raw: "uiColumnPeriodLabel"
};
function fc(e, t) {
  const i = t?.labels ?? [], s = t?.series ?? [], r = L(e), a = o(e, pc[ss(i)]), n = s.map((_) => ei(e, _));
  if (i.length === 0 || s.length === 0) return { periodHeader: a, seriesHeaders: n, rows: [] };
  const u = o(e, "uiNoValueText"), d = Di(t, r, "table");
  return {
    periodHeader: a,
    seriesHeaders: n,
    rows: i.map((_, I) => ({
      label: d[I],
      cells: s.map((X) => {
        const Z = X.values?.[I];
        return Z == null ? u : O(Z, r) || u;
      })
    }))
  };
}
function la(e) {
  return Nn(e) ? "empty" : "chart";
}
const gc = 240;
function mc(e) {
  const t = e?.height;
  return typeof t != "number" || !Number.isFinite(t) || t <= 0 ? gc : Math.min(600, Math.max(120, t));
}
const ca = 25, vc = (e) => typeof e == "number" && Number.isFinite(e);
function je(e, t, i) {
  return vc(t) ? { text: i(t) } : { text: o(e, "uiNoValueText"), missing: !0 };
}
function Wt(e, t) {
  return t ? { text: t } : { text: o(e, "uiNoValueText"), missing: !0 };
}
function ua(e, t) {
  const i = L(e);
  return [
    je(e, t.clicks, (s) => O(s, i)),
    je(e, t.impressions, (s) => O(s, i)),
    je(e, t.ctr_percent, (s) => Ke(s, i, { digits: 2 })),
    je(e, t.position, (s) => Pe(s, i, 1))
  ];
}
function da(e) {
  return [
    { label: o(e, "uiColumnClicksLabel"), numeric: !0 },
    { label: o(e, "uiColumnImpressionsLabel"), numeric: !0 },
    { label: o(e, "uiColumnCtrLabel"), numeric: !0 },
    { label: o(e, "uiColumnPositionLabel"), numeric: !0 }
  ];
}
const bc = (e) => [{ label: o(e, "uiColumnQueryLabel") }, ...da(e)];
function yc(e, t, i = ca) {
  return (t?.queries ?? []).slice(0, Math.max(0, i)).map((s) => ({
    cells: [{ ...Wt(e, s.query), title: s.query || void 0, strong: !0 }, ...ua(e, s)]
  }));
}
const $c = (e) => [{ label: o(e, "uiColumnPageLabel") }, ...da(e)];
function xc(e, t, i = ca) {
  return (t?.pages ?? []).slice(0, Math.max(0, i)).map((s) => ({
    cells: [{ ...Wt(e, s.path || s.url), title: s.url, strong: !0 }, ...ua(e, s)]
  }));
}
function ha(e) {
  return e?.segments?.[0]?.metrics ?? e?.total?.metrics ?? [];
}
function wc(e, t) {
  return [
    { label: o(e, "uiColumnSegmentLabel") },
    ...ha(t).map((i) => ({ label: j(e, "segmentMetric", i.key, "Label"), numeric: !0 }))
  ];
}
const _c = (e, t) => t.format === "percent" ? Ke(t.value, L(e), { digits: 2 }) : dt(t.value, t.format, L(e));
function Tc(e, t) {
  const i = ha(t).length;
  return [...t?.segments ?? [], ...t?.total ? [t.total] : []].map((r) => {
    const a = Array.from({ length: i }, (n, u) => {
      const d = r.metrics?.[u], _ = d ? _c(e, d) : "";
      return _ ? { text: _, trend: Yt(e, d.trend) } : { text: o(e, "uiNoValueText"), missing: !0 };
    });
    return { cells: [{ text: ei(e, r), strong: !0 }, ...a] };
  });
}
const kc = (e) => [
  { label: o(e, "uiColumnChannelLabel") },
  { label: o(e, "uiColumnSourceLabel") },
  { label: o(e, "uiColumnVisitsLabel"), numeric: !0 }
];
function Cc(e, t) {
  return t.map((i) => ({
    cells: [Wt(e, i.channel), Wt(e, i.source), je(e, i.sessions, (s) => O(s, L(e)))]
  }));
}
function Sc(e) {
  const t = e?.traffic;
  return { blocks: t ? [t.organic, t.total].filter((i) => !!i) : [] };
}
function Ec(e) {
  return { block: e?.traffic?.search_comparison ?? null };
}
const Xs = { state: "empty", brand: !1, table: null, hint: "", charts: [], notes: [] };
function Lc(e, t) {
  const i = t?.traffic?.search ?? null;
  if (!i) return Xs;
  const s = Rn(i), r = Tc(e, i), a = r.length === 0 ? null : {
    icon: "icon-search",
    // The block's own heading says "brand vs non-brand": without the split it would be wrong, so the table is a table of the total.
    title: s ? j(e, "searchBlock", i.id, "Title") : o(e, "uiSearchTotalTitle"),
    subtitle: s ? j(e, "searchBlock", i.id, "Subtitle") : "",
    help: o(e, "uiHelpSegmentsText"),
    columns: wc(e, i),
    rows: r
  };
  return !s && !a ? Xs : {
    state: "ready",
    brand: s,
    table: a,
    hint: !s && a ? o(e, "uiBrandSplitOffText") : "",
    charts: s ? [i.clicks, i.impressions] : [],
    // The notes are about the split and its curve; the warnings are the findings of the To fix tab.
    notes: s ? is(e, i.annotations, i.insights, "exclude") : []
  };
}
function Pc(e, t) {
  return is(e, t?.traffic?.search?.annotations, null, "only");
}
function Dc(e, t) {
  const i = t?.traffic?.search ?? null, s = yc(e, i), r = xc(e, i);
  return s.length === 0 && r.length === 0 ? { state: "empty", queries: null, pages: null, note: null } : {
    state: "ready",
    queries: s.length === 0 ? null : { icon: "icon-search", title: o(e, "uiTopQueriesTitle"), subtitle: "", help: o(e, "uiHelpQueriesText"), columns: bc(e), rows: s },
    pages: r.length === 0 ? null : { icon: "icon-document", title: o(e, "uiTopPagesTitle"), subtitle: "", help: o(e, "uiHelpPagesText"), columns: $c(e), rows: r },
    note: {
      kind: "annotation",
      severity: "info",
      icon: "icon-info",
      label: o(e, "uiNoteInfoLabel"),
      title: "",
      text: o(e, "uiTopListsNoteText")
    }
  };
}
const Nc = /^#([0-9a-f]{3}|[0-9a-f]{6}|[0-9a-f]{8})$/i, Rc = /^(rgba?|color)\(\s*(?:srgb\s+)?([^)]*)\)$/i;
function Zs(e, t) {
  const i = e.endsWith("%"), s = Number(i ? e.slice(0, -1) : e);
  return Number.isFinite(s) ? i ? s / 100 * t : s : null;
}
function G(e) {
  const t = e?.trim();
  if (!t) return null;
  const i = Nc.exec(t);
  if (i) {
    let K = i[1];
    K.length === 3 && (K = [...K].map((H) => H + H).join(""));
    const y = (H) => Number.parseInt(K.slice(H, H + 2), 16);
    return { r: y(0), g: y(2), b: y(4), a: K.length === 8 ? y(6) / 255 : 1 };
  }
  const s = Rc.exec(t);
  if (!s) return null;
  const r = s[1].toLowerCase() === "color", [a, n] = s[2].split("/").map((K) => K.trim()), u = a.split(/[\s,]+/).filter(Boolean), d = n ?? (u.length === 4 ? u.pop() : void 0);
  if (u.length !== 3) return null;
  const _ = r ? 1 : 255, [I, X, Z] = u.map((K) => Zs(K, _)), ti = d === void 0 ? 1 : Zs(d, 1);
  if (I === null || X === null || Z === null || ti === null) return null;
  const ii = r ? 255 : 1, we = (K) => Math.round(Math.min(255, Math.max(0, K * ii)));
  return { r: we(I), g: we(X), b: we(Z), a: Math.min(1, Math.max(0, ti)) };
}
function P({ r: e, g: t, b: i, a: s }) {
  return s >= 1 ? `rgb(${e}, ${t}, ${i})` : `rgba(${e}, ${t}, ${i}, ${s})`;
}
const di = (e) => Math.round(Math.min(255, Math.max(0, e))).toString(16).padStart(2, "0"), hi = ({ r: e, g: t, b: i }) => `#${di(e)}${di(t)}${di(i)}`;
function Js(e, t) {
  const i = G(e);
  return i ? P({ ...i, a: t }) : e;
}
function pa({ r: e, g: t, b: i }) {
  const s = (r) => {
    const a = r / 255;
    return a <= 0.03928 ? a / 12.92 : ((a + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * s(e) + 0.7152 * s(t) + 0.0722 * s(i);
}
function Bt(e, t, i) {
  const s = (r, a) => Math.round(r * i + a * (1 - i));
  return { r: s(e.r, t.r), g: s(e.g, t.g), b: s(e.b, t.b), a: 1 };
}
function rs(e, t, i, s, r) {
  return {
    scheme: e,
    forced: !1,
    highlight: s,
    text: P(t),
    muted: P(Bt(t, i, 0.7)),
    grid: P({ ...t, a: 0.1 }),
    surface: P(i),
    // The tooltip is the page turned over: the text colour as its ground, the surface colour as its text.
    tooltipBackground: P(t),
    tooltipText: P(i),
    tooltipTitle: P(Bt(i, t, 0.75)),
    primary: s,
    neutral: r
  };
}
const as = "#2f6fde", Oc = rs("light", { r: 34, g: 34, b: 34, a: 1 }, { r: 255, g: 255, b: 255, a: 1 }, as, "#64748b"), zc = rs("dark", { r: 240, g: 240, b: 240, a: 1 }, { r: 20, g: 20, b: 20, a: 1 }, as, "#94a3b8");
function Ac(e) {
  const t = G(e.text), i = G(e.surface);
  if (!t || !i) return null;
  const s = P(G(e.highlight) ?? t), r = P(G(e.grayText) ?? Bt(t, i, 0.6));
  return {
    scheme: pa(i) < 0.4 ? "dark" : "light",
    forced: !0,
    text: P(t),
    muted: P(t),
    grid: r,
    surface: P(i),
    tooltipBackground: P(t),
    tooltipText: P(i),
    tooltipTitle: P(i),
    primary: s,
    highlight: s,
    neutral: r
  };
}
function Mc(e, t) {
  const i = t ? Ac(t) : null;
  if (i) return i;
  const s = G(e.text), r = G(e.surface), a = r !== null && pa(r) < 0.4, n = a ? zc : Oc;
  if (!s || !r) return n;
  const u = G(e.primary);
  return rs(a ? "dark" : "light", s, r, u ? P(u) : n.primary, n.neutral);
}
function Fc({ r: e, g: t, b: i }) {
  const [s, r, a] = [e / 255, t / 255, i / 255], n = Math.max(s, r, a), u = n - Math.min(s, r, a), d = (n + Math.min(s, r, a)) / 2;
  if (u === 0) return { h: 0, s: 0, l: d };
  const _ = u / (1 - Math.abs(2 * d - 1));
  return { h: ((n === s ? 60 * ((r - a) / u % 6) : n === r ? 60 * ((a - s) / u + 2) : 60 * ((s - r) / u + 4)) + 360) % 360, s: _, l: d };
}
function Qs(e, t, i) {
  const s = (1 - Math.abs(2 * i - 1)) * t, r = s * (1 - Math.abs(e / 60 % 2 - 1)), a = Math.floor(e / 60) % 6, [n, u, d] = [[s, r, 0], [r, s, 0], [0, s, r], [0, r, s], [r, 0, s], [s, 0, r]][a], _ = i - s / 2;
  return { r: (n + _) * 255, g: (u + _) * 255, b: (d + _) * 255, a: 1 };
}
const Ic = {
  light: { from: 0.7, to: 0.26 },
  dark: { from: 0.48, to: 0.82 }
}, Tt = 0.55;
function Kc(e, t, i) {
  if (t <= 0) return [];
  const s = G(e) ?? G(as), { h: r, s: a } = Fc(s), { from: n, to: u } = Ic[i];
  if (t === 1) return [hi(i === "light" ? s : Qs(r, Math.max(a, Tt), 0.68))];
  const d = Math.min(Math.max(a, Tt), 1);
  return Array.from({ length: t }, (_, I) => {
    const X = I / (t - 1);
    return hi(Qs(r, Tt + X * (d - Tt), n + X * (u - n)));
  });
}
const Uc = {
  light: ["#eb6834", "#3f6212"],
  dark: ["#ffb590", "#5a9a28"]
};
function Gc(e, t, i) {
  if (e === "segment") return Array.from({ length: t }, (a, n) => [i.text, i.highlight][n] ?? i.neutral);
  if (t === 1) return [i.text];
  const s = G(i.text), r = G(i.neutral);
  return Array.from({ length: t }, (a, n) => P(Bt(s, r, n / (t - 1))));
}
function Wc(e, t, i) {
  return t <= 0 ? [] : i.forced ? Gc(e, t, i) : e === "segment" ? Array.from({ length: t }, (s, r) => Uc[i.scheme][r] ?? i.neutral) : Kc(i.primary, t, i.scheme);
}
const Bc = [4, 3];
function fa(e, t) {
  const i = e.series ?? [], s = Wc(e.palette, i.length, t);
  return i.map((r, a) => ({
    // In forced-colours mode the emphasised (newest) series is always in the text colour: the one with the most contrast against Canvas.
    color: t.forced && r.emphasis === !0 ? t.text : s[a] ?? t.neutral,
    width: r.emphasis ? 2.5 : 1.5,
    dashed: r.style === "dashed",
    emphasis: r.emphasis === !0
  }));
}
const er = ["circle", "rect", "triangle", "rectRot", "cross", "star"];
function Hc(e, { host: t, theme: i }) {
  const s = e.series ?? [];
  if (s.length < 2) return [];
  const r = fa(e, i);
  return s.map((a, n) => ({
    label: ei(t, a),
    color: r[n].color,
    width: Math.max(2, r[n].width),
    dashed: r[n].dashed
  }));
}
const Vc = (e) => e.every((t) => t === null || Number.isInteger(t));
function qc(e, { host: t, theme: i, reducedMotion: s, onPoint: r }) {
  const a = L(t), n = e.type === "bar", u = e.series ?? [], d = fa(e, i), _ = o(t, "uiNoValueText"), I = Di(e, a, "table"), X = u.map((y, H) => {
    const { color: We, width: on, dashed: ln, emphasis: si } = d[H], Rs = { label: ei(t, y), data: [...y.values ?? []] };
    return n ? { ...Rs, backgroundColor: Js(We, 0.2), borderColor: We, borderWidth: 1.5, borderRadius: 5, borderSkipped: !1 } : {
      ...Rs,
      borderColor: We,
      backgroundColor: si ? Js(We, 0.1) : "transparent",
      fill: si ? "origin" : !1,
      borderWidth: on,
      borderDash: ln ? [...Bc] : [],
      pointRadius: si ? 4 : 2.5,
      pointHoverRadius: 6,
      pointBackgroundColor: We,
      ...i.forced ? { pointStyle: er[H % er.length] } : {},
      tension: 0.3,
      // Null is no data point. The line breaks there; it does not fall to zero.
      spanGaps: !1
    };
  }), Z = u.flatMap((y) => y.values ?? []), ii = Math.max(0, ...Z.filter((y) => typeof y == "number")) >= 1e4, we = i.forced ? { border: { dash: [1, 4] } } : {};
  return {
    type: n ? "bar" : "line",
    data: { labels: Di(e, a, "axis"), datasets: X },
    options: {
      responsive: !0,
      maintainAspectRatio: !1,
      ...s ? { animation: !1 } : {},
      interaction: { mode: "index", intersect: !1 },
      ...r ? { onClick: (y, H) => H.length > 0 ? r(H[0].index) : void 0 } : {},
      plugins: {
        // The card draws the legend as text.
        legend: { display: !1 },
        tooltip: {
          backgroundColor: i.tooltipBackground,
          titleColor: i.tooltipTitle,
          bodyColor: i.tooltipText,
          padding: 10,
          cornerRadius: 8,
          titleFont: { size: 11 },
          bodyFont: { size: 12 },
          callbacks: {
            title: (y) => I[y[0]?.dataIndex] ?? "",
            label: (y) => ` ${y.dataset.label ?? ""}: ${y.parsed.y === null || y.parsed.y === void 0 ? _ : O(y.parsed.y, a)}`
          }
        }
      },
      scales: {
        x: { grid: n ? { display: !1 } : { color: i.grid, lineWidth: 1 }, ...we, ticks: { color: i.muted, maxRotation: 0 } },
        y: {
          beginAtZero: !0,
          grid: { color: i.grid, lineWidth: 1 },
          ...we,
          ticks: {
            color: i.muted,
            ...Vc(Z) ? { precision: 0 } : {},
            callback: (y) => typeof y == "number" ? ii ? lo(y, a) : O(y, a) : y
          }
        }
      }
    }
  };
}
let Be = null;
function jc() {
  return Be || (Be = import("./chart-bundle.js"), Be.catch(() => Be = null)), Be;
}
function Yc() {
  try {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  } catch {
    return !1;
  }
}
const ga = "(forced-colors: active)";
function Xc() {
  try {
    return window.matchMedia(ga).matches;
  } catch {
    return !1;
  }
}
function ma(e) {
  const t = Xc(), i = document.createElement("span");
  i.style.cssText = `position:absolute;visibility:hidden;pointer-events:none;width:0;height:0${t ? "" : ";forced-color-adjust:none"}`, e.append(i);
  const s = (r) => (i.style.color = "", i.style.color = r, getComputedStyle(i).color);
  try {
    return Mc(
      { text: s("var(--uui-color-text)"), surface: s("var(--uui-color-surface)"), primary: s("var(--seo-notice)") },
      t ? { text: s("CanvasText"), surface: s("Canvas"), highlight: s("Highlight"), grayText: s("GrayText") } : null
    );
  } finally {
    i.remove();
  }
}
function Zc(e) {
  let t = 0;
  const i = () => {
    t || (t = requestAnimationFrame(() => {
      t = 0, e();
    }));
  }, s = new MutationObserver(i);
  s.observe(document.documentElement, { attributes: !0 }), document.body && s.observe(document.body, { attributes: !0 }), s.observe(document.head, { childList: !0 });
  const r = ["(prefers-color-scheme: dark)", ga, "(prefers-contrast: more)"].map((a) => window.matchMedia(a));
  return r.forEach((a) => a.addEventListener("change", i)), () => {
    s.disconnect(), r.forEach((a) => a.removeEventListener("change", i)), t && cancelAnimationFrame(t);
  };
}
function Jc(e, t, i) {
  const s = () => e.clientWidth > 0 && e.clientHeight > 0;
  return s() ? Promise.resolve(!t()) : new Promise((r) => {
    const a = new ResizeObserver(() => {
      t() ? (a.disconnect(), r(!1)) : s() && (a.disconnect(), r(!0));
    });
    i(() => {
      a.disconnect(), r(!1);
    }), a.observe(e);
  });
}
var Qc = Object.defineProperty, eu = Object.getOwnPropertyDescriptor, va = (e) => {
  throw TypeError(e);
}, ue = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? eu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Qc(t, i, r), r;
}, ns = (e, t, i) => t.has(e) || va("Cannot " + i), me = (e, t, i) => (ns(e, t, "read from private field"), i ? i.call(e) : t.get(e)), He = (e, t, i) => t.has(e) ? va("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), ve = (e, t, i, s) => (ns(e, t, "write to private field"), t.set(e, i), i), pe = (e, t, i) => (ns(e, t, "access private method"), i), tu = (e, t, i, s) => ({
  set _(r) {
    ve(e, t, r);
  },
  get _() {
    return me(e, t, s);
  }
}), ze, st, rt, Ye, Q, ba, os, Ni, ya, $a;
const iu = (e, t) => JSON.stringify(e ?? null) === JSON.stringify(t ?? null);
let Y = class extends k {
  constructor() {
    super(...arguments), He(this, Q), this.block = null, this.args = null, this.selectable = !1, this.embedded = !1, this._theme = null, this._hidden = [], this._failed = !1, He(this, ze, null), He(this, st, 0), He(this, rt, null), He(this, Ye, null);
  }
  connectedCallback() {
    super.connectedCallback(), ve(this, Ye, Zc(() => pe(this, Q, ba).call(this))), this.hasUpdated && pe(this, Q, Ni).call(this);
  }
  disconnectedCallback() {
    var e;
    (e = me(this, Ye)) == null || e.call(this), ve(this, Ye, null), pe(this, Q, os).call(this), super.disconnectedCallback();
  }
  updated(e) {
    if (this._theme === null) {
      this._theme = ma(this.renderRoot);
      return;
    }
    (e.has("block") || e.has("args") || e.has("selectable") || e.has("_theme")) && pe(this, Q, Ni).call(this);
  }
  // ---- Showing -----------------------------------------------------------------------------------------------------------------
  render() {
    const e = this.block;
    if (!e) return c;
    const t = la(e) === "empty", i = Je(this, e, "Title", this.args), s = t ? "" : Je(this, e, "Subtitle", this.args), r = t ? "" : oa(this, e), a = Je(this, e, "Description", this.args);
    return l`
      <figure class="chart ${this.embedded ? "embedded" : "card"}">
        <figcaption class="card-head ${this.embedded ? "visually-hidden" : ""}">
          <span class="icon-badge" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
          <span class="titles">
            <h3>${i}</h3>
            ${s ? l`<span class="muted">${s}</span>` : c}
            ${r ? l`<span class="muted small">${r}</span>` : c}
          </span>
        </figcaption>
        ${a ? l`<p class="description muted">${a}</p>` : c}
        ${t ? l`<p class="empty" role="status">${o(this, "uiNoDataText")}</p>` : pe(this, Q, $a).call(this, e, i)}
        ${ht(is(this, e.annotations, e.insights))}
      </figure>
    `;
  }
};
ze = /* @__PURE__ */ new WeakMap();
st = /* @__PURE__ */ new WeakMap();
rt = /* @__PURE__ */ new WeakMap();
Ye = /* @__PURE__ */ new WeakMap();
Q = /* @__PURE__ */ new WeakSet();
ba = function() {
  const e = ma(this.renderRoot);
  JSON.stringify(e) !== JSON.stringify(this._theme) && (this._theme = e);
};
os = function() {
  var e;
  tu(this, st)._++, (e = me(this, rt)) == null || e.call(this), ve(this, rt, null), me(this, ze)?.destroy(), ve(this, ze, null);
};
Ni = async function() {
  pe(this, Q, os).call(this);
  const e = me(this, st), t = () => e !== me(this, st);
  this._hidden = [], this._failed = !1;
  const i = this.renderRoot.querySelector("canvas"), s = this.renderRoot.querySelector(".wrap");
  if (!i || !s || !this.block || la(this.block) === "empty" || !this._theme || !await Jc(s, t, (n) => ve(this, rt, n))) return;
  let r;
  try {
    r = await jc();
  } catch {
    t() || (this._failed = !0);
    return;
  }
  if (t() || !i.isConnected) return;
  const a = qc(this.block, {
    host: this,
    theme: this._theme,
    reducedMotion: Yc(),
    onPoint: this.selectable ? (n) => this.dispatchEvent(new CustomEvent("point-select", { detail: { index: n } })) : void 0
  });
  ve(this, ze, new r.Chart(i, a));
};
ya = function(e) {
  const t = me(this, ze);
  t && (t.setDatasetVisibility(e, !t.isDatasetVisible(e)), t.update(), this._hidden = (this.block?.series ?? []).map((i, s) => !t.isDatasetVisible(s)));
};
$a = function(e, t) {
  const i = fc(this, e), s = this._theme ? Hc(e, { host: this, theme: this._theme }) : [];
  return l`
      ${s.length > 0 ? l`<ul class="legend" aria-label=${o(this, "uiChartLegendLabel")}>
            ${s.map(
    (r, a) => l`<li>
                <button type="button" aria-pressed=${this._hidden[a] ? "false" : "true"} class=${this._hidden[a] ? "off" : ""} @click=${() => pe(this, Q, ya).call(this, a)}>
                  <span
                    class="swatch"
                    style=${Os({ borderBottomWidth: `${r.width}px`, borderBottomStyle: r.dashed ? "dashed" : "solid", borderBottomColor: r.color })}
                    >${r.label}</span
                  >
                </button>
              </li>`
  )}
          </ul>` : c}
      ${this._failed ? l`<p class="muted" role="status">${o(this, "uiChartFailedText")}</p>` : c}
      <div class="wrap ${this.selectable ? "selectable" : ""}" style=${Os({ height: `${mc(e)}px` })}>
        <canvas role="img" aria-label=${hc(this, e, this.args)}></canvas>
      </div>
      <table class="visually-hidden">
        <caption>${o(this, "uiChartTableCaption", { title: t })}</caption>
        <thead>
          <tr>
            <th scope="col">${i.periodHeader}</th>
            ${i.seriesHeaders.map((r) => l`<th scope="col">${r}</th>`)}
          </tr>
        </thead>
        <tbody>
          ${i.rows.map((r) => l`<tr><th scope="row">${r.label}</th>${r.cells.map((a) => l`<td>${a}</td>`)}</tr>`)}
        </tbody>
      </table>
    `;
};
Y.styles = [
  C,
  Jt,
  w`
      :host { display: block; }
      .chart { margin: 0; }
      .chart.embedded { padding: 0; }
      .titles { display: grid; gap: 2px; }
      .titles h3 { font-size: 16px; font-weight: 700; }
      .description { margin-bottom: var(--uui-size-space-4); max-width: 70ch; font-size: 0.85rem; line-height: 1.5; }
      .empty { padding: var(--uui-size-space-5); border-radius: var(--seo-radius-sm); background: var(--seo-wash); color: var(--seo-muted); }
      .wrap { position: relative; }
      .wrap.selectable { cursor: pointer; }

      /* The legend is text. Each entry is underlined with the line it names: its colour, its width and its dash. */
      .legend { display: flex; flex-wrap: wrap; gap: var(--uui-size-1) var(--uui-size-4); list-style: none; margin: 0 0 var(--uui-size-2); padding: 0; }
      .legend button {
        font: inherit; font-size: 0.85rem; color: var(--uui-color-text); background: none; border: 0; padding: var(--uui-size-1) 0; cursor: pointer;
      }
      .legend button:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
      .swatch { padding-bottom: 2px; }
      .legend button.off .swatch { text-decoration: line-through; opacity: 0.45; }
    `
];
ue([
  h({ attribute: !1 })
], Y.prototype, "block", 2);
ue([
  h({ attribute: !1, hasChanged: (e, t) => !iu(e, t) })
], Y.prototype, "args", 2);
ue([
  h({ type: Boolean })
], Y.prototype, "selectable", 2);
ue([
  h({ type: Boolean })
], Y.prototype, "embedded", 2);
ue([
  B()
], Y.prototype, "_theme", 2);
ue([
  B()
], Y.prototype, "_hidden", 2);
ue([
  B()
], Y.prototype, "_failed", 2);
Y = ue([
  T("esatto-seo-report-chart-card")
], Y);
var su = Object.defineProperty, ru = Object.getOwnPropertyDescriptor, ls = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? ru(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && su(t, i, r), r;
};
let at = class extends k {
  constructor() {
    super(...arguments), this.report = null, this.period = "";
  }
  render() {
    const { block: e } = Ec(this.report);
    return e ? l`<div class="page"><esatto-seo-report-chart-card .block=${e}></esatto-seo-report-chart-card></div>` : c;
  }
};
at.styles = [C];
ls([
  h({ attribute: !1 })
], at.prototype, "report", 2);
ls([
  h()
], at.prototype, "period", 2);
at = ls([
  T("esatto-seo-report-comparison")
], at);
var au = Object.defineProperty, nu = Object.getOwnPropertyDescriptor, cs = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? nu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && au(t, i, r), r;
};
let nt = class extends k {
  constructor() {
    super(...arguments), this.report = null, this.period = "";
  }
  render() {
    if (!this.report) return c;
    const e = Lc(this, this.report);
    return e.state === "empty" ? l`<section class="card"><p class="muted">${o(this, "uiNoDataText")}</p></section>` : l`
      <div class="page">
        ${e.table ? Gt({ ...e.table }) : c}
        ${e.hint ? l`<p class="hint muted">${e.hint}</p>` : c}
        ${e.charts.map((t) => l`<esatto-seo-report-chart-card .block=${t}></esatto-seo-report-chart-card>`)} ${ht(e.notes)}
      </div>
    `;
  }
};
nt.styles = [C, Jt, es, w`.hint { font-size: 0.9rem; max-width: 70ch; }`];
cs([
  h({ attribute: !1 })
], nt.prototype, "report", 2);
cs([
  h()
], nt.prototype, "period", 2);
nt = cs([
  T("esatto-seo-report-queries")
], nt);
const us = w`
  .fields { display: grid; gap: var(--uui-size-space-5); }
  .field { display: grid; gap: 6px; align-content: start; }
  .field > label, fieldset > legend { font-size: 13px; font-weight: 600; line-height: 1.5; }
  fieldset { border: 0; margin: 0; padding: 0; min-width: 0; display: grid; gap: var(--uui-size-space-2); }
  fieldset > legend { padding: 0; margin-bottom: var(--uui-size-space-2); }

  input[type='text'] {
    font: inherit; box-sizing: border-box; width: 100%; max-width: 36rem; min-height: 40px; padding: 0 var(--uui-size-space-3);
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), box-shadow var(--seo-ease);
  }
  input[type='text']:hover { border-color: var(--uui-color-border-emphasis); }
  input[type='text']:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 0; border-color: var(--uui-color-focus);
    box-shadow: 0 0 0 4px color-mix(in srgb, var(--uui-color-focus) 22%, transparent);
  }
  /* Read-only (set in configuration, or a save on its way): the wash and a dashed border say it, with the words beside it. */
  input[readonly], textarea[readonly] { background: var(--seo-wash); border-style: dashed; }

  .check { display: grid; grid-template-columns: auto 1fr; column-gap: var(--uui-size-space-3); row-gap: 2px; align-items: center; min-height: 32px; }
  .check input { width: 20px; height: 20px; margin: 0; accent-color: var(--uui-color-selected); }
  .check > label { font-weight: 600; }
  .check .help, .check .lock { grid-column: 2; margin: 0; }
  .check input[aria-readonly='true'] { cursor: not-allowed; }
  .choices { display: grid; gap: var(--uui-size-space-2); }

  .lock { color: var(--seo-muted); font-size: 13px; display: inline-flex; gap: 4px; align-items: center; }
  .status-line { display: inline-flex; gap: 6px; align-items: center; min-height: 24px; font-size: 13px; }
  .status-line uui-icon { flex: none; }
  .preview { font-size: 13px; color: var(--seo-muted); overflow-wrap: anywhere; }

  .buttons { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-3); align-items: center; }
  .secondary {
    display: inline-flex; gap: var(--uui-size-space-2); align-items: center; min-height: 40px; box-sizing: border-box;
    padding: 0 var(--uui-size-space-4); font: inherit; font-weight: 600; cursor: pointer;
    border: 1px solid var(--uui-color-border-standalone); border-radius: var(--seo-radius-sm);
    background: var(--uui-color-surface); color: var(--uui-color-text);
    transition: border-color var(--seo-ease), background-color var(--seo-ease);
  }
  .secondary:hover { border-color: var(--uui-color-border-emphasis); background: var(--seo-wash); }
  .secondary[aria-disabled='true'] { cursor: not-allowed; opacity: 0.7; }
  .secondary[aria-busy='true'] { cursor: progress; }
  .secondary uui-loader-circle, .save uui-loader-circle { font-size: 16px; }
  .danger { border-color: var(--seo-critical); box-shadow: inset 3px 0 0 var(--seo-critical); }
`;
var ou = Object.defineProperty, lu = Object.getOwnPropertyDescriptor, xa = (e) => {
  throw TypeError(e);
}, mt = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? lu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && ou(t, i, r), r;
}, wa = (e, t, i) => t.has(e) || xa("Cannot " + i), Ri = (e, t, i) => (wa(e, t, "read from private field"), i ? i.call(e) : t.get(e)), pi = (e, t, i) => t.has(e) ? xa("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), tr = (e, t, i) => (wa(e, t, "access private method"), i), Dt, ds, Nt, _a, Ta;
const cu = { busy: !1, result: null, problem: null };
let be = class extends k {
  constructor() {
    super(...arguments), pi(this, Nt), this.test = cu, this.email = null, this.savedEmail = null, this._copy = null, pi(this, Dt, () => {
      this.test.busy || this.dispatchEvent(new CustomEvent("test"));
    }), pi(this, ds, async () => {
      const e = this.email;
      if (e)
        try {
          await navigator.clipboard.writeText(e), this._copy = "copied";
        } catch {
          this._copy = "failed";
        }
    });
  }
  willUpdate(e) {
    e.has("email") && (this._copy = null);
  }
  render() {
    const { busy: e, result: t, problem: i } = this.test;
    return l`
      ${this.email ? tr(this, Nt, _a).call(this, this.email) : c}
      <div class="buttons">
        <button type="button" class="save" aria-disabled=${e ? "true" : "false"} aria-busy=${e ? "true" : "false"} aria-describedby="test-help" @click=${Ri(this, Dt)}>
          ${e ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : c}${o(this, "uiTestConnectionLabel")}
        </button>
      </div>
      <p class="help muted" id="test-help">${o(this, "uiTestHelpText")}</p>
      <p class="status-line" role="status" aria-live="polite">
        ${e ? o(this, "uiTestingText") : t ? o(this, "uiTestDoneText") : c}
      </p>
      ${i ? l`<esatto-seo-report-problem .error=${i} @retry=${Ri(this, Dt)}></esatto-seo-report-problem>` : c}
      ${t ? tr(this, Nt, Ta).call(this, yl(this, t, this.savedEmail)) : c}
    `;
  }
};
Dt = /* @__PURE__ */ new WeakMap();
ds = /* @__PURE__ */ new WeakMap();
Nt = /* @__PURE__ */ new WeakSet();
_a = function(e) {
  return l`
      <div class="account">
        <p class="account-label" id="account-label">${o(this, "uiServiceAccountLabel")}</p>
        <div class="buttons">
          <code class="address" id="account-address" aria-labelledby="account-label account-address">${e}</code>
          <button type="button" class="secondary" aria-describedby="account-address" @click=${Ri(this, ds)}>
            <uui-icon name="icon-documents" aria-hidden="true"></uui-icon>${o(this, "uiCopyAddressLabel")}
          </button>
          <span class="status-line" role="status" aria-live="polite">
            ${this._copy === "copied" ? l`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${o(this, "uiCopiedText")}` : this._copy === "failed" ? l`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiCopyFailedText")}` : c}
          </span>
        </div>
        <p class="help muted">${o(this, "uiServiceAccountHelpText")}</p>
      </div>
    `;
};
Ta = function(e) {
  return l`
      <section class="result" aria-labelledby="result-title">
        <h4 id="result-title">${o(this, "uiTestResultTitle")}</h4>
        <ul>
          ${e.map(
    (t) => l`<li class="tone-${t.tone}">
              <span class="icon-badge solid round" aria-hidden="true"><uui-icon name=${t.icon}></uui-icon></span>
              <div>
                <p><strong>${t.label}:</strong> ${t.status}</p>
                ${t.title ? l`<p class="row-title">${t.title}</p>` : c} ${t.text ? l`<p class="row-text">${t.text}</p>` : c}
              </div>
            </li>`
  )}
        </ul>
      </section>
    `;
};
be.styles = [
  C,
  pt,
  us,
  w`
      :host { display: grid; gap: var(--uui-size-space-3); }
      .account { display: grid; gap: var(--uui-size-space-2); padding: var(--uui-size-space-4); border-radius: var(--seo-radius-sm); background: var(--seo-wash); border: 1px solid var(--seo-line); }
      .account-label { font-size: 13px; font-weight: 600; }
      .address { font-size: 14px; padding: 4px 8px; user-select: all; overflow-wrap: anywhere; }
      h4 { font-size: 14px; font-weight: 700; margin-bottom: var(--uui-size-space-3); }
      ul { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--uui-size-space-3); }
      li { display: flex; gap: var(--uui-size-space-3); align-items: flex-start; }
      li > div { display: grid; gap: 4px; padding-top: 6px; max-width: 80ch; line-height: 1.5; }
      .row-title { font-weight: 700; }
    `
];
mt([
  h({ attribute: !1 })
], be.prototype, "test", 2);
mt([
  h({ attribute: !1 })
], be.prototype, "email", 2);
mt([
  h({ attribute: !1 })
], be.prototype, "savedEmail", 2);
mt([
  B()
], be.prototype, "_copy", 2);
be = mt([
  T("esatto-seo-report-connection-test")
], be);
var uu = Object.defineProperty, du = Object.getOwnPropertyDescriptor, ka = (e) => {
  throw TypeError(e);
}, M = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? du(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && uu(t, i, r), r;
}, hs = (e, t, i) => t.has(e) || ka("Cannot " + i), U = (e, t, i) => (hs(e, t, "read from private field"), i ? i.call(e) : t.get(e)), Ve = (e, t, i) => t.has(e) ? ka("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), Oi = (e, t, i, s) => (hs(e, t, "write to private field"), t.set(e, i), i), E = (e, t, i) => (hs(e, t, "access private method"), i), Le, g, te, Ca, Ae, ps, fs, gs, Sa, zi, ms, Ea, vs, Ai, Rt;
const hu = { busy: !1, done: !1, problem: null }, pu = {
  notJson: "uiKeyNotJsonText",
  notServiceAccount: "uiKeyNotServiceAccountText",
  noClientEmail: "uiKeyNoClientEmailText",
  noPrivateKey: "uiKeyNoPrivateKeyText",
  tooLarge: "uiKeyTooLargeText"
};
let D = class extends k {
  constructor() {
    super(...arguments), Ve(this, g), this.mode = "none", this.email = null, this.hasDraft = !1, this.draftEmail = null, this.draftProblem = null, this.draftRevision = 0, this.confirmingRemove = !1, this.removal = hu, this.busy = !1, this.onDraft = () => {
    }, this._fileProblem = null, Ve(this, Le, null), Ve(this, ps, (e) => {
      this._fileProblem = null, this.onDraft(e.target.value);
    }), Ve(this, fs, () => {
      U(this, g, te) || this._file?.click();
    }), Ve(this, gs, async (e) => {
      const t = e.target, i = t.files?.[0];
      if (t.value = "", !i) return;
      const s = await Uo(i);
      if (!(!this.isConnected || !this._box || U(this, g, te) || !Xi(this.mode))) {
        if (!s.ok) {
          this._fileProblem = s.problem;
          return;
        }
        this._fileProblem = null, this._box.value = s.text, this.onDraft(s.text);
      }
    });
  }
  updated(e) {
    e.has("draftRevision") && e.get("draftRevision") !== void 0 && (this._box && (this._box.value = ""), this._fileProblem = null), E(this, g, Ca).call(this, e);
  }
  // "Key removed" is said in a polite line that is in the page whatever the mode, so it is announced when it is filled: after a removal the mode
  // is a different one.
  render() {
    return l`
      ${E(this, g, Sa).call(this)}
      <span class="status-line" role="status" aria-live="polite">
        ${this.removal.done ? l`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${o(this, "uiKeyRemovedText")}` : c}
      </span>
    `;
  }
};
Le = /* @__PURE__ */ new WeakMap();
g = /* @__PURE__ */ new WeakSet();
te = function() {
  return this.busy || this.removal.busy;
};
Ca = function(e) {
  const t = U(this, Le);
  if (!t) return;
  if (t === "afterRemove") {
    e.has("mode") && Xi(this.mode) && (Oi(this, Le, null), this._box?.focus());
    return;
  }
  const i = this.renderRoot.querySelector(t === "box" ? "#key" : `#key-${t}`);
  i && (Oi(this, Le, null), i.focus());
};
Ae = function(e, t, i = U(this, g, te)) {
  return () => {
    i || (Oi(this, Le, t), this.dispatchEvent(new CustomEvent(e)));
  };
};
ps = /* @__PURE__ */ new WeakMap();
fs = /* @__PURE__ */ new WeakMap();
gs = /* @__PURE__ */ new WeakMap();
Sa = function() {
  switch (this.mode) {
    case "configured":
      return l`
          <p class="status-line"><uui-icon name="icon-lock" aria-hidden="true"></uui-icon><strong>${o(this, "uiKeyConfiguredText", { email: this.email })}</strong></p>
          <p class="help muted">${o(this, "uiKeyConfiguredHelpText")}</p>
        `;
    case "configuredBroken":
      return E(this, g, zi).call(this, o(this, "uiKeyConfiguredBrokenTitle"), o(this, "uiKeyConfiguredBrokenText"));
    case "saved":
      return l`${E(this, g, Ea).call(this)} ${E(this, g, Ai).call(this)}`;
    case "replacing":
      return l`
          ${E(this, g, ms).call(this)}
          <p class="help muted">${o(this, "uiKeyReplaceHelpText")}</p>
          ${E(this, g, Rt).call(this)}
          <div class="buttons">
            <button type="button" class="secondary" id="key-cancel" aria-disabled=${U(this, g, te) ? "true" : "false"} @click=${E(this, g, Ae).call(this, "cancel-replace", "replace")}>
              ${o(this, "uiCancelLabel")}
            </button>
          </div>
        `;
    case "unreadable": {
      const { title: e, text: t } = le(this, "keyUnreadable");
      return l`${E(this, g, zi).call(this, e, t)} ${E(this, g, Rt).call(this)} ${E(this, g, Ai).call(this)}`;
    }
    default:
      return E(this, g, Rt).call(this);
  }
};
zi = function(e, t) {
  return l`
      <div class="banner tone-warning">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
        <div><h4>${e}</h4><p>${t}</p></div>
      </div>
    `;
};
ms = function() {
  return l`<p class="status-line"><uui-icon name="icon-key" aria-hidden="true"></uui-icon><strong>${o(this, "uiKeySavedText", { email: this.email })}</strong></p>`;
};
Ea = function() {
  const e = U(this, g, te);
  return l`
      ${E(this, g, ms).call(this)}
      <div class="buttons">
        <button type="button" class="secondary" id="key-replace" aria-disabled=${e ? "true" : "false"} @click=${E(this, g, Ae).call(this, "replace", "box")}>
          ${o(this, "uiKeyReplaceLabel")}
        </button>
        ${this.confirmingRemove ? c : E(this, g, vs).call(this)}
      </div>
    `;
};
vs = function() {
  const e = U(this, g, te);
  return l`<button type="button" class="secondary" id="key-remove" aria-disabled=${e ? "true" : "false"} @click=${E(this, g, Ae).call(this, "ask-remove", "confirm")}>
      ${o(this, "uiKeyRemoveLabel")}
    </button>`;
};
Ai = function() {
  const { busy: e, problem: t } = this.removal;
  return l`
      ${this.mode === "unreadable" && !this.confirmingRemove ? l`<div class="buttons">${E(this, g, vs).call(this)}</div>` : c}
      ${this.confirmingRemove ? l`<div class="confirm" role="group" aria-labelledby="key-remove-question">
            <p id="key-remove-question">${o(this, "uiKeyRemoveConfirmText")}</p>
            <div class="buttons">
              <button
                type="button"
                class="secondary danger"
                id="key-confirm"
                aria-disabled=${e || this.busy ? "true" : "false"}
                aria-busy=${e ? "true" : "false"}
                @click=${E(this, g, Ae).call(this, "confirm-remove", "afterRemove")}
              >
                ${e ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : c}${o(this, "uiKeyRemoveConfirmLabel")}
              </button>
              <button type="button" class="secondary" id="key-cancel-remove" aria-disabled=${e ? "true" : "false"} @click=${E(this, g, Ae).call(this, "cancel-remove", "remove")}>
                ${o(this, "uiCancelLabel")}
              </button>
            </div>
          </div>` : c}
      ${t ? l`<esatto-seo-report-problem .error=${t} @retry=${() => this.dispatchEvent(new CustomEvent("retry-remove"))}></esatto-seo-report-problem>` : c}
    `;
};
Rt = function() {
  const e = this.draftProblem ? pu[this.draftProblem] : null, i = (this._fileProblem === "tooLarge" ? "uiKeyTooLargeText" : this._fileProblem === "unreadable" ? "uiKeyFileUnreadableText" : null) ?? e;
  return l`
      <div class="editor">
        <label for="key">${o(this, "uiKeyPasteLabel")}</label>
        <textarea
          id="key"
          rows="6"
          autocomplete="off"
          autocapitalize="off"
          autocorrect="off"
          spellcheck="false"
          aria-describedby="key-help key-check"
          aria-invalid=${i ? "true" : "false"}
          ?readonly=${U(this, g, te)}
          @input=${U(this, ps)}
        ></textarea>
        <div class="buttons">
          <button type="button" class="secondary" id="key-choose" aria-disabled=${U(this, g, te) ? "true" : "false"} @click=${U(this, fs)}>
            <uui-icon name="icon-document" aria-hidden="true"></uui-icon>${o(this, "uiKeyChooseFileLabel")}
          </button>
          <input type="file" id="key-file" class="visually-hidden" accept=".json,application/json" tabindex="-1" aria-hidden="true" @change=${U(this, gs)} />
        </div>
        <p class="help muted" id="key-help">${o(this, "uiKeyHelpText")}</p>
        <div id="key-check" role="status" aria-live="polite">
          ${i ? l`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, i, { max: It / 1024 })}</p>
                <p class="help muted">${o(this, "uiKeyCheckHintText")}</p>` : this.hasDraft ? l`<p class="status-line"><uui-icon name="icon-check" aria-hidden="true"></uui-icon>${o(this, "uiKeyDraftText", { email: this.draftEmail })}</p>` : c}
        </div>
      </div>
    `;
};
D.styles = [
  C,
  pt,
  us,
  w`
      :host { display: grid; gap: var(--uui-size-space-3); }
      h4 { font-size: 14px; font-weight: 700; }
      .confirm {
        display: grid; gap: var(--uui-size-space-3); max-width: 70ch; padding: var(--uui-size-space-4);
        border: 1px solid var(--seo-line); border-radius: var(--seo-radius-sm); background: var(--seo-wash);
      }
    `
];
M([
  h()
], D.prototype, "mode", 2);
M([
  h({ attribute: !1 })
], D.prototype, "email", 2);
M([
  h({ type: Boolean })
], D.prototype, "hasDraft", 2);
M([
  h({ attribute: !1 })
], D.prototype, "draftEmail", 2);
M([
  h({ attribute: !1 })
], D.prototype, "draftProblem", 2);
M([
  h({ type: Number })
], D.prototype, "draftRevision", 2);
M([
  h({ type: Boolean })
], D.prototype, "confirmingRemove", 2);
M([
  h({ attribute: !1 })
], D.prototype, "removal", 2);
M([
  h({ type: Boolean })
], D.prototype, "busy", 2);
M([
  h({ attribute: !1 })
], D.prototype, "onDraft", 2);
M([
  B()
], D.prototype, "_fileProblem", 2);
M([
  ir("#key")
], D.prototype, "_box", 2);
M([
  ir("#key-file")
], D.prototype, "_file", 2);
D = M([
  T("esatto-seo-report-key-field")
], D);
const fu = [
  {
    textKey: "uiGuideStepProjectText",
    links: [{ labelKey: "uiGuideLinkProjectLabel", href: "https://docs.cloud.google.com/resource-manager/docs/creating-managing-projects" }]
  },
  {
    textKey: "uiGuideStepApisText",
    links: [
      { labelKey: "uiGuideLinkApisLabel", href: "https://support.google.com/googleapi/answer/6158841" },
      { labelKey: "uiGuideLinkAnalyticsApiLabel", href: "https://console.cloud.google.com/apis/library/analyticsdata.googleapis.com" },
      { labelKey: "uiGuideLinkSearchConsoleApiLabel", href: "https://console.cloud.google.com/apis/library/searchconsole.googleapis.com" }
    ]
  },
  {
    textKey: "uiGuideStepAccountText",
    links: [
      { labelKey: "uiGuideLinkServiceAccountLabel", href: "https://docs.cloud.google.com/iam/docs/service-accounts-create" },
      { labelKey: "uiGuideLinkKeyLabel", href: "https://docs.cloud.google.com/iam/docs/keys-create-delete" }
    ]
  },
  {
    textKey: "uiGuideStepAccessText",
    withEmail: !0,
    links: [
      { labelKey: "uiGuideLinkAnalyticsUsersLabel", href: "https://support.google.com/analytics/answer/9305788" },
      { labelKey: "uiGuideLinkSearchConsoleUsersLabel", href: "https://support.google.com/webmasters/answer/7687615" }
    ]
  },
  {
    textKey: "uiGuideStepFormText",
    links: [
      { labelKey: "uiGuideLinkPropertyIdLabel", href: "https://developers.google.com/analytics/devguides/reporting/data/v1/property-id" },
      { labelKey: "uiGuideLinkPropertyTypesLabel", href: "https://support.google.com/webmasters/answer/34592" }
    ]
  },
  { textKey: "uiGuideStepTestText", links: [] }
];
var gu = Object.defineProperty, mu = Object.getOwnPropertyDescriptor, La = (e) => {
  throw TypeError(e);
}, bs = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? mu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && gu(t, i, r), r;
}, vu = (e, t, i) => t.has(e) || La("Cannot " + i), bu = (e, t, i) => t.has(e) ? La("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), yu = (e, t, i) => (vu(e, t, "access private method"), i), Mi, Pa;
let ot = class extends k {
  constructor() {
    super(...arguments), bu(this, Mi), this.open = !1, this.email = null;
  }
  render() {
    return l`
      <details class="card" ?open=${this.open}>
        <summary>
          <span class="icon-badge round" aria-hidden="true"><uui-icon name="icon-help-alt"></uui-icon></span>
          <span class="title">${o(this, "uiGuideTitle")}</span>
        </summary>
        <div class="body">
          <p>${o(this, "uiGuideIntroText")}</p>
          <ol>
            ${fu.map(
      (e) => l`<li>
                <p>${o(this, e.textKey, e.withEmail ? { email: this.email } : void 0)}</p>
                ${e.links.length > 0 ? l`<p class="links">${e.links.map((t) => yu(this, Mi, Pa).call(this, t))}</p>` : ""}
              </li>`
    )}
          </ol>
        </div>
      </details>
    `;
  }
};
Mi = /* @__PURE__ */ new WeakSet();
Pa = function(e) {
  return l`<a href=${e.href} target="_blank" rel="noopener noreferrer"
      >${o(this, e.labelKey)}<span class="visually-hidden"> ${o(this, "uiNewTabText")}</span
      ><uui-icon name="icon-out" aria-hidden="true"></uui-icon
    ></a>`;
};
ot.styles = [
  C,
  w`
      summary {
        display: flex; gap: var(--uui-size-space-3); align-items: center; cursor: pointer; list-style: none;
        min-height: 40px; border-radius: var(--seo-radius-sm);
      }
      summary::-webkit-details-marker { display: none; }
      /* The open/closed state in a shape as well as by the panel itself: a chevron that turns. */
      summary::after {
        content: ''; width: 8px; height: 8px; margin-left: auto; border-right: 2px solid currentColor; border-bottom: 2px solid currentColor;
        transform: rotate(45deg); transition: transform var(--seo-ease);
      }
      details[open] > summary::after { transform: rotate(225deg); }
      .title { font-size: 16px; font-weight: 700; }
      .body { display: grid; gap: var(--uui-size-space-4); margin-top: var(--uui-size-space-4); max-width: 80ch; line-height: 1.55; }
      ol { margin: 0; padding-left: var(--uui-size-space-6); display: grid; gap: var(--uui-size-space-4); }
      li { padding-left: var(--uui-size-space-2); }
      .links { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-5); margin-top: var(--uui-size-space-2); font-size: 13px; }
      .links a { display: inline-flex; gap: 4px; align-items: center; }
      .links uui-icon { font-size: 12px; }
    `
];
bs([
  h({ type: Boolean })
], ot.prototype, "open", 2);
bs([
  h({ attribute: !1 })
], ot.prototype, "email", 2);
ot = bs([
  T("esatto-seo-report-setup-guide")
], ot);
var $u = Object.defineProperty, xu = Object.getOwnPropertyDescriptor, Da = (e) => {
  throw TypeError(e);
}, vt = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? xu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && $u(t, i, r), r;
}, ys = (e, t, i) => t.has(e) || Da("Cannot " + i), m = (e, t, i) => (ys(e, t, "read from private field"), i ? i.call(e) : t.get(e)), qe = (e, t, i) => t.has(e) ? Da("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), wu = (e, t, i, s) => (ys(e, t, "write to private field"), t.set(e, i), i), f = (e, t, i) => (ys(e, t, "access private method"), i), Xe, $, p, Na, ne, $s, bt, ye, yt, Ge, xs, Ra, Oa, za, Aa, Ma, Fa, Ia, Ka;
let $e = class extends k {
  constructor() {
    super(), qe(this, p), this.status = null, this.onSaved = () => {
    }, this.onEngaged = () => {
    }, this._version = 0, qe(this, Xe), qe(this, $, new xl({ getSettings: Sn, putSettings: En, testConnection: Ln, startFetch: ar, getUserGroups: Pn }, () => f(this, p, Na).call(this), {
      saved: () => this.onSaved(),
      fetchStarted: (e) => this.dispatchEvent(new CustomEvent(Or, { detail: { progress: e }, bubbles: !0, composed: !0 }))
    })), qe(this, $s, (e) => m(this, $).setDraftKey(e)), qe(this, bt, (e) => (t) => {
      e && t.preventDefault();
    }), this.addEventListener("focusin", () => this.onEngaged()), this.addEventListener("input", () => this.onEngaged());
  }
  connectedCallback() {
    super.connectedCallback(), m(this, $).connect();
  }
  disconnectedCallback() {
    m(this, $).disconnect(), super.disconnectedCallback();
  }
  willUpdate() {
    m(this, Xe) === void 0 && this.status && wu(this, Xe, this.status.setup !== "ready");
  }
  // ---- Showing ------------------------------------------------------------------------------------------------------------------
  render() {
    const e = m(this, $).state;
    if (e.loadProblem)
      return l`<esatto-seo-report-problem .error=${e.loadProblem} @retry=${() => {
        m(this, $).retryLoad();
      }}></esatto-seo-report-problem>`;
    if (!e.saved || !e.form)
      return l`<section class="card"><p class="muted loading" role="status"><uui-loader-circle aria-hidden="true"></uui-loader-circle>${o(this, "uiSettingsLoadingText")}</p></section>`;
    const t = e.draftEmail ?? e.test.result?.serviceAccountEmail ?? e.saved.key.serviceAccountEmail;
    return l`
      <div class="page">
        <p class="intro">${o(this, "uiSettingsIntroText")}</p>
        ${e.saved.locked.length > 0 ? l`<p class="lock"><uui-icon name="icon-lock" aria-hidden="true"></uui-icon>${o(this, "uiLockedFieldsText")}</p>` : c}
        <esatto-seo-report-setup-guide ?open=${m(this, Xe) ?? !0} .email=${t}></esatto-seo-report-setup-guide>
        ${f(this, p, Ra).call(this, e, e.saved, e.form, t)} ${f(this, p, za).call(this, e, e.saved, e.form)} ${f(this, p, Ma).call(this, e, e.saved, e.form)}
        ${f(this, p, Ka).call(this, e, t)}
      </div>
    `;
  }
};
Xe = /* @__PURE__ */ new WeakMap();
$ = /* @__PURE__ */ new WeakMap();
p = /* @__PURE__ */ new WeakSet();
Na = function() {
  this._version += 1, m(this, $).engaged && this.onEngaged();
};
ne = function(e) {
  m(this, $).edit(e), this.requestUpdate();
};
$s = /* @__PURE__ */ new WeakMap();
bt = /* @__PURE__ */ new WeakMap();
ye = function(e, t, i) {
  return dl(e.locked, t, i.save.busy);
};
yt = function(e, t, ...i) {
  return [...i, `${e}-help`, ...t.locked ? [`${e}-lock`] : []].join(" ");
};
Ge = function(e, t) {
  return t.locked ? l`<span class="lock" id="${e}-lock"><uui-icon name="icon-lock" aria-hidden="true"></uui-icon>${o(this, "uiSetInConfigurationLabel")}</span>` : c;
};
xs = function(e, t, i, s, r, a) {
  return l`
      <div class="field">
        <label for=${e}>${o(this, t)}</label>
        <input
          type="text"
          id=${e}
          autocomplete="off"
          spellcheck="false"
          .value=${W(s)}
          ?readonly=${r.readonly}
          aria-readonly=${r.readonly ? "true" : "false"}
          aria-describedby=${f(this, p, yt).call(this, e, r)}
          @input=${(n) => a(n.target.value)}
        />
        ${f(this, p, Ge).call(this, e, r)}
        <p class="help muted" id="${e}-help">${o(this, i)}</p>
      </div>
    `;
};
Ra = function(e, t, i, s) {
  return l`
      <section class="card" aria-labelledby="connection-title">
        ${ce("icon-link", "connection-title", o(this, "uiConnectionTitle"))}
        <div class="fields">
          ${f(this, p, xs).call(this, "ga4", "uiGa4PropertyLabel", "uiGa4PropertyHelpText", i.ga4PropertyId, f(this, p, ye).call(this, t, "ga4PropertyId", e), (r) => f(this, p, ne).call(this, { ga4PropertyId: r }))}
          ${f(this, p, Oa).call(this, e, t, i)}
          <div class="field" role="group" aria-labelledby="key-legend">
            <p class="legend" id="key-legend">${o(this, "uiKeyLegendLabel")}</p>
            <esatto-seo-report-key-field
              .mode=${m(this, $).keyMode}
              .email=${t.key.serviceAccountEmail}
              ?hasDraft=${e.hasDraftKey}
              .draftEmail=${e.draftEmail}
              .draftProblem=${e.draftProblem}
              .draftRevision=${e.draftRevision}
              ?confirmingRemove=${e.confirmingRemove}
              .removal=${e.remove}
              ?busy=${e.save.busy}
              .onDraft=${m(this, $s)}
              @replace=${() => m(this, $).replaceKey()}
              @cancel-replace=${() => m(this, $).cancelReplace()}
              @ask-remove=${() => m(this, $).askRemove()}
              @cancel-remove=${() => m(this, $).cancelRemove()}
              @confirm-remove=${() => {
    m(this, $).removeKey();
  }}
              @retry-remove=${() => {
    m(this, $).removeKey();
  }}
            ></esatto-seo-report-key-field>
          </div>
          <esatto-seo-report-connection-test
            .test=${e.test}
            .email=${s}
            .savedEmail=${t.key.serviceAccountEmail}
            @test=${() => {
    m(this, $).test();
  }}
          ></esatto-seo-report-connection-test>
        </div>
      </section>
    `;
};
Oa = function(e, t, i) {
  const s = f(this, p, ye).call(this, t, "searchConsoleSite", e);
  if (s.locked)
    return l`
        <fieldset>
          <legend id="sc-legend">${o(this, "uiSearchConsoleLegendLabel")}</legend>
          <input type="text" id="sc" readonly aria-readonly="true" aria-labelledby="sc-legend" aria-describedby="sc-lock" .value=${t.settings.searchConsoleSite ?? ""} />
          ${f(this, p, Ge).call(this, "sc", s)}
        </fieldset>
      `;
  const r = i.searchConsoleKind, a = ll(r, i.searchConsoleText), n = (d) => () => f(this, p, ne).call(this, { searchConsoleKind: d }), u = a.warning === "looksLikeUrl" ? "uiSearchConsoleLooksLikeUrlText" : a.warning === "missingScheme" ? "uiSearchConsoleMissingSchemeText" : null;
  return l`
      <fieldset aria-describedby="sc-help">
        <legend>${o(this, "uiSearchConsoleLegendLabel")}</legend>
        <div class="choices">
          ${["domain", "prefix"].map(
    (d) => l`<div class="check">
              <input
                type="radio"
                id="sc-${d}"
                name="sc-kind"
                .checked=${W(r === d)}
                @click=${m(this, bt).call(this, s.readonly)}
                @change=${n(d)}
              />
              <label for="sc-${d}">${o(this, d === "domain" ? "uiSearchConsoleDomainLabel" : "uiSearchConsolePrefixLabel")}</label>
            </div>`
  )}
        </div>
        <div class="field">
          <label for="sc">${o(this, r === "domain" ? "uiSearchConsoleDomainInputLabel" : "uiSearchConsolePrefixInputLabel")}</label>
          <input
            type="text"
            id="sc"
            autocomplete="off"
            spellcheck="false"
            .value=${W(i.searchConsoleText)}
            ?readonly=${s.readonly}
            aria-readonly=${s.readonly ? "true" : "false"}
            aria-invalid=${a.line === "refused" ? "true" : "false"}
            aria-describedby="sc-sent sc-warning"
            @input=${(d) => f(this, p, ne).call(this, { searchConsoleText: d.target.value })}
          />
          <p class="preview" id="sc-sent">${a.line === "preview" ? o(this, "uiSearchConsoleSentText", { value: a.stored }) : c}</p>
          <div id="sc-warning" role="status" aria-live="polite">
            ${a.line === "refused" ? l`<p class="error"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiSearchConsoleRefusedText")}</p>
                  ${u ? l`<p class="help">${o(this, u)}</p>` : c}` : a.line === "warning" && u ? l`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, u)} ${o(this, "uiSearchConsoleSavedAsDomainText")}</p>` : c}
          </div>
        </div>
        <p class="help muted" id="sc-help">${o(this, "uiSearchConsoleHelpText")}</p>
      </fieldset>
    `;
};
za = function(e, t, i) {
  const s = f(this, p, ye).call(this, t, "brandTerms", e), r = Do(i.brandTerms);
  return l`
      <section class="card" aria-labelledby="report-title">
        ${ce("icon-chart-curve", "report-title", o(this, "uiReportSettingsTitle"))}
        <div class="fields">
          <div class="editor">
            <label for="brand">${o(this, "uiBrandTermsLabel")}</label>
            <textarea
              id="brand"
              rows="4"
              spellcheck="false"
              .value=${W(i.brandTerms)}
              ?readonly=${s.readonly}
              aria-readonly=${s.readonly ? "true" : "false"}
              aria-describedby=${f(this, p, yt).call(this, "brand", s, "brand-counter", "brand-warnings", "brand-change")}
              @input=${(a) => f(this, p, ne).call(this, { brandTerms: a.target.value })}
            ></textarea>
            <div class="counters">
              <span class="counter ${r.tooMany ? "over" : ""}" id="brand-counter">
                ${r.tooMany ? l`<uui-icon name="icon-alert" aria-hidden="true"></uui-icon>` : c}${o(this, "uiBrandTermsCounterText", {
    count: r.count,
    max: De,
    maxLength: Mt
  })}
              </span>
            </div>
            <div id="brand-warnings">
              ${r.tooMany ? l`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiTermsTooManyText", { max: De })}</p>` : c}
              ${r.tooLong > 0 ? l`<p class="warning"><uui-icon name="icon-alert" aria-hidden="true"></uui-icon>${o(this, "uiBrandTermsTooLongText", {
    count: r.tooLong,
    max: Mt
  })}</p>` : c}
            </div>
            ${f(this, p, Ge).call(this, "brand", s)}
            <p class="help muted" id="brand-help">${o(this, "uiBrandTermsHelpText")}</p>
            <p class="help muted" id="brand-change"><uui-icon name="icon-info" aria-hidden="true"></uui-icon> ${o(this, "uiBrandTermsChangeText")}</p>
          </div>
          ${f(this, p, xs).call(this, "display-name", "uiDisplayNameLabel", "uiDisplayNameHelpText", i.displayName, f(this, p, ye).call(this, t, "displayName", e), (a) => f(this, p, ne).call(this, { displayName: a }))}
          ${f(this, p, Aa).call(this, e, t, i)}
        </div>
      </section>
    `;
};
Aa = function(e, t, i) {
  const s = f(this, p, ye).call(this, t, "findingsUserGroups", e), r = (n, u) => {
    const d = i.findingsUserGroups.filter((_) => _.toLowerCase() !== n.toLowerCase());
    f(this, p, ne).call(this, { findingsUserGroups: u ? [...d, n] : d });
  };
  let a;
  if (e.groupsProblem)
    a = l`<esatto-seo-report-problem .error=${e.groupsProblem} @retry=${() => {
      m(this, $).retryGroups();
    }}></esatto-seo-report-problem>`;
  else if (!e.groups)
    a = l`<p class="muted loading" role="status"><uui-loader-circle aria-hidden="true"></uui-loader-circle>${o(this, "uiGroupsLoadingText")}</p>`;
  else {
    const n = gl(e.groups, i.findingsUserGroups);
    a = n.length === 0 ? l`<p class="muted">${o(this, "uiGroupsEmptyText")}</p>` : l`<div class="choices">
              ${n.map(
      (u, d) => l`<div class="check">
                  <input
                    type="checkbox"
                    id="group-${d}"
                    .checked=${W(u.selected)}
                    aria-readonly=${s.readonly ? "true" : "false"}
                    @click=${m(this, bt).call(this, s.readonly)}
                    @change=${(_) => r(u.alias, _.target.checked)}
                  />
                  <label for="group-${d}">${u.known ? u.name : o(this, "uiGroupUnknownText", { alias: u.alias })}</label>
                </div>`
    )}
            </div>`;
  }
  return l`
      <fieldset aria-describedby=${f(this, p, yt).call(this, "groups", s)}>
        <legend>${o(this, "uiFindingsGroupsLegendLabel")}</legend>
        ${a} ${f(this, p, Ge).call(this, "groups", s)}
        <p class="help muted" id="groups-help">${o(this, "uiFindingsGroupsHelpText")}</p>
      </fieldset>
    `;
};
Ma = function(e, t, i) {
  const s = f(this, p, ye).call(this, t, "autoFetch", e), { busy: r, alreadyRunning: a, problem: n } = e.fetch;
  return l`
      <section class="card" aria-labelledby="fetching-title">
        ${ce("icon-sync", "fetching-title", o(this, "uiFetchingTitle"))}
        <div class="fields">
          <div class="check">
            <input
              type="checkbox"
              id="auto-fetch"
              .checked=${W(i.autoFetch)}
              aria-readonly=${s.readonly ? "true" : "false"}
              aria-describedby=${f(this, p, yt).call(this, "auto-fetch", s)}
              @click=${m(this, bt).call(this, s.readonly)}
              @change=${(u) => f(this, p, ne).call(this, { autoFetch: u.target.checked })}
            />
            <label for="auto-fetch">${o(this, "uiAutoFetchLabel")}</label>
            ${f(this, p, Ge).call(this, "auto-fetch", s)}
            <p class="help muted" id="auto-fetch-help">${o(this, "uiAutoFetchHelpText")}</p>
          </div>
          <div class="editor">
            <div class="buttons">
              <button
                type="button"
                class="secondary"
                aria-disabled=${r ? "true" : "false"}
                aria-busy=${r ? "true" : "false"}
                aria-describedby="fetch-help"
                @click=${() => {
    m(this, $).fetchNow();
  }}
              >
                ${r ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : l`<uui-icon name="icon-cloud-download" aria-hidden="true"></uui-icon>`}${o(this, "uiFetchNowLabel")}
              </button>
            </div>
            <p class="help muted" id="fetch-help">${o(this, "uiFetchNowHelpText")}</p>
            <div role="status" aria-live="polite">
              <p class="status-line">${f(this, p, Fa).call(this)}</p>
              ${a ? f(this, p, Ia).call(this) : c}
            </div>
            ${n ? l`<esatto-seo-report-problem .error=${n} @retry=${() => {
    m(this, $).fetchNow();
  }}></esatto-seo-report-problem>` : c}
          </div>
        </div>
      </section>
    `;
};
Fa = function() {
  const e = this.status?.fetch;
  if (!e) return c;
  const t = or(e);
  return t.phase === "starting" ? o(this, "uiProgressStartingText") : t.phase === "running" ? o(this, "uiProgressText", { done: t.done, total: t.total, period: t.currentPeriod }) : o(this, "uiFetchIdleText");
};
Ia = function() {
  const { title: e, text: t } = le(this, "alreadyRunning");
  return l`
      <div class="banner tone-notice">
        <span class="icon-badge solid round" aria-hidden="true"><uui-icon name="icon-info"></uui-icon></span>
        <div><h4>${e}</h4><p>${t}</p></div>
      </div>
    `;
};
Ka = function(e, t) {
  const { busy: i, done: s, problem: r } = e.save, a = m(this, $).dirty, n = i || e.remove.busy;
  return l`
      <section class="card save-bar" aria-label=${o(this, "uiSaveSettingsLabel")}>
        <div class="buttons">
          <button
            type="button"
            class="save"
            aria-disabled=${n ? "true" : "false"}
            aria-busy=${i ? "true" : "false"}
            aria-describedby="save-help"
            @click=${() => {
    m(this, $).save();
  }}
          >
            ${i ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>` : c}${o(this, "uiSaveSettingsLabel")}
          </button>
          <button
            type="button"
            class="secondary"
            aria-disabled=${!a || n ? "true" : "false"}
            @click=${() => a ? m(this, $).revert() : void 0}
          >
            ${o(this, "uiDiscardLabel")}
          </button>
          ${a ? l`<span class="status-line"><uui-icon name="icon-edit" aria-hidden="true"></uui-icon>${o(this, "uiUnsavedText")}</span>` : c}
        </div>
        <p class="help muted" id="save-help">${o(this, "uiSaveHelpText")}</p>
        <p class="status-line" role="status" aria-live="polite">
          ${i ? l`<uui-loader-circle aria-hidden="true"></uui-loader-circle>${o(this, "uiSavingText")}` : s ? l`<uui-icon name="icon-check" aria-hidden="true"></uui-icon>${o(this, "uiSettingsSavedText")}` : c}
        </p>
        ${r ? l`<esatto-seo-report-problem .error=${r} .extraArgs=${{ email: t }} @retry=${() => {
    m(this, $).save();
  }}></esatto-seo-report-problem>` : c}
      </section>
    `;
};
$e.styles = [
  C,
  pt,
  us,
  w`
      .intro { max-width: 80ch; line-height: 1.55; }
      .legend { font-size: 13px; font-weight: 600; line-height: 1.5; }
      .loading { display: flex; align-items: center; gap: var(--uui-size-space-3); }
      h4 { font-size: 14px; font-weight: 700; }
      .save-bar { display: grid; gap: var(--uui-size-space-3); }
    `
];
vt([
  h({ attribute: !1 })
], $e.prototype, "status", 2);
vt([
  h({ attribute: !1 })
], $e.prototype, "onSaved", 2);
vt([
  h({ attribute: !1 })
], $e.prototype, "onEngaged", 2);
vt([
  B()
], $e.prototype, "_version", 2);
$e = vt([
  T("esatto-seo-report-settings")
], $e);
var _u = Object.defineProperty, Tu = Object.getOwnPropertyDescriptor, ws = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Tu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && _u(t, i, r), r;
};
let lt = class extends k {
  constructor() {
    super(...arguments), this.report = null, this.period = "";
  }
  render() {
    if (!this.report) return c;
    const e = Pc(this, this.report);
    return l`
      <section class="card" aria-labelledby="title">
        <div class="card-head">
          <span class="icon-badge tone-warning" aria-hidden="true"><uui-icon name="icon-alert"></uui-icon></span>
          <h3 id="title">${o(this, "uiTabToFixLabel")}</h3>
        </div>
        ${e.length > 0 ? l`<p class="muted">${o(this, "uiToFixHelpText")}</p>${ht(e)}` : l`<p class="muted">${o(this, "uiToFixEmptyText")}</p>`}
      </section>
    `;
  }
};
lt.styles = [C, Jt];
ws([
  h({ attribute: !1 })
], lt.prototype, "report", 2);
ws([
  h()
], lt.prototype, "period", 2);
lt = ws([
  T("esatto-seo-report-to-fix")
], lt);
var ku = Object.defineProperty, Cu = Object.getOwnPropertyDescriptor, _s = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Cu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && ku(t, i, r), r;
};
let ct = class extends k {
  constructor() {
    super(...arguments), this.report = null, this.period = "";
  }
  render() {
    if (!this.report) return c;
    const e = Dc(this, this.report);
    return e.state === "empty" ? l`<section class="card"><p class="muted">${o(this, "uiNoDataText")}</p></section>` : l`
      <div class="page">
        ${e.queries ? Gt({ ...e.queries }) : c} ${e.pages ? Gt({ ...e.pages }) : c}
        ${ht(e.note ? [e.note] : [])}
      </div>
    `;
  }
};
ct.styles = [C, Jt, es];
_s([
  h({ attribute: !1 })
], ct.prototype, "report", 2);
_s([
  h()
], ct.prototype, "period", 2);
ct = _s([
  T("esatto-seo-report-top-pages")
], ct);
const Su = {
  daily: null,
  months: [],
  rollingDays: null,
  pendingMonth: null,
  loading: !1,
  problem: null,
  day: null,
  sources: null,
  sourcesLoading: !1,
  sourcesProblem: null
};
class Eu {
  #a;
  #f;
  #l = Su;
  #e = !1;
  // Bumped by connect and disconnect: an awaited call that started under an earlier one is dropped when it returns.
  #i = 0;
  // Bumped by every read of a curve (and of a day) and by disconnect: only the newest is used.
  #s = 0;
  #n = 0;
  // The month last asked for ("" is the rolling window): what Try again repeats.
  #u = "";
  constructor(t, i) {
    this.#a = t, this.#f = i;
  }
  get state() {
    return this.#l;
  }
  /** The month the picker stands on: the one being read, else the one shown. */
  get selected() {
    return this.#l.pendingMonth ?? this.#l.daily?.window.month ?? "";
  }
  // ---- Lifetime ------------------------------------------------------------------------------------------------------------------
  /** The card is on the page: reads the curve unless it already holds one. Resolves when that read is over. */
  async connect() {
    this.#e = !0, this.#i++, this.#l.daily === null && await this.#d(this.#u);
  }
  /** The card has left the page: nothing that is on its way may touch anything. */
  disconnect() {
    this.#e = !1, this.#i++, this.#s++, this.#n++;
  }
  #t(t) {
    this.#l = { ...this.#l, ...t }, this.#f();
  }
  // ---- Reading -------------------------------------------------------------------------------------------------------------------
  async #d(t) {
    const i = ++this.#s, s = this.#i, r = () => this.#e && s === this.#i && i === this.#s;
    this.#u = t, this.#n++, this.#t({
      loading: !0,
      problem: null,
      daily: null,
      pendingMonth: t,
      day: null,
      sources: null,
      sourcesLoading: !1,
      sourcesProblem: null
    });
    try {
      const a = await this.#a.getDaily(t === "" ? void 0 : t);
      if (!r()) return;
      this.#t({
        daily: a,
        months: a.months?.length ? [...a.months] : this.#l.months,
        rollingDays: a.window?.month == null ? a.windowDays : this.#l.rollingDays,
        pendingMonth: null,
        loading: !1
      });
    } catch (a) {
      r() && this.#t({ problem: N(a), loading: !1 });
    }
  }
  /** Shows the curve of another month ("" is the rolling window). A month the server does not offer is ignored. */
  async selectMonth(t) {
    const { months: i, problem: s } = this.#l;
    this.#e && (t === this.selected && s === null || i.length > 0 && !i.includes(t) || await this.#d(t));
  }
  /** Shows where one day's visits came from; no day clears it. */
  async selectDay(t) {
    if (!this.#e) return;
    const i = ++this.#n, s = this.#i, r = () => this.#e && s === this.#i && i === this.#n;
    if (!t) {
      this.#t({ day: null, sources: null, sourcesLoading: !1, sourcesProblem: null });
      return;
    }
    this.#t({ day: t, sources: null, sourcesLoading: !0, sourcesProblem: null });
    try {
      const a = await this.#a.getDailySources(t);
      r() && this.#t({ sources: a, sourcesLoading: !1 });
    } catch (a) {
      r() && this.#t({ sourcesProblem: N(a), sourcesLoading: !1 });
    }
  }
  /** "Try again": repeats the read that failed (the sources of the day, or else the curve). */
  async retry() {
    const { sourcesProblem: t, problem: i, day: s } = this.#l;
    t && s ? await this.selectDay(s) : i && await this.#d(this.#u);
  }
}
function Lu(e, t) {
  if (typeof e != "number" || !Number.isFinite(e) || e <= 0) return null;
  const i = Math.min(e, t);
  return i > 0 ? i : null;
}
function Pu(e) {
  return {
    period: typeof e.textArgs?.period == "string" ? e.textArgs.period : null,
    windowDays: e.windowDays,
    provisionalDays: Lu(e.textArgs?.provisionalDays, e.windowDays)
  };
}
function Du(e, t, i) {
  return t.map((s) => ({
    value: s,
    label: s === "" ? o(e, "uiDailyRollingLabel", { windowDays: i }) : et(s, L(e))
  }));
}
function Nu(e, t) {
  const i = L(e), s = t.windowDays > 0, r = t.window?.month ?? null;
  let a = null;
  if (s && typeof t.changePercent == "number") {
    const n = Yt(e, { direction: t.changePercent >= 0 ? "up" : "down", change_percent: t.changePercent, text: null })?.pill;
    n && (a = { ...n, against: r ? o(e, "uiDailyAgainstMonthText") : o(e, "uiDailyAgainstDaysText", { windowDays: t.windowDays }) });
  }
  return {
    selected: r ?? "",
    hasDays: s,
    total: O(t.total, i),
    change: a,
    noComparison: s && t.changePercent === null,
    peak: t.peak ? o(e, "uiDailyPeakText", { day: zt(t.peak.date, i), sessions: O(t.peak.sessions, i) }) : "",
    days: (t.days ?? []).map((n) => ({
      date: n.date,
      label: o(e, "uiDailyDayOptionText", { day: zt(n.date, i), sessions: O(n.sessions, i) })
    })),
    textArgs: Pu(t),
    chart: t.chart
  };
}
var Ru = Object.defineProperty, Ou = Object.getOwnPropertyDescriptor, Ua = (e) => {
  throw TypeError(e);
}, Ga = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Ou(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Ru(t, i, r), r;
}, Ts = (e, t, i) => t.has(e) || Ua("Cannot " + i), A = (e, t, i) => (Ts(e, t, "read from private field"), i ? i.call(e) : t.get(e)), fi = (e, t, i) => t.has(e) ? Ua("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), zu = (e, t, i, s) => (Ts(e, t, "write to private field"), t.set(e, i), i), _e = (e, t, i) => (Ts(e, t, "access private method"), i), z, Qe, ie, Wa, Ba, Ha, Va, qa, ja;
let Ht = class extends k {
  constructor() {
    super(...arguments), fi(this, ie), this._version = 0, fi(this, z, new Eu({ getDaily: xn, getDailySources: wn }, () => this._version += 1)), fi(this, Qe, null);
  }
  connectedCallback() {
    super.connectedCallback(), A(this, z).connect();
  }
  disconnectedCallback() {
    A(this, z).disconnect(), super.disconnectedCallback();
  }
  // ---- Showing -------------------------------------------------------------------------------------------------------------
  render() {
    const { daily: e, loading: t, problem: i } = A(this, z).state, s = e ? _e(this, ie, Wa).call(this, e) : null;
    return l`
      <section class="card" aria-labelledby="title">
        <header>
          <div class="card-head">
            <span class="icon-badge" aria-hidden="true"><uui-icon name="icon-calendar"></uui-icon></span>
            <h3 id="title">${o(this, "uiDailyTitle")}</h3>
          </div>
          <div class="controls">${_e(this, ie, Ba).call(this)} ${s ? _e(this, ie, Ha).call(this, s) : c}</div>
          ${s ? _e(this, ie, Va).call(this, s) : c}
        </header>

        ${t ? l`<p class="muted" role="status">${o(this, "uiDailyLoadingText")}</p>` : c}
        ${i ? l`<esatto-seo-report-problem .error=${i} @retry=${() => {
      A(this, z).retry();
    }}></esatto-seo-report-problem>` : c}
        ${s ? l`<esatto-seo-report-chart-card
              embedded
              selectable
              .block=${s.chart}
              .args=${s.textArgs}
              @point-select=${(r) => _e(this, ie, qa).call(this, r, e)}
            ></esatto-seo-report-chart-card>` : c}
        ${s?.peak ? l`<p class="peak muted">${s.peak}</p>` : c} ${_e(this, ie, ja).call(this)}
      </section>
    `;
  }
};
z = /* @__PURE__ */ new WeakMap();
Qe = /* @__PURE__ */ new WeakMap();
ie = /* @__PURE__ */ new WeakSet();
Wa = function(e) {
  return A(this, Qe)?.daily !== e && zu(this, Qe, { daily: e, view: Nu(this, e) }), A(this, Qe).view;
};
Ba = function() {
  const { months: e, rollingDays: t, loading: i } = A(this, z).state;
  if (e.length === 0) return c;
  const s = Du(this, e, t), r = A(this, z).selected;
  return l`
      <div class="field">
        <label for="period">${o(this, "uiDailyPeriodLabel")}</label>
        <select id="period" aria-busy=${i ? "true" : "false"} @change=${(a) => {
    A(this, z).selectMonth(a.target.value);
  }}>
          ${s.map((a) => l`<option value=${a.value} .selected=${W(a.value === r)}>${a.label}</option>`)}
        </select>
      </div>
    `;
};
Ha = function(e) {
  if (e.days.length === 0) return c;
  const { day: t } = A(this, z).state;
  return l`
      <div class="field">
        <label for="day">${o(this, "uiDailyDayPickerLabel")}</label>
        <select id="day" @change=${(i) => {
    A(this, z).selectDay(i.target.value || null);
  }}>
          <option value="" .selected=${W(!t)}>${o(this, "uiDailyPickDayLabel")}</option>
          ${e.days.map((i) => l`<option value=${i.date} .selected=${W(i.date === t)}>${i.label}</option>`)}
        </select>
      </div>
    `;
};
Va = function(e) {
  return e.hasDays ? l`
      <div class="figures">
        <span class="total">${e.total}</span>
        <span class="muted">${o(this, "uiDailyVisitsLabel")}</span>
        ${e.change ? qt({ pill: e.change, note: e.change.against }) : e.noComparison ? l`<span class="muted">${o(this, "uiDailyNoComparisonText")}</span>` : c}
      </div>
    ` : c;
};
qa = function(e, t) {
  e.stopPropagation();
  const i = t.chart.labels[e.detail.index];
  i && A(this, z).selectDay(i);
};
ja = function() {
  const { day: e, sources: t, sourcesLoading: i, sourcesProblem: s } = A(this, z).state;
  if (!e) return c;
  const r = zt(e, L(this));
  return l`
      <div class="sources" aria-live="polite">
        ${i ? l`<p class="muted">${o(this, "uiDailySourcesLoadingText", { day: r })}</p>` : c}
        ${s ? l`<esatto-seo-report-problem .error=${s} @retry=${() => {
    A(this, z).retry();
  }}></esatto-seo-report-problem>` : c}
        ${t && t.sources.length === 0 ? l`<p class="muted">${o(this, "uiDailySourcesNoneText", { day: r })}</p>` : c}
        ${t && t.sources.length > 0 ? l`
              <h4>${o(this, "uiDailySourcesTitle", { day: r })}</h4>
              <esatto-seo-report-data-table
                .caption=${o(this, "uiDailySourcesTitle", { day: r })}
                .columns=${kc(this)}
                .rows=${Cc(this, t.sources)}
              ></esatto-seo-report-data-table>
            ` : c}
      </div>
    `;
};
Ht.styles = [
  C,
  jt,
  w`
      header { display: grid; gap: var(--uui-size-space-3); margin-bottom: var(--uui-size-space-4); }
      .controls { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); }
      .field { min-width: 14rem; }
      .figures { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--uui-size-space-2); }
      .figures .trend { margin-top: 0; }
      .total { font-size: 2rem; font-weight: 800; line-height: 1; letter-spacing: -0.02em; font-variant-numeric: tabular-nums; }
      .peak { margin-top: var(--uui-size-space-4); font-size: 0.85rem; }
      .sources { margin-top: var(--uui-size-space-5); display: grid; gap: var(--uui-size-space-3); }
      .sources h4 { font-size: 1rem; }
    `
];
Ga([
  B()
], Ht.prototype, "_version", 2);
Ht = Ga([
  T("esatto-seo-report-daily-card")
], Ht);
var Au = Object.defineProperty, Mu = Object.getOwnPropertyDescriptor, ks = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Mu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Au(t, i, r), r;
};
let ut = class extends k {
  constructor() {
    super(...arguments), this.report = null, this.period = "";
  }
  render() {
    return this.report ? l`
      <div class="page">
        <esatto-seo-report-daily-card></esatto-seo-report-daily-card>
        ${Sc(this.report).blocks.map((e) => l`<esatto-seo-report-chart-card .block=${e}></esatto-seo-report-chart-card>`)}
      </div>
    ` : c;
  }
};
ut.styles = [C];
ks([
  h({ attribute: !1 })
], ut.prototype, "report", 2);
ks([
  h()
], ut.prototype, "period", 2);
ut = ks([
  T("esatto-seo-report-traffic")
], ut);
var Fu = Object.defineProperty, Iu = Object.getOwnPropertyDescriptor, Ya = (e) => {
  throw TypeError(e);
}, Cs = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? Iu(t, i) : t, a = e.length - 1, n; a >= 0; a--)
    (n = e[a]) && (r = (s ? n(t, i, r) : n(r)) || r);
  return s && r && Fu(t, i, r), r;
}, Xa = (e, t, i) => t.has(e) || Ya("Cannot " + i), b = (e, t, i) => (Xa(e, t, "read from private field"), i ? i.call(e) : t.get(e)), kt = (e, t, i) => t.has(e) ? Ya("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), S = (e, t, i) => (Xa(e, t, "access private method"), i), v, x, Za, Ja, Qa, Fi, Ss, en, tn, Ii, sn, rn, Es, Ls, Ps, Ds, Ns, an, nn;
let Me = class extends k {
  constructor() {
    super(), kt(this, x), this._choice = { tab: "analysis", pinned: !1 }, kt(this, v, new jn(
      { getStatus: bn, getReport: yn, refreshReport: Tn, startFetch: ar, getFetchProgress: _n },
      () => this.requestUpdate()
    )), kt(this, Ds, () => {
      const e = Hn(this._choice);
      e !== this._choice && (this._choice = e);
    }), kt(this, Ns, () => {
      b(this, v).reloadStatus();
    }), this.addEventListener(Sr, (e) => S(this, x, Ps).call(this, e.detail.tab)), this.addEventListener(Cr, (e) => {
      e.stopPropagation();
      const { period: t, note: i } = e.detail;
      b(this, v).setNote(t, i);
    }), this.addEventListener(Or, (e) => {
      e.stopPropagation(), b(this, v).followRun(e.detail.progress);
    }), this.consumeContext(cn, (e) => {
      this.observe(e?.allowedSections, (t) => this._settingsAccess = dn(t), "allowedSections");
    });
  }
  connectedCallback() {
    super.connectedCallback(), b(this, v).connect();
  }
  disconnectedCallback() {
    b(this, v).disconnect(), super.disconnectedCallback();
  }
  // ---- Showing ------------------------------------------------------------------------------------------------------------------
  render() {
    const { status: e, loading: t } = b(this, v).state;
    return l`
      <header class="header">
        <span class="logo" aria-hidden="true"><uui-icon name="icon-chart-curve"></uui-icon></span>
        <div>
          <h2>${o(this, "dashboardTitle")}</h2>
          <p class="muted">${e?.displayName || o(this, "uiSubtitleText")}</p>
        </div>
      </header>
      <div class="page">${t ? S(this, x, Za).call(this) : S(this, x, Ja).call(this, e)}</div>
    `;
  }
};
v = /* @__PURE__ */ new WeakMap();
x = /* @__PURE__ */ new WeakSet();
Za = function() {
  return l`<div class="loading" role="status"><uui-loader></uui-loader><span class="visually-hidden">${o(this, "uiLoadingLabel")}</span></div>`;
};
Ja = function(e) {
  if (!e) return S(this, x, Ii).call(this);
  const { report: t, readFailed: i } = b(this, v).state, s = Kn(e, t, i);
  return l`${S(this, x, Ii).call(this)} ${S(this, x, sn).call(this)} ${S(this, x, Qa).call(this, s, e)} ${S(this, x, an).call(this, s)}`;
};
Qa = function(e, t) {
  const { report: i, starting: s } = b(this, v).state;
  switch (e) {
    case "notConnected":
      return l`<esatto-seo-report-not-connected .status=${t} .canOpenSettings=${this._settingsAccess}></esatto-seo-report-not-connected>`;
    case "progress":
      return l`
          <esatto-seo-report-fetch-card .fetch=${t.fetch} ?busy=${s} @fetch-now=${() => {
        b(this, v).fetchNow();
      }}></esatto-seo-report-fetch-card>
          ${i ? S(this, x, Fi).call(this, t, i) : c}
        `;
    case "error":
      return S(this, x, Ss).call(this, t);
    default:
      return S(this, x, Fi).call(this, t, i);
  }
};
Fi = function(e, t) {
  const { period: i, pendingPeriod: s, fetchedUtc: r, refreshing: a, switching: n } = b(this, v).state, u = t !== null && !e.upToDate && !e.fetch.running;
  return l`
      ${Vi(t) ? l`<esatto-seo-report-overview
            .report=${t}
            .period=${s ?? i}
            .periods=${qi(e.periods)}
            .fetchedUtc=${r}
            ?refreshing=${a}
            ?busy=${n}
            @period-change=${(d) => S(this, x, Es).call(this, d)}
            @refresh=${(d) => S(this, x, Ls).call(this, d)}
          ></esatto-seo-report-overview>` : S(this, x, en).call(this, t, e)}
      ${u ? S(this, x, tn).call(this, e) : c}
    `;
};
Ss = function(e) {
  const { period: t, pendingPeriod: i, fetchedUtc: s, refreshing: r, switching: a } = b(this, v).state, n = i ?? ur(t, e.periods);
  return n ? l`<esatto-seo-report-period-bar
          .periods=${qi(e.periods)}
          .period=${n}
          .fetchedUtc=${s}
          ?refreshing=${r}
          ?busy=${a}
          @period-change=${(u) => S(this, x, Es).call(this, u)}
          @refresh=${(u) => S(this, x, Ls).call(this, u)}
        ></esatto-seo-report-period-bar>` : c;
};
en = function(e, t) {
  const { period: i } = b(this, v).state, s = q(this, "dataStatusNote", e?.data_status_note);
  return l`
      <section class="card" aria-labelledby="empty-title">
        <div class="card-head">
          <span class="icon-badge solid round tone-none" aria-hidden="true"><uui-icon name="icon-chart"></uui-icon></span>
          <h3 id="empty-title">${o(this, "uiEmptyMonthTitle")}</h3>
        </div>
        <div class="stack">
          <p>${e ? l`${s} ${o(this, "uiEmptyMonthText")}` : o(this, "uiNoReportText", { period: i || null })}</p>
          ${i ? S(this, x, Ss).call(this, t) : c}
        </div>
      </section>
    `;
};
tn = function(e) {
  const t = lr(e.fetch), i = t ? le(this, t) : null;
  return l`
      <div class="banner tone-warning" role="status">
        <div>
          <p>${o(this, "uiArchiveBehindText", { period: e.expectedLatestPeriod })}</p>
          ${i ? l`<p class="muted small"><strong>${i.title}</strong> ${i.text}</p>` : c}
          <div class="actions">
            <uui-button look="primary" label=${o(this, "uiFetchNowLabel")} ?disabled=${b(this, v).state.starting} @click=${() => {
    b(this, v).fetchNow();
  }}>
              ${o(this, "uiFetchNowLabel")}
            </uui-button>
          </div>
        </div>
      </div>
    `;
};
Ii = function() {
  const { problem: e } = b(this, v).state;
  return e ? l`<esatto-seo-report-problem .error=${e} @retry=${() => b(this, v).retry()}></esatto-seo-report-problem>` : c;
};
sn = function() {
  const { lost: e, outcome: t } = b(this, v).state;
  return l`
      ${e ? l`<div class="banner tone-warning" role="alert">
            <div>
              <h3>${o(this, "uiFetchLostTitle")}</h3>
              <p>${o(this, "uiFetchLostText")}</p>
              <div class="actions">
                <uui-button look="outline" label=${o(this, "uiTryAgainLabel")} @click=${() => b(this, v).followAgain()}>${o(this, "uiTryAgainLabel")}</uui-button>
              </div>
            </div>
          </div>` : c}
      ${t ? S(this, x, rn).call(this, t) : c}
    `;
};
rn = function(e) {
  const t = e.kind === "failed", i = t ? le(this, e.errorCode) : null, s = t ? "critical" : e.kind === "fetched" ? "good" : "notice";
  return l`
      <div class="banner tone-${s}" role=${t ? "alert" : "status"}>
        <div>
          ${i ? l`<h3>${o(this, "uiFetchFailedTitle")}</h3><p><strong>${i.title}</strong> ${i.text}</p>` : l`<p>${e.kind === "fetched" ? o(this, "uiFetchedMonthsText", { done: e.done }) : o(this, "uiNothingToFetchText")}</p>`}
          <div class="actions">
            <uui-button look="outline" label=${o(this, "uiDismissLabel")} @click=${() => b(this, v).dismissOutcome()}>${o(this, "uiDismissLabel")}</uui-button>
          </div>
        </div>
      </div>
    `;
};
Es = function(e) {
  e.stopPropagation(), b(this, v).selectPeriod(e.detail);
};
Ls = function(e) {
  e.stopPropagation(), b(this, v).refresh();
};
Ps = function(e) {
  this._choice = Bn(this._choice, e);
};
Ds = /* @__PURE__ */ new WeakMap();
Ns = /* @__PURE__ */ new WeakMap();
an = function(e) {
  const t = Gn(b(this, v).state.report, this._settingsAccess === !0);
  if (t.length === 0) return c;
  const i = Wn(this._choice.tab, t, e, this._choice.pinned);
  return l`
      <uui-tab-group aria-label=${o(this, "uiTabsLabel")}>
        ${t.map(
    (s) => l`<uui-tab
            id="tab-${s}"
            aria-controls="tabpanel"
            label=${o(this, Ft[s])}
            ?active=${i === s}
            @click=${() => S(this, x, Ps).call(this, s)}
            >${o(this, Ft[s])}</uui-tab
          >`
  )}
      </uui-tab-group>
      ${i ? l`<div class="panel" id="tabpanel" role="tabpanel" aria-labelledby="tab-${i}">${S(this, x, nn).call(this, i)}</div>` : c}
    `;
};
nn = function(e) {
  const { status: t, report: i, note: s, period: r, archiveRevision: a } = b(this, v).state;
  switch (e) {
    case "analysis":
      return l`<esatto-seo-report-analysis .period=${r} .note=${s} .revision=${a}></esatto-seo-report-analysis>`;
    case "traffic":
      return l`<esatto-seo-report-traffic .report=${i} .period=${r}></esatto-seo-report-traffic>`;
    case "queries":
      return l`<esatto-seo-report-queries .report=${i} .period=${r}></esatto-seo-report-queries>`;
    case "topPages":
      return l`<esatto-seo-report-top-pages .report=${i} .period=${r}></esatto-seo-report-top-pages>`;
    case "toFix":
      return l`<esatto-seo-report-to-fix .report=${i} .period=${r}></esatto-seo-report-to-fix>`;
    case "comparison":
      return l`<esatto-seo-report-comparison .report=${i} .period=${r}></esatto-seo-report-comparison>`;
    case "settings":
      return l`<esatto-seo-report-settings .status=${t} .onSaved=${b(this, Ns)} .onEngaged=${b(this, Ds)}></esatto-seo-report-settings>`;
  }
};
Me.styles = [
  C,
  w`
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
    `
];
Cs([
  B()
], Me.prototype, "_choice", 2);
Cs([
  B()
], Me.prototype, "_settingsAccess", 2);
Me = Cs([
  T("esatto-seo-report-dashboard")
], Me);
const Bu = Me;
export {
  Me as EsattoSeoReportDashboardElement,
  Bu as default
};
//# sourceMappingURL=seo-report-dashboard.js.map
