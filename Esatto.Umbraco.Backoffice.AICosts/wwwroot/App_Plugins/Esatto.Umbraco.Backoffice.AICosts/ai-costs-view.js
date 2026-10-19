import { css as ue, state as T, customElement as he, nothing as _, html as l, svg as M } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as de } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as wt } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as xt } from "@umbraco-cms/backoffice/http-client";
const zt = "/umbraco/management/api/v1/esatto/ai-costs", Tt = () => [{ scheme: "bearer", type: "http" }], pe = (t) => xt.get({ url: `${zt}/report?days=${t}`, security: Tt() }), me = (t, e) => xt.get({ url: `${zt}/breakdown?dimension=${t}&days=${e}`, security: Tt() });
function ve(t) {
  if (!t.rates) return t;
  const e = Object.fromEntries(
    Object.entries(t.rates.perBaseUnit).map(([s, a]) => [s.toUpperCase(), a])
  );
  return { ...t, rates: { ...t.rates, perBaseUnit: e } };
}
function be(t, e) {
  return e && (e === t.baseCurrency || t.rates?.perBaseUnit[e]) ? e : t.baseCurrency;
}
function Ct(t, e, s) {
  return s === t.baseCurrency ? e : e * (t.rates?.perBaseUnit[s] ?? 1);
}
function mt(t, e, { compact: s = !1 } = {}) {
  const a = Math.abs(t) > 0 && Math.abs(t) < 1;
  return new Intl.NumberFormat(void 0, {
    style: "currency",
    currency: e,
    notation: s ? "compact" : "standard",
    minimumFractionDigits: s ? 0 : 2,
    maximumFractionDigits: s ? 1 : a ? 4 : 2
  }).format(t);
}
function f(t, { compact: e = !1 } = {}) {
  return new Intl.NumberFormat(void 0, { notation: e ? "compact" : "standard", maximumFractionDigits: 1 }).format(t);
}
function Mt(t, e) {
  const s = t.month.budget;
  if (!s) return { kind: "none" };
  const a = s.currency === t.baseCurrency ? 1 : t.rates?.perBaseUnit[s.currency];
  if (!a)
    return { kind: "unavailable", reason: `Exchange rates are unavailable, so the ${s.currency} budget can’t be compared right now.` };
  const o = s.amount / a, r = o > 0 ? t.month.cost / o : 0, h = t.month.cost > o ? "over" : r >= 0.8 || t.month.projectedCost > o ? "warning" : "ok";
  return { kind: "set", amount: Ct(t, o, e), spentShare: r, level: h };
}
function fe(t, e) {
  let s = 0;
  const a = t.days.map((h, c) => ({ day: c + 1, value: s += h.cost })), o = a[a.length - 1] ?? { day: 0, value: 0 }, r = [o, { day: e, value: t.projectedCost }];
  return { actual: a, projection: r, max: Math.max(t.projectedCost, o.value) };
}
function $e(t, e, s = 7) {
  const a = (u) => e.map((v) => t.days.reduce((b, w, z) => w.date >= v.start && w.date <= v.end ? b + u[z] : b, 0)), o = t.modelDays.map((u) => ({ id: u.modelId, values: a(u.costs), total: u.costs.reduce((v, b) => v + b, 0) })).sort((u, v) => v.total - u.total);
  if (o.length <= s) return { ids: o.map((u) => u.id), values: o.map((u) => u.values) };
  const r = o.slice(0, s - 1), h = o.slice(s - 1), c = e.map((u, v) => h.reduce((b, w) => b + w.values[v], 0));
  return { ids: [...r.map((u) => u.id), "Other"], values: [...r.map((u) => u.values), c] };
}
function ge(t) {
  const e = new Array(t[0]?.length ?? 0).fill(0);
  return t.map(
    (s) => s.map((a, o) => {
      const r = e[o];
      return e[o] += a, { y0: r, y1: e[o] };
    })
  );
}
function ye(t, e) {
  const s = t + e;
  return s === 0 ? 0 : e / s;
}
function ke(t) {
  return t < 1e3 ? `${Math.round(t)} ms` : `${new Intl.NumberFormat(void 0, { maximumFractionDigits: 1 }).format(t / 1e3)} s`;
}
function K(t, e) {
  return t === "Other" ? "var(--viz-other)" : `var(--viz-cat-${e + 1})`;
}
function _e(t) {
  return [
    { id: "uncached", label: "Uncached input", color: K("uncached", 0), values: t.map((e) => Math.max(0, e.inputTokens - e.cachedInputTokens)) },
    { id: "cached", label: "Cached input", color: K("cached", 1), values: t.map((e) => e.cachedInputTokens) },
    { id: "output", label: "Output", color: K("output", 2), values: t.map((e) => e.outputTokens) }
  ];
}
function we(t, e, s, a) {
  return s + (t - 1) / Math.max(1, e - 1) * a;
}
function xe(t, e, s, a) {
  const o = t[0]?.length ?? 0, r = Array.from({ length: o }, (h, c) => t.map((u) => u[c].y1 > u[c].y0).lastIndexOf(!0));
  return t.map(
    (h, c) => h.map(({ y0: u, y1: v }, b) => {
      const w = e(v) - e(u);
      return w <= 0 ? null : c === r[b] ? { kind: "rounded", base: s - e(u), height: w } : { kind: "rect", y: s - e(v) + a, height: Math.max(0, w - a) };
    })
  );
}
function ze(t, e, s) {
  const a = e.get(t);
  if (a) return { kind: "rows", breakdown: a };
  const o = s.get(t);
  return o ? { kind: "error", message: o } : { kind: "loading" };
}
function Te(t, e) {
  return t.includes(e) ? t : [...t, e];
}
function Ce(t, e) {
  return t ? e > 0 ? "No cost to show — the models used in this period have no configured price." : "No AI usage in this period." : null;
}
function q(t) {
  const [e, s, a] = t.split("-").map(Number);
  return new Date(Date.UTC(e, s - 1, a));
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
}), Me = (t) => t.map(qt);
function qe(t) {
  const e = [];
  for (const s of t) {
    const a = e[e.length - 1];
    if (!a || q(s.date).getUTCDay() === 1) {
      e.push(qt(s));
      continue;
    }
    a.end = s.date, a.requests += s.requests, a.inputTokens += s.inputTokens, a.outputTokens += s.outputTokens, a.cachedInputTokens += s.cachedInputTokens, a.totalTokens += s.totalTokens, a.cost += s.cost, a.unpricedTokens += s.unpricedTokens, a.succeededRequests += s.succeededRequests, a.failedRequests += s.failedRequests;
  }
  return e;
}
function Ie(t, e) {
  return e == null ? { kind: "none" } : e === 0 ? t === 0 ? { kind: "change", percent: 0 } : { kind: "new" } : { kind: "change", percent: Math.round((t - e) / e * 100) };
}
function De(t) {
  return new Intl.NumberFormat(t).formatToParts(1.5).find((s) => s.type === "decimal")?.value === "," ? { field: ";", decimal: "," } : { field: ",", decimal: "." };
}
const Re = /^[=+\-@\t\r]/;
function It(t, e, s) {
  const { field: a, decimal: o } = De(s), r = (h) => {
    const c = typeof h == "number" ? String(h).replace(".", o) : Re.test(h) ? `'${h}` : h;
    return /["\r\n]/.test(c) || c.includes(a) ? `"${c.replace(/"/g, '""')}"` : c;
  };
  return "\uFEFF" + [t, ...e].map((h) => h.map(r).join(a)).join(`\r
`);
}
function Dt(t, e) {
  const s = URL.createObjectURL(new Blob([e], { type: "text/csv;charset=utf-8" }));
  Object.assign(document.createElement("a"), { href: s, download: t }).click(), URL.revokeObjectURL(s);
}
class yt {
  #t = 0;
  next() {
    return ++this.#t;
  }
  isCurrent(e) {
    return e === this.#t;
  }
}
const Rt = "esatto.aiCosts.prefs", St = () => {
  try {
    return globalThis.localStorage;
  } catch {
    return;
  }
};
function Se(t = St()) {
  try {
    const e = JSON.parse(t?.getItem(Rt) ?? "{}");
    return e && typeof e == "object" && !Array.isArray(e) ? e : {};
  } catch {
    return {};
  }
}
function Ee(t, e = St()) {
  try {
    e?.setItem(Rt, JSON.stringify(t));
  } catch {
  }
}
function Fe(t) {
  if (!t.length) return [];
  const e = [...t].sort((s, a) => s - a);
  return [0.25, 0.5, 0.75].map((s) => e[Math.min(e.length - 1, Math.floor(s * e.length))]);
}
function Ue(t, e) {
  for (let s = 0; s < e.length; s++) if (t <= e[s]) return s;
  return 3;
}
function Et(t) {
  if (t <= 0) return [0, 1];
  const e = t / 4, s = 10 ** Math.floor(Math.log10(e)), a = [1, 2, 2.5, 5, 10].map((r) => r * s).find((r) => r >= e) ?? 10 * s, o = [];
  for (let r = 0; r <= t + a * 0.999; r += a) o.push(Number(r.toFixed(10)));
  return o;
}
function Ae(t, e, s, a) {
  const o = Math.min(4, s / 2, a), r = e - a;
  return `M${t},${e} V${r + o} Q${t},${r} ${t + o},${r} H${t + s - o} Q${t + s},${r} ${t + s},${r + o} V${e} Z`;
}
const R = 12, Oe = ue`
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
    /* A figure can't wrap (the currency formatter joins code and amount with a no-break space), so in a
       narrow tile it shrinks to fit: --chars is its length (#figure), at about 0.56em a character in Lato. */
    .tile-value {
        container-type: inline-size;
        font-size: 1.75rem;
        font-weight: 600;
        line-height: 1.2;
        margin: var(--uui-size-space-2) 0;
        color: var(--uui-color-text);
    }
    .tile-value.hero {
        font-size: 3rem;
    }
    .tile-value > span {
        display: block;
        font-size: min(1em, 100cqi / (var(--chars) * 0.56));
        white-space: nowrap;
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
        width: ${R}px;
        height: ${R}px;
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
    .crosshair {
        stroke: var(--viz-axis);
        stroke-width: 1;
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
var Ne = Object.defineProperty, We = Object.getOwnPropertyDescriptor, Ft = (t) => {
  throw TypeError(t);
}, x = (t, e, s, a) => {
  for (var o = a > 1 ? void 0 : a ? We(e, s) : e, r = t.length - 1, h; r >= 0; r--)
    (h = t[r]) && (o = (a ? h(e, s, o) : h(o)) || o);
  return a && o && Ne(e, s, o), o;
}, vt = (t, e, s) => e.has(t) || Ft("Cannot " + s), m = (t, e, s) => (vt(t, e, "read from private field"), s ? s.call(t) : e.get(t)), F = (t, e, s) => e.has(t) ? Ft("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, s), Be = (t, e, s, a) => (vt(t, e, "write to private field"), e.set(t, s), s), n = (t, e, s) => (vt(t, e, "access private method"), s), G, Z, tt, et, i, bt, ft, pt, at, $t, Ut, At, Ot, Nt, I, W, D, j, Wt, Bt, Lt, it, J, V, Pt, X, jt, Ht, Vt, Gt, ot, H, rt, nt, Xt, Q, Jt, lt, ct, ut, Qt, Yt, Kt, Zt, te, ee, se, gt, ae, ie, oe;
const re = [30, 90, 180, 365], ne = [
  { key: "cost", label: "Cost" },
  { key: "tokens", label: "Tokens" },
  { key: "requests", label: "Requests" }
], kt = [
  { key: "model", label: "Model" },
  { key: "profile", label: "Profile" },
  { key: "user", label: "User" },
  { key: "provider", label: "Provider" }
], A = 2, ht = 30, dt = 18, st = 10, S = 220, U = 200, _t = 56, g = 64, le = 26, Le = 24;
let y = class extends de {
  constructor() {
    super(), F(this, i), this._loading = !1, this._error = null, this._tables = /* @__PURE__ */ new Set(), this._costView = "total", this._tooltip = null, this._dark = !1, this._chartWidth = 900, this._dimension = "model", this._breakdowns = /* @__PURE__ */ new Map(), this._breakdownLoading = !1, this._breakdownErrors = /* @__PURE__ */ new Map(), this._monthHover = null, F(this, G, new ResizeObserver((e) => {
      const s = Math.floor(e[0]?.contentRect.width ?? 0);
      s > 0 && s !== this._chartWidth && (this._chartWidth = s);
    })), F(this, Z), F(this, tt, new yt()), F(this, et, new yt()), F(this, J, () => {
      this._tooltip = null;
    }), F(this, H, (e) => mt(e, m(this, i, I))), F(this, rt, (e) => new Intl.NumberFormat(void 0, { style: "currency", currency: m(this, i, I), notation: "compact", maximumFractionDigits: e < 10 ? 2 : 0 }).format(e)), F(this, nt, (e) => f(e, { compact: !0 }));
    const t = Se();
    this._days = re.includes(t.days ?? 0) ? t.days : 365, this._currency = t.currency ?? null, this._metric = ne.some((e) => e.key === t.metric) ? t.metric : "cost";
  }
  connectedCallback() {
    super.connectedCallback(), n(this, i, bt).call(this);
  }
  disconnectedCallback() {
    m(this, G).disconnect(), super.disconnectedCallback();
  }
  firstUpdated() {
    n(this, i, $t).call(this);
  }
  willUpdate(t) {
    t.has("_dark") && this.toggleAttribute("dark", this._dark);
  }
  updated() {
    const t = this.shadowRoot?.querySelector(".column-chart");
    t && t !== m(this, Z) && (m(this, G).disconnect(), m(this, G).observe(t), Be(this, Z, t));
  }
  // ---- render -------------------------------------------------------------------------
  render() {
    return l`
      <span class="theme-probe" aria-hidden="true"></span>
      ${n(this, i, Pt).call(this)}
      ${this._error ? l`<uui-box><p class="error">${this._error}</p></uui-box>` : _}
      ${this._report ? l`<div class="content ${this._loading ? "is-loading" : ""}">
            ${n(this, i, Ht).call(this)} ${n(this, i, Qt).call(this)} ${n(this, i, Gt).call(this)} ${n(this, i, Kt).call(this)}
            ${n(this, i, Zt).call(this)} ${n(this, i, te).call(this)} ${n(this, i, se).call(this)}
            ${n(this, i, oe).call(this)}
          </div>` : this._error ? _ : l`<uui-loader-bar></uui-loader-bar>`}
    `;
  }
};
G = /* @__PURE__ */ new WeakMap();
Z = /* @__PURE__ */ new WeakMap();
tt = /* @__PURE__ */ new WeakMap();
et = /* @__PURE__ */ new WeakMap();
i = /* @__PURE__ */ new WeakSet();
bt = async function() {
  const t = m(this, tt).next();
  this._loading = !0, n(this, i, $t).call(this);
  const { data: e, error: s } = await wt(this, pe(this._days), { disableNotifications: !0 });
  if (m(this, tt).isCurrent(t)) {
    if (this._loading = !1, s || !e) {
      this._error = "Could not load AI cost data.";
      return;
    }
    this._error = null, this._report = ve(e), (!this._currency || !this._report.currencies.includes(this._currency)) && (this._currency = this._report.defaultCurrency), this._dimension !== "model" && n(this, i, ft).call(this, this._dimension);
  }
};
ft = async function(t) {
  const e = `${t}:${this._days}`;
  if (this._breakdowns.has(e)) return;
  const s = m(this, et).next();
  this._breakdownLoading = !0, n(this, i, pt).call(this, e, null);
  const { data: a, error: o } = await wt(this, me(t, this._days), { disableNotifications: !0 });
  if (m(this, et).isCurrent(s)) {
    if (this._breakdownLoading = !1, o || !a) {
      n(this, i, pt).call(this, e, "Could not load the breakdown.");
      return;
    }
    this._breakdowns = new Map(this._breakdowns).set(e, a);
  }
};
pt = function(t, e) {
  const s = new Map(this._breakdownErrors);
  e ? s.set(t, e) : s.delete(t), this._breakdownErrors = s;
};
at = function() {
  Ee({ days: this._days, currency: this._currency ?? void 0, metric: this._metric });
};
$t = function() {
  const t = this.shadowRoot?.querySelector(".theme-probe");
  if (!t) return;
  const e = getComputedStyle(t).color.match(/\d+(\.\d+)?/g);
  if (!e) return;
  const [s, a, o] = e.map(Number);
  this._dark = (0.2126 * s + 0.7152 * a + 0.0722 * o) / 255 < 0.45;
};
Ut = function(t) {
  t !== this._days && (this._days = t, this._breakdowns = /* @__PURE__ */ new Map(), this._breakdownErrors = /* @__PURE__ */ new Map(), n(this, i, at).call(this), n(this, i, bt).call(this));
};
At = function(t) {
  this._currency = t.target.value, n(this, i, at).call(this);
};
Ot = function(t) {
  this._metric = t, n(this, i, at).call(this);
};
Nt = function(t) {
  this._dimension = t, t !== "model" && n(this, i, ft).call(this, t);
};
I = function() {
  return this._report ? be(this._report, this._currency) : "USD";
};
W = function(t) {
  return this._report ? Ct(this._report, t, m(this, i, I)) : t;
};
D = function(t, e = {}) {
  return mt(n(this, i, W).call(this, t), m(this, i, I), e);
};
j = function(t, { hero: e = !1 } = {}) {
  return l`<div class="tile-value ${e ? "hero" : ""}"><span style="--chars: ${t.length}">${t}</span></div>`;
};
Wt = function(t) {
  return this._metric === "tokens" ? t.totalTokens : this._metric === "requests" ? t.requests : t.cost;
};
Bt = function(t) {
  return this._metric === "cost" ? n(this, i, D).call(this, t) : this._metric === "tokens" ? `${f(t)} tokens` : `${f(t)} ${t === 1 ? "request" : "requests"}`;
};
Lt = function() {
  return this._days === 365 ? "12 months" : `${this._days} days`;
};
it = function(t, e, s) {
  const a = t.currentTarget, o = a.closest(".card");
  if (!o) return;
  const r = o.getBoundingClientRect(), h = a.getBoundingClientRect();
  this._tooltip = { card: o, x: h.left - r.left + h.width / 2, y: h.top - r.top, title: e, lines: s };
};
J = /* @__PURE__ */ new WeakMap();
V = function(t) {
  const e = this._tooltip;
  return !e || e.card.dataset.card !== t ? _ : l`<div class="tooltip" style="left:${e.x}px;top:${e.y}px" role="status">
      <div class="tooltip-title">${e.title}</div>
      ${e.lines.map((s) => l`<div class="tooltip-line"><strong>${s.value}</strong><span>${s.label}</span></div>`)}
    </div>`;
};
Pt = function() {
  const t = this._report?.currencies ?? [], e = !!this._report && !this._report.rates;
  return l`<div class="filters">
      <uui-button-group aria-label="Period">
        ${re.map(
    (s) => l`<uui-button
            compact
            look=${s === this._days ? "primary" : "secondary"}
            aria-pressed=${s === this._days}
            @click=${() => n(this, i, Ut).call(this, s)}
            >${s === 365 ? "12 months" : `${s} days`}</uui-button
          >`
  )}
      </uui-button-group>
      <label class="currency">
        <span>Currency</span>
        <select @change=${n(this, i, At)} ?disabled=${e} aria-label="Currency">
          ${Te(t, m(this, i, I)).map((s) => l`<option value=${s} ?selected=${s === m(this, i, I)}>${s}</option>`)}
        </select>
      </label>
    </div>`;
};
X = function(t, e) {
  if (!this._report?.previous || e === void 0) return _;
  const s = Ie(t, e), a = `vs previous ${n(this, i, Lt).call(this)}`;
  return s.kind === "new" ? l`<div class="tile-change">New ${a}</div>` : s.kind === "change" ? l`<div class="tile-change">${s.percent > 0 ? "+" : ""}${s.percent} % ${a}</div>` : _;
};
jt = function() {
  const t = this._report, e = Mt(t, m(this, i, I));
  return l`<uui-box class="tile">
      <div class="tile-label">This month</div>
      ${n(this, i, j).call(this, n(this, i, D).call(this, t.month.cost))}
      <div class="tile-note">Projected ${n(this, i, D).call(this, t.month.projectedCost)} by month end</div>
      ${e.kind === "set" ? l`<div
              class="budget budget-${e.level}"
              role="meter"
              aria-valuemin="0"
              aria-valuemax="100"
              aria-valuenow=${Math.round(e.spentShare * 100)}
              aria-label="Share of the monthly budget used">
              <span class="budget-fill" style="width:${Math.min(100, e.spentShare * 100)}%"></span>
            </div>
            <div class="tile-note">${f(e.spentShare * 100)} % of ${mt(e.amount, m(this, i, I))}</div>` : e.kind === "unavailable" ? l`<div class="tile-note">${e.reason}</div>` : _}
    </uui-box>`;
};
Ht = function() {
  const t = this._report, e = t.totals, s = t.previous?.totals, a = t.days.reduce((c, u) => c + u.inputTokens, 0), o = t.days.reduce((c, u) => c + u.outputTokens, 0), r = e.requests > 0 ? e.cost / e.requests : 0, h = s && s.requests > 0 && e.requests > 0 ? s.cost / s.requests : void 0;
  return l`<div class="tiles">
      ${n(this, i, jt).call(this)}
      <uui-box class="tile tile-hero">
        <div class="tile-label">Estimated cost</div>
        ${n(this, i, j).call(this, n(this, i, D).call(this, e.cost), { hero: !0 })}
        ${e.unpricedTokens > 0 ? l`<div class="tile-note">
              <uui-icon name="icon-alert"></uui-icon> ${f(e.unpricedTokens)} tokens from models without a price
            </div>` : l`<div class="tile-note">${n(this, i, Vt).call(this)}</div>`}
        ${n(this, i, X).call(this, e.cost, s?.cost)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Tokens</div>
        ${n(this, i, j).call(this, f(e.totalTokens, { compact: !0 }))}
        <div class="tile-note">${f(a, { compact: !0 })} in · ${f(o, { compact: !0 })} out</div>
        ${n(this, i, X).call(this, e.totalTokens, s?.totalTokens)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Requests</div>
        ${n(this, i, j).call(this, f(e.requests))}
        <div class="tile-note">${f(e.requests / t.days.length)} per day on average</div>
        ${n(this, i, X).call(this, e.requests, s?.requests)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Cost per request</div>
        ${n(this, i, j).call(this, e.requests > 0 ? n(this, i, D).call(this, r) : "–")}
        <div class="tile-note">Priced models only</div>
        ${n(this, i, X).call(this, r, h)}
      </uui-box>
    </div>`;
};
Vt = function() {
  const t = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" });
  return `${t.format(q(this._report.from))} – ${t.format(q(this._report.to))}`;
};
Gt = function() {
  const t = this._report.days, e = t.map((d) => n(this, i, Wt).call(this, d)), s = Fe(e.filter((d) => d > 0)), a = (q(t[0].date).getUTCDay() + 6) % 7, o = Math.ceil((a + t.length) / 7), r = ht + o * (R + A), h = dt + 7 * (R + A), c = new Intl.DateTimeFormat(void 0, { month: "short" }), u = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short", year: "numeric" }), v = new Intl.DateTimeFormat(void 0, { weekday: "short" }), b = [], w = t.map((d, $) => {
    const O = a + $, B = Math.floor(O / 7), L = O % 7, k = q(d.date);
    (k.getUTCDate() === 1 || $ === 0) && b.push({ col: B, label: c.format(k) });
    const p = e[$], C = p > 0 ? Ue(p, s) : -1;
    return M`<rect
        class="cell ${C < 0 ? "cell-empty" : `cell-${C}`}"
        x=${ht + B * (R + A)}
        y=${dt + L * (R + A)}
        width=${R}
        height=${R}
        rx="2"
        @pointerenter=${(E) => n(this, i, it).call(this, E, u.format(k), [
      { value: n(this, i, D).call(this, d.cost), label: "cost" },
      { value: f(d.totalTokens), label: "tokens" },
      { value: f(d.requests), label: d.requests === 1 ? "request" : "requests" }
    ])}
        @pointerleave=${m(this, J)}
      ></rect>`;
  }), z = b.filter((d, $) => $ === 0 || d.col - b[$ - 1].col >= 3);
  return l`<uui-box class="card" data-card="heatmap" headline="Usage calendar">
      <div slot="header-actions">
        <uui-button-group aria-label="Heatmap metric">
          ${ne.map(
    (d) => l`<uui-button
              compact
              look=${d.key === this._metric ? "primary" : "secondary"}
              aria-pressed=${d.key === this._metric}
              @click=${() => n(this, i, Ot).call(this, d.key)}
              >${d.label}</uui-button
            >`
  )}
        </uui-button-group>
      </div>
      <div class="heatmap-scroll">
        <svg
          class="heatmap"
          width=${r}
          height=${h}
          viewBox="0 0 ${r} ${h}"
          role="img"
          aria-label="Daily ${this._metric} over the selected period. The table under the cost chart lists every value.">
          ${z.map((d) => M`<text class="axis-label" x=${ht + d.col * (R + A)} y="11">${d.label}</text>`)}
          ${[0, 2, 4].map((d) => {
    const $ = v.format(new Date(Date.UTC(2024, 0, 1 + d)));
    return M`<text class="axis-label" x="0" y=${dt + d * (R + A) + R - 2}>${$}</text>`;
  })}
          ${w}
        </svg>
      </div>
      <div class="scale-legend" aria-hidden="true">
        <span class="swatch cell-empty"></span><span>No usage</span>
        <span class="scale-gap"></span>
        <span>Less</span>
        ${[0, 1, 2, 3].map((d) => l`<span class="swatch cell-${d}"></span>`)}
        <span>More</span>
        ${s.length ? l`<span class="scale-range">up to ${n(this, i, Bt).call(this, Math.max(...e))} a day</span>` : _}
      </div>
      ${n(this, i, V).call(this, "heatmap")}
    </uui-box>`;
};
ot = function() {
  const t = this._report.days.length > 92;
  return { buckets: t ? qe(this._report.days) : Me(this._report.days), weekly: t };
};
H = /* @__PURE__ */ new WeakMap();
rt = /* @__PURE__ */ new WeakMap();
nt = /* @__PURE__ */ new WeakMap();
Xt = function(t) {
  const e = new Set(this._tables);
  e.has(t) ? e.delete(t) : e.add(t), this._tables = e;
};
Q = function(t) {
  return l`<uui-button compact look="secondary" @click=${() => n(this, i, Xt).call(this, t)}>
      ${this._tables.has(t) ? "Show chart" : "Show as table"}
    </uui-button>`;
};
Jt = function(t) {
  if (this._costView === "total")
    return [{ id: "cost", label: "Cost", color: "var(--viz-cat-1)", values: t.map((a) => n(this, i, W).call(this, a.cost)) }];
  const { ids: e, values: s } = $e(this._report, t);
  return e.map((a, o) => ({ id: a, label: a, color: K(a, o), values: s[o].map((r) => n(this, i, W).call(this, r)) }));
};
lt = function(t) {
  return t.length < 2 ? _ : l`<div class="legend">
      ${t.map(
    (e) => l`<span class="legend-item"
          >${e.icon ? l`<uui-icon name=${e.icon} style="color:${e.color}"></uui-icon>` : l`<span class="legend-swatch" style="background:${e.color}"></span>`}${e.label}</span
        >`
  )}
    </div>`;
};
ct = function(t, e, s, a, o, r) {
  const h = t.map((k, p) => s.reduce((C, E) => C + E.values[p], 0)), c = Math.max(0, ...h), u = Et(c), v = u[u.length - 1] || 1, b = Math.max(200, this._chartWidth - g), w = b / t.length, z = Math.max(1, Math.min(Le, w - A)), d = (k) => k / v * S, $ = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short" }), O = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), B = Math.max(1, Math.ceil(t.length / Math.max(2, Math.floor(b / 90)))), L = xe(ge(s.map((k) => k.values)), d, S, A);
  return l`<div class="column-chart">
      <svg width=${g + b} height=${st + S + le} role="img" aria-label="${r}. Use “Show as table” for the values.">
        <g transform="translate(0 ${st})">
          ${u.map(
    (k) => M`
              <line class="grid" x1=${g} x2=${g + b} y1=${S - d(k)} y2=${S - d(k)}></line>
              <text class="axis-label tick" x=${g - 8} y=${S - d(k) + 4}>${o(k)}</text>`
  )}
          ${t.map((k, p) => {
    const C = g + p * w + (w - z) / 2, E = e ? `Week of ${$.format(q(k.start))}` : O.format(q(k.start)), Y = s.map((N) => ({ value: a(N.values[p]), label: N.label.toLowerCase() }));
    return s.length > 1 && Y.push({ value: a(h[p]), label: "total" }), M`
              <rect class="hit" x=${g + p * w} y="0" width=${w} height=${S}
                @pointerenter=${(N) => n(this, i, it).call(this, N, E, Y)}
                @pointerleave=${m(this, J)}></rect>
              ${s.map((N, ce) => {
      const P = L[ce][p];
      return P ? P.kind === "rounded" ? M`<path class="bar" style="fill:${N.color}" d=${Ae(C, P.base, z, P.height)}></path>` : M`<rect class="bar" style="fill:${N.color}" x=${C} y=${P.y} width=${z} height=${P.height}></rect>` : _;
    })}
              ${p % B === 0 ? M`<text class="axis-label" x=${C + z / 2} y=${S + 18} text-anchor="middle">${$.format(q(k.start))}</text>` : _}`;
  })}
          <line class="baseline" x1=${g} x2=${g + b} y1=${S} y2=${S}></line>
        </g>
      </svg>
    </div>`;
};
ut = function(t, e, s, a, o) {
  const r = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }), h = t.map((c, u) => ({ b: c, i: u })).filter(({ i: c }) => s.some((u) => u.values[c] > 0)).reverse();
  return h.length ? l`<div class="table-scroll">
      <uui-table aria-label=${o}>
        <uui-table-head>
          <uui-table-head-cell>${e ? "Week of" : "Date"}</uui-table-head-cell>
          ${s.map((c) => l`<uui-table-head-cell class="num">${c.label}</uui-table-head-cell>`)}
          ${s.length > 1 ? l`<uui-table-head-cell class="num">Total</uui-table-head-cell>` : _}
        </uui-table-head>
        ${h.map(
    ({ b: c, i: u }) => l`<uui-table-row>
            <uui-table-cell>${r.format(q(c.start))}</uui-table-cell>
            ${s.map((v) => l`<uui-table-cell class="num">${a(v.values[u])}</uui-table-cell>`)}
            ${s.length > 1 ? l`<uui-table-cell class="num">${a(s.reduce((v, b) => v + b.values[u], 0))}</uui-table-cell>` : _}
          </uui-table-row>`
  )}
      </uui-table>
    </div>` : l`<p class="empty">No AI usage in this period.</p>`;
};
Qt = function() {
  const t = this._report, e = q(t.month.to), s = new Date(Date.UTC(e.getUTCFullYear(), e.getUTCMonth() + 1, 0)).getUTCDate(), a = Mt(t, m(this, i, I)), o = a.kind === "set" ? a.amount : void 0, r = fe(t.month, s), h = (p) => ({ day: p.day, value: n(this, i, W).call(this, p.value) }), c = r.actual.map(h), u = r.projection.map(h), v = Math.max(n(this, i, W).call(this, r.max), o ?? 0), b = Et(v), w = b[b.length - 1] || 1, z = Math.max(200, this._chartWidth - g - _t), d = (p) => we(p, s, g, z), $ = (p) => U - p / w * U, O = (p) => p.map((C, E) => `${E ? "L" : "M"}${d(C.day)},${$(C.value)}`).join(" "), B = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), L = z / Math.max(1, s - 1), k = this._tables.has("month");
  return l`<uui-box class="card" data-card="month" headline="This month">
      <div slot="header-actions" class="header-actions">${n(this, i, Q).call(this, "month")}</div>
      ${k ? n(this, i, Yt).call(this) : l`<div class="legend">
              <span class="legend-item"><span class="legend-line"></span>Cost so far</span>
              <span class="legend-item"><span class="legend-line dashed"></span>Projected ${n(this, i, D).call(this, t.month.projectedCost)}</span>
              ${o !== void 0 ? l`<span class="legend-item"><span class="legend-line budget"></span>Budget ${m(this, H).call(this, o)}</span>` : _}
            </div>
            <div class="column-chart">
              <svg width=${g + z + _t} height=${st + U + le} role="img"
                aria-label="Cumulative cost this month with the projection to month end. Use “Show as table” for the values.">
                <g transform="translate(0 ${st})">
                  ${b.map(
    (p) => M`
                      <line class="grid" x1=${g} x2=${g + z} y1=${$(p)} y2=${$(p)}></line>
                      <text class="axis-label tick" x=${g - 8} y=${$(p) + 4}>${m(this, rt).call(this, p)}</text>`
  )}
                  ${o !== void 0 ? M`<line class="budget-line" x1=${g} x2=${g + z} y1=${$(o)} y2=${$(o)}></line>
                        <text class="axis-label" x=${g + z + 6} y=${$(o) + 4}>Budget</text>` : _}
                  ${this._monthHover !== null ? M`<line class="crosshair" x1=${d(this._monthHover)} x2=${d(this._monthHover)} y1="0" y2=${U}></line>` : _}
                  <path class="line projection" d=${O(u)}></path>
                  <path class="line" d=${O(c)}></path>
                  ${c.length ? M`<circle class="dot" cx=${d(c[c.length - 1].day)} cy=${$(c[c.length - 1].value)} r="4"></circle>` : _}
                  ${[1, 8, 15, 22, s].map(
    (p) => M`<text class="axis-label" x=${d(p)} y=${U + 18} text-anchor="middle">${p}</text>`
  )}
                  ${c.map((p, C) => {
    const E = q(t.month.days[C].date);
    return M`<rect class="hit" x=${d(p.day) - L / 2} y="0" width=${L} height=${U}
                      @pointerenter=${(Y) => {
      this._monthHover = p.day, n(this, i, it).call(this, Y, B.format(E), [
        { value: m(this, H).call(this, p.value), label: "so far" },
        { value: n(this, i, D).call(this, t.month.days[C].cost), label: "that day" }
      ]);
    }}
                      @pointerleave=${() => {
      this._monthHover = null, m(this, J).call(this);
    }}></rect>`;
  })}
                  <line class="baseline" x1=${g} x2=${g + z} y1=${U} y2=${U}></line>
                </g>
              </svg>
            </div>`}
      ${n(this, i, V).call(this, "month")}
    </uui-box>`;
};
Yt = function() {
  const t = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" });
  let e = 0;
  const s = this._report.month.days.map((a) => ({ d: a, total: e += a.cost })).reverse();
  return l`<div class="table-scroll">
      <uui-table aria-label="Cost this month per day">
        <uui-table-head>
          <uui-table-head-cell>Date</uui-table-head-cell>
          <uui-table-head-cell class="num">Cost</uui-table-head-cell>
          <uui-table-head-cell class="num">So far</uui-table-head-cell>
        </uui-table-head>
        ${s.map(
    ({ d: a, total: o }) => l`<uui-table-row>
            <uui-table-cell>${t.format(q(a.date))}</uui-table-cell>
            <uui-table-cell class="num">${n(this, i, D).call(this, a.cost)}</uui-table-cell>
            <uui-table-cell class="num">${n(this, i, D).call(this, o)}</uui-table-cell>
          </uui-table-row>`
  )}
      </uui-table>
    </div>`;
};
Kt = function() {
  const { buckets: t, weekly: e } = n(this, i, ot).call(this), s = n(this, i, Jt).call(this, t), a = Ce(s.every((c) => c.values.every((u) => u === 0)), this._report.totals.unpricedTokens), o = `${e ? "Weekly" : "Daily"} cost`, r = this._tables.has("columns"), h = this._report.totals.unpricedTokens > 0;
  return l`<uui-box class="card" data-card="columns" headline=${o}>
      <div slot="header-actions" class="header-actions">
        <uui-button-group aria-label="Cost per">
          ${["total", "model"].map(
    (c) => l`<uui-button compact look=${c === this._costView ? "primary" : "secondary"} aria-pressed=${c === this._costView}
              @click=${() => this._costView = c}>${c === "total" ? "Total" : "By model"}</uui-button>`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" @click=${() => n(this, i, ae).call(this, t, e, s)}>Download CSV</uui-button>
        ${n(this, i, Q).call(this, "columns")}
      </div>
      ${n(this, i, lt).call(this, s)}
      ${a ? l`<p class="empty">${a}</p>` : r ? n(this, i, ut).call(this, t, e, s, m(this, H), `${o} per ${this._costView === "total" ? "period" : "model"}`) : n(this, i, ct).call(this, t, e, s, m(this, H), m(this, rt), `${o}${this._costView === "model" ? " by model" : ""}`)}
      ${this._costView === "model" && h ? l`<p class="chart-note">Models without a configured price aren’t included.</p>` : _}
      ${n(this, i, V).call(this, "columns")}
    </uui-box>`;
};
Zt = function() {
  const { buckets: t, weekly: e } = n(this, i, ot).call(this), s = _e(t), a = (r) => f(r), o = `Tokens by type${e ? " per week" : ""}`;
  return l`<uui-box class="card" data-card="tokens" headline=${o}>
      <div slot="header-actions" class="header-actions">${n(this, i, Q).call(this, "tokens")}</div>
      <div class="figures">
        <div><div class="tile-label">Caching saved</div><div class="figure">${n(this, i, D).call(this, this._report.cachingSavings)}</div></div>
      </div>
      ${n(this, i, lt).call(this, s)}
      ${this._tables.has("tokens") ? n(this, i, ut).call(this, t, e, s, a, o) : s.every((r) => r.values.every((h) => h === 0)) ? l`<p class="empty">No AI usage in this period.</p>` : n(this, i, ct).call(this, t, e, s, a, m(this, nt), o)}
      ${n(this, i, V).call(this, "tokens")}
    </uui-box>`;
};
te = function() {
  const t = this._report, { buckets: e, weekly: s } = n(this, i, ot).call(this), a = t.days.reduce((u, v) => u + v.succeededRequests, 0), o = t.days.reduce((u, v) => u + v.failedRequests, 0), r = [
    { id: "succeeded", label: "Succeeded", color: "var(--viz-cat-1)", values: e.map((u) => u.succeededRequests) },
    { id: "failed", label: "Failed", color: "var(--viz-critical)", icon: "icon-alert", values: e.map((u) => u.failedRequests) }
  ], h = (u) => f(u), c = `Requests${s ? " per week" : ""}`;
  return l`<uui-box class="card" data-card="reliability" headline="Reliability">
      <div slot="header-actions" class="header-actions">${n(this, i, Q).call(this, "reliability")}</div>
      <div class="figures">
        <div><div class="tile-label">Failed requests</div><div class="figure">${f(o)} <span class="figure-note">(${f(ye(a, o) * 100)} %)</span></div></div>
        <div><div class="tile-label">Average response time</div><div class="figure">${t.averageDurationMs > 0 ? ke(t.averageDurationMs) : "–"}</div></div>
      </div>
      ${n(this, i, lt).call(this, r)}
      ${this._tables.has("reliability") ? n(this, i, ut).call(this, e, s, r, h, c) : a + o === 0 ? l`<p class="empty">No AI usage in this period.</p>` : n(this, i, ct).call(this, e, s, r, h, m(this, nt), c)}
      ${n(this, i, V).call(this, "reliability")}
    </uui-box>`;
};
ee = function() {
  if (this._dimension === "model")
    return this._report.models.map((e) => ({ ...e, name: e.modelId }));
  const t = ze(`${this._dimension}:${this._days}`, this._breakdowns, this._breakdownErrors);
  return t.kind === "rows" ? t.breakdown.rows : t.kind === "error" ? { error: t.message } : void 0;
};
se = function() {
  const t = kt.find((r) => r.key === this._dimension).label, e = n(this, i, ee).call(this), s = Array.isArray(e) ? e : void 0, a = this._report.totals.cost, o = this._dimension === "model" ? "Add this model to Esatto:AICosts:PricesPerMillionTokens in appsettings" : "Some of this usage is from models without a configured price";
  return l`<uui-box class="card" headline="Cost by">
      <div slot="header-actions" class="header-actions">
        <uui-button-group aria-label="Group cost by">
          ${kt.map(
    (r) => l`<uui-button
              compact
              look=${r.key === this._dimension ? "primary" : "secondary"}
              aria-pressed=${r.key === this._dimension}
              @click=${() => n(this, i, Nt).call(this, r.key)}
              >${r.label}</uui-button
            >`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" ?disabled=${!s?.length} @click=${() => n(this, i, ie).call(this, t, s ?? [])}
          >Download CSV</uui-button
        >
      </div>
      ${e && "error" in e ? l`<p class="error">${e.error}</p>` : s ? s.length === 0 ? l`<p class="empty">No AI usage in this period.</p>` : l`<div class="table-scroll">
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
                  ${s.map(
    (r) => l`<uui-table-row>
                      <uui-table-cell>${r.name}</uui-table-cell>
                      <uui-table-cell class="num">${f(r.requests)}</uui-table-cell>
                      <uui-table-cell class="num">${f(r.inputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${f(r.cachedInputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${f(r.outputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">
                        ${r.cost === null ? l`<uui-tag look="secondary" title=${o}>No price</uui-tag>` : n(this, i, D).call(this, r.cost)}
                      </uui-table-cell>
                      <uui-table-cell class="num">
                        ${r.cost !== null && a > 0 ? l`<span class="share"
                              ><span class="share-track"
                                ><span class="share-fill" style="width:${r.cost / a * 100}%"></span></span
                              >${f(r.cost / a * 100)}%</span
                            >` : "–"}
                      </uui-table-cell>
                    </uui-table-row>`
  )}
                </uui-table>
              </div>` : l`<uui-loader-bar></uui-loader-bar>`}
      ${this._breakdownLoading && s ? l`<uui-loader-bar></uui-loader-bar>` : _}
    </uui-box>`;
};
gt = function(t) {
  return Number(n(this, i, W).call(this, t).toFixed(4));
};
ae = function(t, e, s) {
  const a = this._report, o = this._costView === "model", r = [
    e ? "Week of" : "Date",
    "Requests",
    "Input tokens",
    "Cached input tokens",
    "Output tokens",
    "Total tokens",
    `Cost (${m(this, i, I)})`,
    "Unpriced tokens",
    ...o ? s.map((c) => `${c.label} (${m(this, i, I)})`) : []
  ], h = t.map((c, u) => [
    c.start,
    c.requests,
    c.inputTokens,
    c.cachedInputTokens,
    c.outputTokens,
    c.totalTokens,
    n(this, i, gt).call(this, c.cost),
    c.unpricedTokens,
    ...o ? s.map((v) => Number(v.values[u].toFixed(4))) : []
  ]);
  Dt(`ai-costs-${a.from}-${a.to}${o ? "-by-model" : ""}.csv`, It(r, h));
};
ie = function(t, e) {
  const s = this._report, a = [t, "Requests", "Input tokens", "Cached input tokens", "Output tokens", "Total tokens", `Cost (${m(this, i, I)})`], o = e.map((r) => [r.name, r.requests, r.inputTokens, r.cachedInputTokens, r.outputTokens, r.totalTokens, r.cost === null ? "" : n(this, i, gt).call(this, r.cost)]);
  Dt(`ai-costs-${s.from}-${s.to}-${this._dimension}.csv`, It(a, o));
};
oe = function() {
  const t = this._report, e = t.rates ? new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }).format(q(t.rates.date)) : null;
  return l`<p class="footnote">
      Estimates: tokens × list prices per model${t.pricesSource ? l` (${t.pricesSource})` : _}, in ${t.baseCurrency}.
      Cached input tokens are billed at the cache-read price. Excludes taxes, discounts and provider surcharges.
      ${t.rates ? l`Converted with ${t.rates.source} reference rates of ${e}.` : l`<strong>Exchange rates are unavailable right now — amounts are shown in ${t.baseCurrency}.</strong>`}
      Times are UTC.
    </p>`;
};
y.styles = [Oe];
x([
  T()
], y.prototype, "_report", 2);
x([
  T()
], y.prototype, "_loading", 2);
x([
  T()
], y.prototype, "_error", 2);
x([
  T()
], y.prototype, "_days", 2);
x([
  T()
], y.prototype, "_currency", 2);
x([
  T()
], y.prototype, "_metric", 2);
x([
  T()
], y.prototype, "_tables", 2);
x([
  T()
], y.prototype, "_costView", 2);
x([
  T()
], y.prototype, "_tooltip", 2);
x([
  T()
], y.prototype, "_dark", 2);
x([
  T()
], y.prototype, "_chartWidth", 2);
x([
  T()
], y.prototype, "_dimension", 2);
x([
  T()
], y.prototype, "_breakdowns", 2);
x([
  T()
], y.prototype, "_breakdownLoading", 2);
x([
  T()
], y.prototype, "_breakdownErrors", 2);
x([
  T()
], y.prototype, "_monthHover", 2);
y = x([
  he("esatto-ai-costs-view")
], y);
const Ge = y;
export {
  y as EsattoAICostsViewElement,
  Ge as default
};
//# sourceMappingURL=ai-costs-view.js.map
