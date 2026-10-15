import { css as de, state as z, customElement as he, nothing as _, html as n, svg as M } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as pe } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as wt } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as zt } from "@umbraco-cms/backoffice/http-client";
const Tt = "/umbraco/management/api/v1/esatto/ai-costs", Ct = () => [{ scheme: "bearer", type: "http" }], me = (t) => zt.get({ url: `${Tt}/report?days=${t}`, security: Ct() }), ve = (t, e) => zt.get({ url: `${Tt}/breakdown?dimension=${t}&days=${e}`, security: Ct() });
function be(t) {
  if (!t.rates) return t;
  const e = Object.fromEntries(
    Object.entries(t.rates.perBaseUnit).map(([s, i]) => [s.toUpperCase(), i])
  );
  return { ...t, rates: { ...t.rates, perBaseUnit: e } };
}
function fe(t, e) {
  return e && (e === t.baseCurrency || t.rates?.perBaseUnit[e]) ? e : t.baseCurrency;
}
function Mt(t, e, s) {
  return s === t.baseCurrency ? e : e * (t.rates?.perBaseUnit[s] ?? 1);
}
function ht(t, e, { compact: s = !1 } = {}) {
  const i = Math.abs(t) > 0 && Math.abs(t) < 1;
  return new Intl.NumberFormat(void 0, {
    style: "currency",
    currency: e,
    notation: s ? "compact" : "standard",
    minimumFractionDigits: s ? 0 : 2,
    maximumFractionDigits: s ? 1 : i ? 4 : 2
  }).format(t);
}
function f(t, { compact: e = !1 } = {}) {
  return new Intl.NumberFormat(void 0, { notation: e ? "compact" : "standard", maximumFractionDigits: 1 }).format(t);
}
function It(t, e) {
  const s = t.month.budget;
  if (!s) return { kind: "none" };
  const i = s.currency === t.baseCurrency ? 1 : t.rates?.perBaseUnit[s.currency];
  if (!i)
    return { kind: "unavailable", reason: `Exchange rates are unavailable, so the ${s.currency} budget can’t be compared right now.` };
  const a = s.amount / i, r = a > 0 ? t.month.cost / a : 0, d = t.month.cost > a ? "over" : r >= 0.8 || t.month.projectedCost > a ? "warning" : "ok";
  return { kind: "set", amount: Mt(t, a, e), spentShare: r, level: d };
}
function $e(t, e, s) {
  let i = 0;
  const a = t.days.map((c, u) => ({ day: u + 1, value: i += c.cost })), r = a[a.length - 1] ?? { day: 0, value: 0 }, d = [r, { day: e, value: t.projectedCost }];
  return { actual: a, projection: d, max: Math.max(t.projectedCost, r.value, 0) };
}
function ye(t, e, s = 7) {
  const i = (u) => e.map((v) => t.days.reduce((b, T, x) => T.date >= v.start && T.date <= v.end ? b + u[x] : b, 0)), a = t.modelDays.map((u) => ({ id: u.modelId, values: i(u.costs), total: u.costs.reduce((v, b) => v + b, 0) })).sort((u, v) => v.total - u.total);
  if (a.length <= s) return { ids: a.map((u) => u.id), values: a.map((u) => u.values) };
  const r = a.slice(0, s - 1), d = a.slice(s - 1), c = e.map((u, v) => d.reduce((b, T) => b + T.values[v], 0));
  return { ids: [...r.map((u) => u.id), "Other"], values: [...r.map((u) => u.values), c] };
}
function ge(t) {
  const e = new Array(t[0]?.length ?? 0).fill(0);
  return t.map(
    (s) => s.map((i, a) => {
      const r = e[a];
      return e[a] += i, { y0: r, y1: e[a] };
    })
  );
}
function ke(t, e) {
  const s = t + e;
  return s === 0 ? 0 : e / s;
}
function _e(t) {
  return t < 1e3 ? `${Math.round(t)} ms` : `${new Intl.NumberFormat(void 0, { maximumFractionDigits: 1 }).format(t / 1e3)} s`;
}
function Q(t, e) {
  return t === "Other" ? "var(--viz-other)" : `var(--viz-cat-${e + 1})`;
}
function xe(t) {
  return [
    { id: "uncached", label: "Uncached input", color: Q("uncached", 0), values: t.map((e) => Math.max(0, e.inputTokens - e.cachedInputTokens)) },
    { id: "cached", label: "Cached input", color: Q("cached", 1), values: t.map((e) => e.cachedInputTokens) },
    { id: "output", label: "Output", color: Q("output", 2), values: t.map((e) => e.outputTokens) }
  ];
}
function I(t) {
  const [e, s, i] = t.split("-").map(Number);
  return new Date(Date.UTC(e, s - 1, i));
}
const qt = (t) => ({
  start: t.date,
  end: t.date,
  requests: t.requests,
  inputTokens: t.inputTokens,
  outputTokens: t.outputTokens,
  cachedInputTokens: t.cachedInputTokens,
  totalTokens: t.totalTokens,
  cost: t.cost,
  unpricedTokens: t.unpricedTokens,
  succeededRequests: t.succeededRequests,
  failedRequests: t.failedRequests
}), we = (t) => t.map(qt);
function ze(t) {
  const e = [];
  for (const s of t) {
    const i = e[e.length - 1];
    if (!i || I(s.date).getUTCDay() === 1) {
      e.push(qt(s));
      continue;
    }
    i.end = s.date, i.requests += s.requests, i.inputTokens += s.inputTokens, i.outputTokens += s.outputTokens, i.cachedInputTokens += s.cachedInputTokens, i.totalTokens += s.totalTokens, i.cost += s.cost, i.unpricedTokens += s.unpricedTokens, i.succeededRequests += s.succeededRequests, i.failedRequests += s.failedRequests;
  }
  return e;
}
function Te(t, e) {
  return e == null ? { kind: "none" } : e === 0 ? t === 0 ? { kind: "change", percent: 0 } : { kind: "new" } : { kind: "change", percent: Math.round((t - e) / e * 100) };
}
function Ce(t) {
  return new Intl.NumberFormat(t).formatToParts(1.5).find((s) => s.type === "decimal")?.value === "," ? { field: ";", decimal: "," } : { field: ",", decimal: "." };
}
const Me = /^[=+\-@\t\r]/;
function Dt(t, e, s) {
  const { field: i, decimal: a } = Ce(s), r = (d) => {
    const c = typeof d == "number" ? String(d).replace(".", a) : Me.test(d) ? `'${d}` : d;
    return /["\r\n]/.test(c) || c.includes(i) ? `"${c.replace(/"/g, '""')}"` : c;
  };
  return "\uFEFF" + [t, ...e].map((d) => d.map(r).join(i)).join(`\r
`);
}
function Rt(t, e) {
  const s = URL.createObjectURL(new Blob([e], { type: "text/csv;charset=utf-8" }));
  Object.assign(document.createElement("a"), { href: s, download: t }).click(), URL.revokeObjectURL(s);
}
class kt {
  #t = 0;
  next() {
    return ++this.#t;
  }
  isCurrent(e) {
    return e === this.#t;
  }
}
const St = "esatto.aiCosts.prefs", Ft = () => {
  try {
    return globalThis.localStorage;
  } catch {
    return;
  }
};
function Ie(t = Ft()) {
  try {
    const e = JSON.parse(t?.getItem(St) ?? "{}");
    return e && typeof e == "object" && !Array.isArray(e) ? e : {};
  } catch {
    return {};
  }
}
function qe(t, e = Ft()) {
  try {
    e?.setItem(St, JSON.stringify(t));
  } catch {
  }
}
function De(t) {
  if (!t.length) return [];
  const e = [...t].sort((s, i) => s - i);
  return [0.25, 0.5, 0.75].map((s) => e[Math.min(e.length - 1, Math.floor(s * e.length))]);
}
function Re(t, e) {
  for (let s = 0; s < e.length; s++) if (t <= e[s]) return s;
  return 3;
}
function Et(t) {
  if (t <= 0) return [0, 1];
  const e = t / 4, s = 10 ** Math.floor(Math.log10(e)), i = [1, 2, 2.5, 5, 10].map((r) => r * s).find((r) => r >= e) ?? 10 * s, a = [];
  for (let r = 0; r <= t + i * 0.999; r += i) a.push(Number(r.toFixed(10)));
  return a;
}
function Se(t, e, s, i) {
  const a = Math.min(4, s / 2, i), r = e - i;
  return `M${t},${e} V${r + a} Q${t},${r} ${t + a},${r} H${t + s - a} Q${t + s},${r} ${t + s},${r + a} V${e} Z`;
}
const S = 12, Fe = de`
    :host {
        display: block;
        padding: var(--uui-size-layout-1);
        --viz-column: #2a78d6;
        --viz-heat-0: #86b6ef;
        --viz-heat-1: #3987e5;
        --viz-heat-2: #1c5cab;
        --viz-heat-3: #0d366b;
        --viz-empty: var(--uui-color-surface-alt);
        --viz-grid: var(--uui-color-divider);
        --viz-axis: var(--uui-color-border-emphasis);
        --viz-muted: var(--uui-color-text-alt);
        /* Categorical slots in fixed order (dataviz reference palette, validated light on #fff and dark on
           Umbraco's #2d333b), and the reserved status colour for failures. */
        --viz-cat-1: #2a78d6;
        --viz-cat-2: #eb6834;
        --viz-cat-3: #1baf7a;
        --viz-cat-4: #eda100;
        --viz-cat-5: #e87ba4;
        --viz-cat-6: #008300;
        --viz-cat-7: #6250d6;
        --viz-cat-8: #e34948;
        --viz-critical: #d03b3b;
        /* "Other" (folded models): neutral, so it never reads as a series hue or a status. */
        --viz-other: #a3a29e;
    }
    :host([dark]) {
        --viz-column: #3987e5;
        --viz-heat-0: #256abf;
        --viz-heat-1: #3987e5;
        --viz-heat-2: #86b6ef;
        --viz-heat-3: #cde2fb;
        --viz-cat-1: #3987e5;
        --viz-cat-2: #d95926;
        --viz-cat-3: #199e70;
        --viz-cat-4: #c98500;
        --viz-cat-5: #d55181;
        --viz-cat-6: #008300;
        --viz-cat-7: #9085e9;
        --viz-cat-8: #e66767;
        --viz-other: #6b6a66;
    }
    .theme-probe {
        position: absolute;
        width: 0;
        height: 0;
        overflow: hidden;
        color: var(--uui-color-surface);
    }
    .filters {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-5);
        margin-bottom: var(--uui-size-space-5);
    }
    .currency {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
    }
    .currency select {
        font: inherit;
        color: var(--uui-color-text);
        background: var(--uui-color-surface);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
    }
    .content {
        display: grid;
        gap: var(--uui-size-space-5);
        transition: opacity 120ms;
    }
    .content.is-loading {
        opacity: 0.5;
    }
    .tiles {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: var(--uui-size-space-5);
    }
    .tile-label {
        color: var(--uui-color-text-alt);
    }
    .tile-value {
        font-size: 1.75rem;
        font-weight: 600;
        line-height: 1.2;
        margin: var(--uui-size-space-2) 0;
        color: var(--uui-color-text);
    }
    .tile-value.hero {
        font-size: 3rem;
    }
    .tile-note {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
    }
    .card {
        position: relative;
    }
    .heatmap-scroll,
    .table-scroll {
        overflow-x: auto;
    }
    svg {
        display: block;
        font-family: inherit;
    }
    .axis-label {
        fill: var(--viz-muted);
        font-size: 11px;
    }
    .tick {
        text-anchor: end;
        font-variant-numeric: tabular-nums;
    }
    .cell {
        fill: var(--viz-empty);
        cursor: default;
    }
    .cell:hover {
        stroke: var(--uui-color-text);
        stroke-width: 1.5;
    }
    .cell-0 { fill: var(--viz-heat-0); background: var(--viz-heat-0); }
    .cell-1 { fill: var(--viz-heat-1); background: var(--viz-heat-1); }
    .cell-2 { fill: var(--viz-heat-2); background: var(--viz-heat-2); }
    .cell-3 { fill: var(--viz-heat-3); background: var(--viz-heat-3); }
    .cell-empty { fill: var(--viz-empty); background: var(--viz-empty); }
    .scale-legend {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2);
        margin-top: var(--uui-size-space-4);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
    }
    .swatch {
        display: inline-block;
        width: ${S}px;
        height: ${S}px;
        border-radius: 2px;
    }
    .scale-gap {
        width: var(--uui-size-space-4);
    }
    .scale-range {
        margin-left: auto;
    }
    .column-chart {
        width: 100%;
        overflow: hidden;
    }
    .grid {
        stroke: var(--viz-grid);
        stroke-width: 1;
    }
    .baseline {
        stroke: var(--viz-axis);
        stroke-width: 1;
    }
    .bar {
        fill: var(--viz-column);
        pointer-events: none;
    }
    .hit {
        fill: transparent;
    }
    .hit:hover + .bar {
        opacity: 0.8;
    }
    .tooltip {
        position: absolute;
        transform: translate(-50%, calc(-100% - 8px));
        background: var(--uui-color-surface);
        color: var(--uui-color-text);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        box-shadow: var(--uui-shadow-depth-3);
        padding: var(--uui-size-space-3) var(--uui-size-space-4);
        pointer-events: none;
        white-space: nowrap;
        z-index: 2;
        font-size: var(--uui-type-small-size);
    }
    .tooltip-title {
        color: var(--uui-color-text-alt);
        margin-bottom: var(--uui-size-space-1);
    }
    .tooltip-line {
        display: flex;
        gap: var(--uui-size-space-2);
        align-items: baseline;
    }
    .tooltip-line strong {
        font-variant-numeric: tabular-nums;
    }
    .tooltip-line span {
        color: var(--uui-color-text-alt);
    }
    .num {
        text-align: right;
        font-variant-numeric: tabular-nums;
    }
    .share {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-3);
    }
    .share-track {
        display: inline-block;
        width: 60px;
        height: 6px;
        border-radius: 3px;
        background: var(--uui-color-surface-alt);
        overflow: hidden;
    }
    .share-fill {
        display: block;
        height: 100%;
        background: var(--viz-column);
    }
    .empty {
        color: var(--uui-color-text-alt);
        margin: var(--uui-size-space-5) 0;
    }
    .error {
        color: var(--uui-color-danger);
    }
    .footnote {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        margin: 0;
    }

    /* This month: budget bar; comparison with the previous period. */
    .tile-change {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        margin-top: var(--uui-size-space-1);
    }
    .budget {
        height: 6px;
        border-radius: 3px;
        background: var(--uui-color-surface-alt);
        overflow: hidden;
        margin: var(--uui-size-space-3) 0 var(--uui-size-space-2);
    }
    .budget-fill {
        display: block;
        height: 100%;
        background: var(--viz-column);
    }
    .budget-warning .budget-fill {
        background: var(--uui-color-warning-standalone);
    }
    .budget-over .budget-fill {
        background: var(--uui-color-danger-standalone);
    }
    /* Charts: legend, lines, figures. */
    .legend {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2) var(--uui-size-space-5);
        margin-bottom: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
    }
    .legend-item {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
    }
    .legend-swatch {
        width: 10px;
        height: 10px;
        border-radius: 2px;
    }
    .legend-line {
        width: 18px;
        border-top: 2px solid var(--viz-cat-1);
    }
    .legend-line.dashed {
        border-top-style: dashed;
    }
    .legend-line.budget {
        border-top: 1px solid var(--uui-color-text);
    }
    .line {
        fill: none;
        stroke: var(--viz-cat-1);
        stroke-width: 2;
        stroke-linejoin: round;
        stroke-linecap: round;
        pointer-events: none;
    }
    .line.projection {
        stroke-dasharray: 5 4;
    }
    .dot {
        fill: var(--viz-cat-1);
        stroke: var(--uui-color-surface);
        stroke-width: 2;
        pointer-events: none;
    }
    .budget-line {
        stroke: var(--uui-color-text);
        stroke-width: 1;
    }
    .figures {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-6);
        margin-bottom: var(--uui-size-space-4);
    }
    .figure {
        font-size: 1.5rem;
        font-weight: 600;
        color: var(--uui-color-text);
    }
    .figure-note {
        font-size: var(--uui-type-small-size);
        font-weight: 400;
        color: var(--uui-color-text-alt);
    }
    .chart-note {
        margin: var(--uui-size-space-3) 0 0;
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
    }
    .header-actions {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
    }
`;
var Ee = Object.defineProperty, Ue = Object.getOwnPropertyDescriptor, Ut = (t) => {
  throw TypeError(t);
}, w = (t, e, s, i) => {
  for (var a = i > 1 ? void 0 : i ? Ue(e, s) : e, r = t.length - 1, d; r >= 0; r--)
    (d = t[r]) && (a = (i ? d(e, s, a) : d(a)) || a);
  return i && a && Ee(e, s, a), a;
}, pt = (t, e, s) => e.has(t) || Ut("Cannot " + s), m = (t, e, s) => (pt(t, e, "read from private field"), s ? s.call(t) : e.get(t)), U = (t, e, s) => e.has(t) ? Ut("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, s), Ne = (t, e, s, i) => (pt(t, e, "write to private field"), e.set(t, s), s), l = (t, e, s) => (pt(t, e, "access private method"), s), V, Y, Z, K, o, mt, vt, et, bt, Nt, Ot, At, Wt, D, W, q, Bt, Pt, Lt, st, G, j, jt, H, Vt, Ht, Gt, Xt, at, L, it, ot, Jt, X, Qt, lt, rt, nt, Yt, Zt, Kt, te, ee, se, ae, ft, ie, oe, le;
const re = [30, 90, 180, 365], ne = [
  { key: "cost", label: "Cost" },
  { key: "tokens", label: "Tokens" },
  { key: "requests", label: "Requests" }
], _t = [
  { key: "model", label: "Model" },
  { key: "profile", label: "Profile" },
  { key: "user", label: "User" },
  { key: "provider", label: "Provider" }
], N = 2, ut = 30, dt = 18, tt = 10, R = 220, A = 200, xt = 56, y = 64, ce = 26, Oe = 24;
let k = class extends pe {
  constructor() {
    super(), U(this, o), this._loading = !1, this._error = null, this._tables = /* @__PURE__ */ new Set(), this._costView = "total", this._tooltip = null, this._dark = !1, this._chartWidth = 900, this._dimension = "model", this._breakdowns = /* @__PURE__ */ new Map(), this._breakdownLoading = !1, this._breakdownError = null, U(this, V, new ResizeObserver((e) => {
      const s = Math.floor(e[0]?.contentRect.width ?? 0);
      s > 0 && s !== this._chartWidth && (this._chartWidth = s);
    })), U(this, Y), U(this, Z, new kt()), U(this, K, new kt()), U(this, G, () => {
      this._tooltip = null;
    }), U(this, L, (e) => ht(e, m(this, o, D))), U(this, it, (e) => new Intl.NumberFormat(void 0, { style: "currency", currency: m(this, o, D), notation: "compact", maximumFractionDigits: e < 10 ? 2 : 0 }).format(e)), U(this, ot, (e) => f(e, { compact: !0 }));
    const t = Ie();
    this._days = re.includes(t.days ?? 0) ? t.days : 365, this._currency = t.currency ?? null, this._metric = ne.some((e) => e.key === t.metric) ? t.metric : "cost";
  }
  connectedCallback() {
    super.connectedCallback(), l(this, o, mt).call(this);
  }
  disconnectedCallback() {
    m(this, V).disconnect(), super.disconnectedCallback();
  }
  firstUpdated() {
    l(this, o, bt).call(this);
  }
  willUpdate(t) {
    t.has("_dark") && this.toggleAttribute("dark", this._dark);
  }
  updated() {
    const t = this.shadowRoot?.querySelector(".column-chart");
    t && t !== m(this, Y) && (m(this, V).disconnect(), m(this, V).observe(t), Ne(this, Y, t));
  }
  // ---- render -------------------------------------------------------------------------
  render() {
    return n`
      <span class="theme-probe" aria-hidden="true"></span>
      ${l(this, o, jt).call(this)}
      ${this._error ? n`<uui-box><p class="error">${this._error}</p></uui-box>` : _}
      ${this._report ? n`<div class="content ${this._loading ? "is-loading" : ""}">
            ${l(this, o, Ht).call(this)} ${l(this, o, Yt).call(this)} ${l(this, o, Xt).call(this)} ${l(this, o, Kt).call(this)}
            ${l(this, o, te).call(this)} ${l(this, o, ee).call(this)} ${l(this, o, ae).call(this)}
            ${l(this, o, le).call(this)}
          </div>` : this._error ? _ : n`<uui-loader-bar></uui-loader-bar>`}
    `;
  }
};
V = /* @__PURE__ */ new WeakMap();
Y = /* @__PURE__ */ new WeakMap();
Z = /* @__PURE__ */ new WeakMap();
K = /* @__PURE__ */ new WeakMap();
o = /* @__PURE__ */ new WeakSet();
mt = async function() {
  const t = m(this, Z).next();
  this._loading = !0, l(this, o, bt).call(this);
  const { data: e, error: s } = await wt(this, me(this._days), { disableNotifications: !0 });
  if (m(this, Z).isCurrent(t)) {
    if (this._loading = !1, s || !e) {
      this._error = "Could not load AI cost data.";
      return;
    }
    this._error = null, this._report = be(e), (!this._currency || !this._report.currencies.includes(this._currency)) && (this._currency = this._report.defaultCurrency), this._dimension !== "model" && l(this, o, vt).call(this, this._dimension);
  }
};
vt = async function(t) {
  const e = `${t}:${this._days}`;
  if (this._breakdowns.has(e)) return;
  const s = m(this, K).next();
  this._breakdownLoading = !0, this._breakdownError = null;
  const { data: i, error: a } = await wt(this, ve(t, this._days), { disableNotifications: !0 });
  if (m(this, K).isCurrent(s)) {
    if (this._breakdownLoading = !1, a || !i) {
      this._breakdownError = "Could not load the breakdown.";
      return;
    }
    this._breakdowns = new Map(this._breakdowns).set(e, i);
  }
};
et = function() {
  qe({ days: this._days, currency: this._currency ?? void 0, metric: this._metric });
};
bt = function() {
  const t = this.shadowRoot?.querySelector(".theme-probe");
  if (!t) return;
  const e = getComputedStyle(t).color.match(/\d+(\.\d+)?/g);
  if (!e) return;
  const [s, i, a] = e.map(Number);
  this._dark = (0.2126 * s + 0.7152 * i + 0.0722 * a) / 255 < 0.45;
};
Nt = function(t) {
  t !== this._days && (this._days = t, this._breakdowns = /* @__PURE__ */ new Map(), l(this, o, et).call(this), l(this, o, mt).call(this));
};
Ot = function(t) {
  this._currency = t.target.value, l(this, o, et).call(this);
};
At = function(t) {
  this._metric = t, l(this, o, et).call(this);
};
Wt = function(t) {
  this._dimension = t, t !== "model" && l(this, o, vt).call(this, t);
};
D = function() {
  return this._report ? fe(this._report, this._currency) : "USD";
};
W = function(t) {
  return this._report ? Mt(this._report, t, m(this, o, D)) : t;
};
q = function(t, e = {}) {
  return ht(l(this, o, W).call(this, t), m(this, o, D), e);
};
Bt = function(t) {
  return this._metric === "tokens" ? t.totalTokens : this._metric === "requests" ? t.requests : t.cost;
};
Pt = function(t) {
  return this._metric === "cost" ? l(this, o, q).call(this, t) : this._metric === "tokens" ? `${f(t)} tokens` : `${f(t)} ${t === 1 ? "request" : "requests"}`;
};
Lt = function() {
  return this._days === 365 ? "12 months" : `${this._days} days`;
};
st = function(t, e, s) {
  const i = t.currentTarget, a = i.closest(".card");
  if (!a) return;
  const r = a.getBoundingClientRect(), d = i.getBoundingClientRect();
  this._tooltip = { card: a, x: d.left - r.left + d.width / 2, y: d.top - r.top, title: e, lines: s };
};
G = /* @__PURE__ */ new WeakMap();
j = function(t) {
  const e = this._tooltip;
  return !e || e.card.dataset.card !== t ? _ : n`<div class="tooltip" style="left:${e.x}px;top:${e.y}px" role="status">
      <div class="tooltip-title">${e.title}</div>
      ${e.lines.map((s) => n`<div class="tooltip-line"><strong>${s.value}</strong><span>${s.label}</span></div>`)}
    </div>`;
};
jt = function() {
  const t = this._report?.currencies ?? [], e = !!this._report && !this._report.rates;
  return n`<div class="filters">
      <uui-button-group aria-label="Period">
        ${re.map(
    (s) => n`<uui-button
            compact
            look=${s === this._days ? "primary" : "secondary"}
            aria-pressed=${s === this._days}
            @click=${() => l(this, o, Nt).call(this, s)}
            >${s === 365 ? "12 months" : `${s} days`}</uui-button
          >`
  )}
      </uui-button-group>
      <label class="currency">
        <span>Currency</span>
        <select @change=${l(this, o, Ot)} ?disabled=${e} aria-label="Currency">
          ${t.map((s) => n`<option value=${s} ?selected=${s === m(this, o, D)}>${s}</option>`)}
        </select>
      </label>
    </div>`;
};
H = function(t, e) {
  if (!this._report?.previous || e === void 0) return _;
  const s = Te(t, e), i = `vs previous ${l(this, o, Lt).call(this)}`;
  return s.kind === "new" ? n`<div class="tile-change">New ${i}</div>` : s.kind === "change" ? n`<div class="tile-change">${s.percent > 0 ? "+" : ""}${s.percent} % ${i}</div>` : _;
};
Vt = function() {
  const t = this._report, e = It(t, m(this, o, D));
  return n`<uui-box class="tile">
      <div class="tile-label">This month</div>
      <div class="tile-value">${l(this, o, q).call(this, t.month.cost)}</div>
      <div class="tile-note">Projected ${l(this, o, q).call(this, t.month.projectedCost)} by month end</div>
      ${e.kind === "set" ? n`<div
              class="budget budget-${e.level}"
              role="meter"
              aria-valuemin="0"
              aria-valuemax="100"
              aria-valuenow=${Math.round(e.spentShare * 100)}
              aria-label="Share of the monthly budget used">
              <span class="budget-fill" style="width:${Math.min(100, e.spentShare * 100)}%"></span>
            </div>
            <div class="tile-note">${f(e.spentShare * 100)} % of ${ht(e.amount, m(this, o, D))}</div>` : e.kind === "unavailable" ? n`<div class="tile-note">${e.reason}</div>` : _}
    </uui-box>`;
};
Ht = function() {
  const t = this._report, e = t.totals, s = t.previous?.totals, i = t.days.reduce((c, u) => c + u.inputTokens, 0), a = t.days.reduce((c, u) => c + u.outputTokens, 0), r = e.requests > 0 ? e.cost / e.requests : 0, d = s && s.requests > 0 && e.requests > 0 ? s.cost / s.requests : void 0;
  return n`<div class="tiles">
      ${l(this, o, Vt).call(this)}
      <uui-box class="tile tile-hero">
        <div class="tile-label">Estimated cost</div>
        <div class="tile-value hero">${l(this, o, q).call(this, e.cost)}</div>
        ${e.unpricedTokens > 0 ? n`<div class="tile-note">
              <uui-icon name="icon-alert"></uui-icon> ${f(e.unpricedTokens)} tokens from models without a price
            </div>` : n`<div class="tile-note">${l(this, o, Gt).call(this)}</div>`}
        ${l(this, o, H).call(this, e.cost, s?.cost)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Tokens</div>
        <div class="tile-value">${f(e.totalTokens, { compact: !0 })}</div>
        <div class="tile-note">${f(i, { compact: !0 })} in · ${f(a, { compact: !0 })} out</div>
        ${l(this, o, H).call(this, e.totalTokens, s?.totalTokens)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Requests</div>
        <div class="tile-value">${f(e.requests)}</div>
        <div class="tile-note">${f(e.requests / t.days.length)} per day on average</div>
        ${l(this, o, H).call(this, e.requests, s?.requests)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Cost per request</div>
        <div class="tile-value">${e.requests > 0 ? l(this, o, q).call(this, r) : "–"}</div>
        <div class="tile-note">Priced models only</div>
        ${l(this, o, H).call(this, r, d)}
      </uui-box>
    </div>`;
};
Gt = function() {
  const t = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" });
  return `${t.format(I(this._report.from))} – ${t.format(I(this._report.to))}`;
};
Xt = function() {
  const t = this._report.days, e = t.map((h) => l(this, o, Bt).call(this, h)), s = De(e.filter((h) => h > 0)), i = (I(t[0].date).getUTCDay() + 6) % 7, a = Math.ceil((i + t.length) / 7), r = ut + a * (S + N), d = dt + 7 * (S + N), c = new Intl.DateTimeFormat(void 0, { month: "short" }), u = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short", year: "numeric" }), v = new Intl.DateTimeFormat(void 0, { weekday: "short" }), b = [], T = t.map((h, $) => {
    const O = i + $, B = Math.floor(O / 7), P = O % 7, g = I(h.date);
    (g.getUTCDate() === 1 || $ === 0) && b.push({ col: B, label: c.format(g) });
    const p = e[$], C = p > 0 ? Re(p, s) : -1;
    return M`<rect
        class="cell ${C < 0 ? "cell-empty" : `cell-${C}`}"
        x=${ut + B * (S + N)}
        y=${dt + P * (S + N)}
        width=${S}
        height=${S}
        rx="2"
        @pointerenter=${(F) => l(this, o, st).call(this, F, u.format(g), [
      { value: l(this, o, q).call(this, h.cost), label: "cost" },
      { value: f(h.totalTokens), label: "tokens" },
      { value: f(h.requests), label: h.requests === 1 ? "request" : "requests" }
    ])}
        @pointerleave=${m(this, G)}
      ></rect>`;
  }), x = b.filter((h, $) => $ === 0 || h.col - b[$ - 1].col >= 3);
  return n`<uui-box class="card" data-card="heatmap" headline="Usage calendar">
      <div slot="header-actions">
        <uui-button-group aria-label="Heatmap metric">
          ${ne.map(
    (h) => n`<uui-button
              compact
              look=${h.key === this._metric ? "primary" : "secondary"}
              aria-pressed=${h.key === this._metric}
              @click=${() => l(this, o, At).call(this, h.key)}
              >${h.label}</uui-button
            >`
  )}
        </uui-button-group>
      </div>
      <div class="heatmap-scroll">
        <svg
          class="heatmap"
          width=${r}
          height=${d}
          viewBox="0 0 ${r} ${d}"
          role="img"
          aria-label="Daily ${this._metric} over the selected period. The table under the cost chart lists every value.">
          ${x.map((h) => M`<text class="axis-label" x=${ut + h.col * (S + N)} y="11">${h.label}</text>`)}
          ${[0, 2, 4].map((h) => {
    const $ = v.format(new Date(Date.UTC(2024, 0, 1 + h)));
    return M`<text class="axis-label" x="0" y=${dt + h * (S + N) + S - 2}>${$}</text>`;
  })}
          ${T}
        </svg>
      </div>
      <div class="scale-legend" aria-hidden="true">
        <span class="swatch cell-empty"></span><span>No usage</span>
        <span class="scale-gap"></span>
        <span>Less</span>
        ${[0, 1, 2, 3].map((h) => n`<span class="swatch cell-${h}"></span>`)}
        <span>More</span>
        ${s.length ? n`<span class="scale-range">up to ${l(this, o, Pt).call(this, Math.max(...e))} a day</span>` : _}
      </div>
      ${l(this, o, j).call(this, "heatmap")}
    </uui-box>`;
};
at = function() {
  const t = this._report.days.length > 92;
  return { buckets: t ? ze(this._report.days) : we(this._report.days), weekly: t };
};
L = /* @__PURE__ */ new WeakMap();
it = /* @__PURE__ */ new WeakMap();
ot = /* @__PURE__ */ new WeakMap();
Jt = function(t) {
  const e = new Set(this._tables);
  e.has(t) ? e.delete(t) : e.add(t), this._tables = e;
};
X = function(t) {
  return n`<uui-button compact look="secondary" @click=${() => l(this, o, Jt).call(this, t)}>
      ${this._tables.has(t) ? "Show chart" : "Show as table"}
    </uui-button>`;
};
Qt = function(t) {
  if (this._costView === "total")
    return [{ id: "cost", label: "Cost", color: "var(--viz-cat-1)", values: t.map((i) => l(this, o, W).call(this, i.cost)) }];
  const { ids: e, values: s } = ye(this._report, t);
  return e.map((i, a) => ({ id: i, label: i, color: Q(i, a), values: s[a].map((r) => l(this, o, W).call(this, r)) }));
};
lt = function(t) {
  return t.length < 2 ? _ : n`<div class="legend">
      ${t.map(
    (e) => n`<span class="legend-item"
          >${e.icon ? n`<uui-icon name=${e.icon} style="color:${e.color}"></uui-icon>` : n`<span class="legend-swatch" style="background:${e.color}"></span>`}${e.label}</span
        >`
  )}
    </div>`;
};
rt = function(t, e, s, i, a, r) {
  const d = t.map((g, p) => s.reduce((C, F) => C + F.values[p], 0)), c = Math.max(0, ...d), u = Et(c), v = u[u.length - 1] || 1, b = Math.max(200, this._chartWidth - y), T = b / t.length, x = Math.max(1, Math.min(Oe, T - N)), h = (g) => g / v * R, $ = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short" }), O = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), B = Math.max(1, Math.ceil(t.length / Math.max(2, Math.floor(b / 90)))), P = ge(s.map((g) => g.values));
  return n`<div class="column-chart">
      <svg width=${y + b} height=${tt + R + ce} role="img" aria-label="${r}. Use “Show as table” for the values.">
        <g transform="translate(0 ${tt})">
          ${u.map(
    (g) => M`
              <line class="grid" x1=${y} x2=${y + b} y1=${R - h(g)} y2=${R - h(g)}></line>
              <text class="axis-label tick" x=${y - 8} y=${R - h(g) + 4}>${a(g)}</text>`
  )}
          ${t.map((g, p) => {
    const C = y + p * T + (T - x) / 2, F = e ? `Week of ${$.format(I(g.start))}` : O.format(I(g.start)), J = s.map((E) => ({ value: i(E.values[p]), label: E.label.toLowerCase() }));
    s.length > 1 && J.push({ value: i(d[p]), label: "total" });
    const ue = s.map((E) => E.values[p] > 0).lastIndexOf(!0);
    return M`
              <rect class="hit" x=${y + p * T} y="0" width=${T} height=${R}
                @pointerenter=${(E) => l(this, o, st).call(this, E, F, J)}
                @pointerleave=${m(this, G)}></rect>
              ${s.map((E, $t) => {
      const { y0: yt, y1: gt } = P[$t][p], ct = h(gt) - h(yt);
      return ct <= 0 ? _ : $t === ue ? M`<path class="bar" style="fill:${E.color}" d=${Se(C, R - h(yt), x, ct)}></path>` : M`<rect class="bar" style="fill:${E.color}" x=${C} y=${R - h(gt) + N} width=${x} height=${Math.max(0, ct - N)}></rect>`;
    })}
              ${p % B === 0 ? M`<text class="axis-label" x=${C + x / 2} y=${R + 18} text-anchor="middle">${$.format(I(g.start))}</text>` : _}`;
  })}
          <line class="baseline" x1=${y} x2=${y + b} y1=${R} y2=${R}></line>
        </g>
      </svg>
    </div>`;
};
nt = function(t, e, s, i, a) {
  const r = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }), d = t.map((c, u) => ({ b: c, i: u })).filter(({ i: c }) => s.some((u) => u.values[c] > 0)).reverse();
  return d.length ? n`<div class="table-scroll">
      <uui-table aria-label=${a}>
        <uui-table-head>
          <uui-table-head-cell>${e ? "Week of" : "Date"}</uui-table-head-cell>
          ${s.map((c) => n`<uui-table-head-cell class="num">${c.label}</uui-table-head-cell>`)}
          ${s.length > 1 ? n`<uui-table-head-cell class="num">Total</uui-table-head-cell>` : _}
        </uui-table-head>
        ${d.map(
    ({ b: c, i: u }) => n`<uui-table-row>
            <uui-table-cell>${r.format(I(c.start))}</uui-table-cell>
            ${s.map((v) => n`<uui-table-cell class="num">${i(v.values[u])}</uui-table-cell>`)}
            ${s.length > 1 ? n`<uui-table-cell class="num">${i(s.reduce((v, b) => v + b.values[u], 0))}</uui-table-cell>` : _}
          </uui-table-row>`
  )}
      </uui-table>
    </div>` : n`<p class="empty">No AI usage in this period.</p>`;
};
Yt = function() {
  const t = this._report, e = I(t.month.to), s = new Date(Date.UTC(e.getUTCFullYear(), e.getUTCMonth() + 1, 0)).getUTCDate(), i = It(t, m(this, o, D)), a = i.kind === "set" ? i.amount : void 0, r = $e(t.month, s), d = (p) => ({ day: p.day, value: l(this, o, W).call(this, p.value) }), c = r.actual.map(d), u = r.projection.map(d), v = Math.max(l(this, o, W).call(this, r.max), a ?? 0), b = Et(v), T = b[b.length - 1] || 1, x = Math.max(200, this._chartWidth - y - xt), h = (p) => y + (p - 1) / Math.max(1, s - 1) * x, $ = (p) => A - p / T * A, O = (p) => p.map((C, F) => `${F ? "L" : "M"}${h(C.day)},${$(C.value)}`).join(" "), B = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), P = x / Math.max(1, s - 1), g = this._tables.has("month");
  return n`<uui-box class="card" data-card="month" headline="This month">
      <div slot="header-actions" class="header-actions">${l(this, o, X).call(this, "month")}</div>
      ${g ? l(this, o, Zt).call(this) : n`<div class="legend">
              <span class="legend-item"><span class="legend-line"></span>Cost so far</span>
              <span class="legend-item"><span class="legend-line dashed"></span>Projected ${l(this, o, q).call(this, t.month.projectedCost)}</span>
              ${a !== void 0 ? n`<span class="legend-item"><span class="legend-line budget"></span>Budget ${m(this, L).call(this, a)}</span>` : _}
            </div>
            <div class="column-chart">
              <svg width=${y + x + xt} height=${tt + A + ce} role="img"
                aria-label="Cumulative cost this month with the projection to month end. Use “Show as table” for the values.">
                <g transform="translate(0 ${tt})">
                  ${b.map(
    (p) => M`
                      <line class="grid" x1=${y} x2=${y + x} y1=${$(p)} y2=${$(p)}></line>
                      <text class="axis-label tick" x=${y - 8} y=${$(p) + 4}>${m(this, it).call(this, p)}</text>`
  )}
                  ${a !== void 0 ? M`<line class="budget-line" x1=${y} x2=${y + x} y1=${$(a)} y2=${$(a)}></line>
                        <text class="axis-label" x=${y + x + 6} y=${$(a) + 4}>Budget</text>` : _}
                  <path class="line projection" d=${O(u)}></path>
                  <path class="line" d=${O(c)}></path>
                  ${c.length ? M`<circle class="dot" cx=${h(c[c.length - 1].day)} cy=${$(c[c.length - 1].value)} r="4"></circle>` : _}
                  ${[1, 8, 15, 22, s].map(
    (p) => M`<text class="axis-label" x=${h(p)} y=${A + 18} text-anchor="middle">${p}</text>`
  )}
                  ${c.map((p, C) => {
    const F = I(t.month.days[C].date);
    return M`<rect class="hit" x=${h(p.day) - P / 2} y="0" width=${P} height=${A}
                      @pointerenter=${(J) => l(this, o, st).call(this, J, B.format(F), [
      { value: m(this, L).call(this, p.value), label: "so far" },
      { value: l(this, o, q).call(this, t.month.days[C].cost), label: "that day" }
    ])}
                      @pointerleave=${m(this, G)}></rect>`;
  })}
                  <line class="baseline" x1=${y} x2=${y + x} y1=${A} y2=${A}></line>
                </g>
              </svg>
            </div>`}
      ${l(this, o, j).call(this, "month")}
    </uui-box>`;
};
Zt = function() {
  const t = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" });
  let e = 0;
  const s = this._report.month.days.map((i) => ({ d: i, total: e += i.cost })).reverse();
  return n`<div class="table-scroll">
      <uui-table aria-label="Cost this month per day">
        <uui-table-head>
          <uui-table-head-cell>Date</uui-table-head-cell>
          <uui-table-head-cell class="num">Cost</uui-table-head-cell>
          <uui-table-head-cell class="num">So far</uui-table-head-cell>
        </uui-table-head>
        ${s.map(
    ({ d: i, total: a }) => n`<uui-table-row>
            <uui-table-cell>${t.format(I(i.date))}</uui-table-cell>
            <uui-table-cell class="num">${l(this, o, q).call(this, i.cost)}</uui-table-cell>
            <uui-table-cell class="num">${l(this, o, q).call(this, a)}</uui-table-cell>
          </uui-table-row>`
  )}
      </uui-table>
    </div>`;
};
Kt = function() {
  const { buckets: t, weekly: e } = l(this, o, at).call(this), s = l(this, o, Qt).call(this, t), i = s.every((c) => c.values.every((u) => u === 0)), a = `${e ? "Weekly" : "Daily"} cost`, r = this._tables.has("columns"), d = this._report.totals.unpricedTokens > 0;
  return n`<uui-box class="card" data-card="columns" headline=${a}>
      <div slot="header-actions" class="header-actions">
        <uui-button-group aria-label="Cost per">
          ${["total", "model"].map(
    (c) => n`<uui-button compact look=${c === this._costView ? "primary" : "secondary"} aria-pressed=${c === this._costView}
              @click=${() => this._costView = c}>${c === "total" ? "Total" : "By model"}</uui-button>`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" @click=${() => l(this, o, ie).call(this, t, e, s)}>Download CSV</uui-button>
        ${l(this, o, X).call(this, "columns")}
      </div>
      ${l(this, o, lt).call(this, s)}
      ${r ? l(this, o, nt).call(this, t, e, s, m(this, L), `${a} per ${this._costView === "total" ? "period" : "model"}`) : i ? n`<p class="empty">
              ${d ? "No cost to show — the models used in this period have no configured price." : "No AI usage in this period."}
            </p>` : l(this, o, rt).call(this, t, e, s, m(this, L), m(this, it), `${a}${this._costView === "model" ? " by model" : ""}`)}
      ${this._costView === "model" && d ? n`<p class="chart-note">Models without a configured price aren’t included.</p>` : _}
      ${l(this, o, j).call(this, "columns")}
    </uui-box>`;
};
te = function() {
  const { buckets: t, weekly: e } = l(this, o, at).call(this), s = xe(t), i = (r) => f(r), a = `Tokens by type${e ? " per week" : ""}`;
  return n`<uui-box class="card" data-card="tokens" headline=${a}>
      <div slot="header-actions" class="header-actions">${l(this, o, X).call(this, "tokens")}</div>
      <div class="figures">
        <div><div class="tile-label">Caching saved</div><div class="figure">${l(this, o, q).call(this, this._report.cachingSavings)}</div></div>
      </div>
      ${l(this, o, lt).call(this, s)}
      ${this._tables.has("tokens") ? l(this, o, nt).call(this, t, e, s, i, a) : s.every((r) => r.values.every((d) => d === 0)) ? n`<p class="empty">No AI usage in this period.</p>` : l(this, o, rt).call(this, t, e, s, i, m(this, ot), a)}
      ${l(this, o, j).call(this, "tokens")}
    </uui-box>`;
};
ee = function() {
  const t = this._report, { buckets: e, weekly: s } = l(this, o, at).call(this), i = t.days.reduce((u, v) => u + v.succeededRequests, 0), a = t.days.reduce((u, v) => u + v.failedRequests, 0), r = [
    { id: "succeeded", label: "Succeeded", color: "var(--viz-cat-1)", values: e.map((u) => u.succeededRequests) },
    { id: "failed", label: "Failed", color: "var(--viz-critical)", icon: "icon-alert", values: e.map((u) => u.failedRequests) }
  ], d = (u) => f(u), c = `Requests${s ? " per week" : ""}`;
  return n`<uui-box class="card" data-card="reliability" headline="Reliability">
      <div slot="header-actions" class="header-actions">${l(this, o, X).call(this, "reliability")}</div>
      <div class="figures">
        <div><div class="tile-label">Failed requests</div><div class="figure">${f(a)} <span class="figure-note">(${f(ke(i, a) * 100)} %)</span></div></div>
        <div><div class="tile-label">Average response time</div><div class="figure">${t.averageDurationMs > 0 ? _e(t.averageDurationMs) : "–"}</div></div>
      </div>
      ${l(this, o, lt).call(this, r)}
      ${this._tables.has("reliability") ? l(this, o, nt).call(this, e, s, r, d, c) : i + a === 0 ? n`<p class="empty">No AI usage in this period.</p>` : l(this, o, rt).call(this, e, s, r, d, m(this, ot), c)}
      ${l(this, o, j).call(this, "reliability")}
    </uui-box>`;
};
se = function() {
  return this._dimension === "model" ? this._report.models.map((t) => ({ ...t, name: t.modelId })) : this._breakdowns.get(`${this._dimension}:${this._days}`)?.rows;
};
ae = function() {
  const t = _t.find((a) => a.key === this._dimension).label, e = l(this, o, se).call(this), s = this._report.totals.cost, i = this._dimension === "model" ? "Add this model to Esatto:AICosts:PricesPerMillionTokens in appsettings" : "Some of this usage is from models without a configured price";
  return n`<uui-box class="card" headline="Cost by">
      <div slot="header-actions" class="header-actions">
        <uui-button-group aria-label="Group cost by">
          ${_t.map(
    (a) => n`<uui-button
              compact
              look=${a.key === this._dimension ? "primary" : "secondary"}
              aria-pressed=${a.key === this._dimension}
              @click=${() => l(this, o, Wt).call(this, a.key)}
              >${a.label}</uui-button
            >`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" ?disabled=${!e?.length} @click=${() => l(this, o, oe).call(this, t, e ?? [])}
          >Download CSV</uui-button
        >
      </div>
      ${this._breakdownError && this._dimension !== "model" ? n`<p class="error">${this._breakdownError}</p>` : e ? e.length === 0 ? n`<p class="empty">No AI usage in this period.</p>` : n`<div class="table-scroll">
                <uui-table aria-label="Cost by ${t.toLowerCase()}">
                  <uui-table-head>
                    <uui-table-head-cell>${t}</uui-table-head-cell>
                    <uui-table-head-cell class="num">Requests</uui-table-head-cell>
                    <uui-table-head-cell class="num">Input</uui-table-head-cell>
                    <uui-table-head-cell class="num">Cached input</uui-table-head-cell>
                    <uui-table-head-cell class="num">Output</uui-table-head-cell>
                    <uui-table-head-cell class="num">Cost</uui-table-head-cell>
                    <uui-table-head-cell class="num">Share</uui-table-head-cell>
                  </uui-table-head>
                  ${e.map(
    (a) => n`<uui-table-row>
                      <uui-table-cell>${a.name}</uui-table-cell>
                      <uui-table-cell class="num">${f(a.requests)}</uui-table-cell>
                      <uui-table-cell class="num">${f(a.inputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${f(a.cachedInputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${f(a.outputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">
                        ${a.cost === null ? n`<uui-tag look="secondary" title=${i}>No price</uui-tag>` : l(this, o, q).call(this, a.cost)}
                      </uui-table-cell>
                      <uui-table-cell class="num">
                        ${a.cost !== null && s > 0 ? n`<span class="share"
                              ><span class="share-track"
                                ><span class="share-fill" style="width:${a.cost / s * 100}%"></span></span
                              >${f(a.cost / s * 100)}%</span
                            >` : "–"}
                      </uui-table-cell>
                    </uui-table-row>`
  )}
                </uui-table>
              </div>` : n`<uui-loader-bar></uui-loader-bar>`}
      ${this._breakdownLoading && e ? n`<uui-loader-bar></uui-loader-bar>` : _}
    </uui-box>`;
};
ft = function(t) {
  return Number(l(this, o, W).call(this, t).toFixed(4));
};
ie = function(t, e, s) {
  const i = this._report, a = this._costView === "model", r = [
    e ? "Week of" : "Date",
    "Requests",
    "Input tokens",
    "Cached input tokens",
    "Output tokens",
    "Total tokens",
    `Cost (${m(this, o, D)})`,
    "Unpriced tokens",
    ...a ? s.map((c) => `${c.label} (${m(this, o, D)})`) : []
  ], d = t.map((c, u) => [
    c.start,
    c.requests,
    c.inputTokens,
    c.cachedInputTokens,
    c.outputTokens,
    c.totalTokens,
    l(this, o, ft).call(this, c.cost),
    c.unpricedTokens,
    ...a ? s.map((v) => Number(v.values[u].toFixed(4))) : []
  ]);
  Rt(`ai-costs-${i.from}-${i.to}${a ? "-by-model" : ""}.csv`, Dt(r, d));
};
oe = function(t, e) {
  const s = this._report, i = [t, "Requests", "Input tokens", "Cached input tokens", "Output tokens", "Total tokens", `Cost (${m(this, o, D)})`], a = e.map((r) => [r.name, r.requests, r.inputTokens, r.cachedInputTokens, r.outputTokens, r.totalTokens, r.cost === null ? "" : l(this, o, ft).call(this, r.cost)]);
  Rt(`ai-costs-${s.from}-${s.to}-${this._dimension}.csv`, Dt(i, a));
};
le = function() {
  const t = this._report, e = t.rates ? new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }).format(I(t.rates.date)) : null;
  return n`<p class="footnote">
      Estimates: tokens × list prices per model${t.pricesSource ? n` (${t.pricesSource})` : _}, in ${t.baseCurrency}.
      Cached input tokens are billed at the cache-read price. Excludes taxes, discounts and provider surcharges.
      ${t.rates ? n`Converted with ${t.rates.source} reference rates of ${e}.` : n`<strong>Exchange rates are unavailable right now — amounts are shown in ${t.baseCurrency}.</strong>`}
      Times are UTC.
    </p>`;
};
k.styles = [Fe];
w([
  z()
], k.prototype, "_report", 2);
w([
  z()
], k.prototype, "_loading", 2);
w([
  z()
], k.prototype, "_error", 2);
w([
  z()
], k.prototype, "_days", 2);
w([
  z()
], k.prototype, "_currency", 2);
w([
  z()
], k.prototype, "_metric", 2);
w([
  z()
], k.prototype, "_tables", 2);
w([
  z()
], k.prototype, "_costView", 2);
w([
  z()
], k.prototype, "_tooltip", 2);
w([
  z()
], k.prototype, "_dark", 2);
w([
  z()
], k.prototype, "_chartWidth", 2);
w([
  z()
], k.prototype, "_dimension", 2);
w([
  z()
], k.prototype, "_breakdowns", 2);
w([
  z()
], k.prototype, "_breakdownLoading", 2);
w([
  z()
], k.prototype, "_breakdownError", 2);
k = w([
  he("esatto-ai-costs-view")
], k);
const Le = k;
export {
  k as EsattoAICostsViewElement,
  Le as default
};
//# sourceMappingURL=ai-costs-view.js.map
