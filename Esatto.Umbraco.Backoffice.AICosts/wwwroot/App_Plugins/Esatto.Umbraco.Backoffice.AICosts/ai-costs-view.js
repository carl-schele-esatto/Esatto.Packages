import { css as fe, state as T, customElement as be, nothing as _, html as l, svg as M } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as ge } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as Tt } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as Ct } from "@umbraco-cms/backoffice/http-client";
const Mt = "/umbraco/management/api/v1/esatto/ai-costs", qt = () => [{ scheme: "bearer", type: "http" }], ye = (t) => Ct.get({ url: `${Mt}/report?days=${t}`, security: qt() }), $e = (t, e) => Ct.get({ url: `${Mt}/breakdown?dimension=${t}&days=${e}`, security: qt() });
function ke(t) {
  if (!t.rates) return t;
  const e = Object.fromEntries(
    Object.entries(t.rates.perBaseUnit).map(([s, a]) => [s.toUpperCase(), a])
  );
  return { ...t, rates: { ...t.rates, perBaseUnit: e } };
}
function _e(t, e) {
  return e && (e === t.baseCurrency || t.rates?.perBaseUnit[e]) ? e : t.baseCurrency;
}
function It(t, e, s) {
  return s === t.baseCurrency ? e : e * (t.rates?.perBaseUnit[s] ?? 1);
}
function bt(t, e, { compact: s = !1 } = {}) {
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
function Rt(t, e) {
  const s = t.month.budget;
  if (!s) return { kind: "none" };
  const a = s.currency === t.baseCurrency ? 1 : t.rates?.perBaseUnit[s.currency];
  if (!a)
    return { kind: "unavailable", reason: `Exchange rates are unavailable, so the ${s.currency} budget can’t be compared right now.` };
  const r = s.amount / a, n = r > 0 ? t.month.cost / r : 0, h = t.month.cost > r ? "over" : n >= 0.8 || t.month.projectedCost > r ? "warning" : "ok";
  return { kind: "set", amount: It(t, r, e), spentShare: n, level: h };
}
function we(t, e) {
  let s = 0;
  const a = t.days.map((h, c) => ({ day: c + 1, value: s += h.cost })), r = a[a.length - 1] ?? { day: 0, value: 0 }, n = [r, { day: e, value: t.projectedCost }];
  return { actual: a, projection: n, max: Math.max(t.projectedCost, r.value) };
}
function xe(t, e, s = 7) {
  const a = (u) => e.map((v) => t.days.reduce((b, w, z) => w.date >= v.start && w.date <= v.end ? b + u[z] : b, 0)), r = t.modelDays.map((u) => ({ id: u.modelId, values: a(u.costs), total: u.costs.reduce((v, b) => v + b, 0) })).sort((u, v) => v.total - u.total);
  if (r.length <= s) return { ids: r.map((u) => u.id), values: r.map((u) => u.values) };
  const n = r.slice(0, s - 1), h = r.slice(s - 1), c = e.map((u, v) => h.reduce((b, w) => b + w.values[v], 0));
  return { ids: [...n.map((u) => u.id), "Other"], values: [...n.map((u) => u.values), c] };
}
function ze(t) {
  const e = new Array(t[0]?.length ?? 0).fill(0);
  return t.map(
    (s) => s.map((a, r) => {
      const n = e[r];
      return e[r] += a, { y0: n, y1: e[r] };
    })
  );
}
function Te(t, e) {
  const s = t + e;
  return s === 0 ? 0 : e / s;
}
function Ce(t) {
  return t < 1e3 ? `${Math.round(t)} ms` : `${new Intl.NumberFormat(void 0, { maximumFractionDigits: 1 }).format(t / 1e3)} s`;
}
function K(t, e) {
  return t === "Other" ? "var(--viz-other)" : `var(--viz-cat-${e + 1})`;
}
function Me(t) {
  return [
    { id: "uncached", label: "Uncached input", color: K("uncached", 0), values: t.map((e) => Math.max(0, e.inputTokens - e.cachedInputTokens)) },
    { id: "cached", label: "Cached input", color: K("cached", 1), values: t.map((e) => e.cachedInputTokens) },
    { id: "output", label: "Output", color: K("output", 2), values: t.map((e) => e.outputTokens) }
  ];
}
function qe(t, e, s, a) {
  return s + (t - 1) / Math.max(1, e - 1) * a;
}
function Ie(t, e, s, a) {
  const r = t[0]?.length ?? 0, n = Array.from({ length: r }, (h, c) => t.map((u) => u[c].y1 > u[c].y0).lastIndexOf(!0));
  return t.map(
    (h, c) => h.map(({ y0: u, y1: v }, b) => {
      const w = e(v) - e(u);
      return w <= 0 ? null : c === n[b] ? { kind: "rounded", base: s - e(u), height: w } : { kind: "rect", y: s - e(v) + a, height: Math.max(0, w - a) };
    })
  );
}
function Re(t, e, s) {
  const a = e.get(t);
  if (a) return { kind: "rows", breakdown: a };
  const r = s.get(t);
  return r ? { kind: "error", message: r } : { kind: "loading" };
}
function De(t, e) {
  return t.includes(e) ? t : [...t, e];
}
function Se(t, e) {
  return t ? e > 0 ? "No cost to show — the models used in this period have no configured price." : "No AI usage in this period." : null;
}
function q(t) {
  const [e, s, a] = t.split("-").map(Number);
  return new Date(Date.UTC(e, s - 1, a));
}
const Dt = (t) => ({
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
}), Ee = (t) => t.map(Dt);
function Fe(t) {
  const e = [];
  for (const s of t) {
    const a = e[e.length - 1];
    if (!a || q(s.date).getUTCDay() === 1) {
      e.push(Dt(s));
      continue;
    }
    a.end = s.date, a.requests += s.requests, a.inputTokens += s.inputTokens, a.outputTokens += s.outputTokens, a.cachedInputTokens += s.cachedInputTokens, a.totalTokens += s.totalTokens, a.cost += s.cost, a.unpricedTokens += s.unpricedTokens, a.succeededRequests += s.succeededRequests, a.failedRequests += s.failedRequests;
  }
  return e;
}
function Ae(t, e) {
  return e == null ? { kind: "none" } : e === 0 ? t === 0 ? { kind: "change", percent: 0 } : { kind: "new" } : { kind: "change", percent: Math.round((t - e) / e * 100) };
}
function Ue(t) {
  return new Intl.NumberFormat(t).formatToParts(1.5).find((s) => s.type === "decimal")?.value === "," ? { field: ";", decimal: "," } : { field: ",", decimal: "." };
}
const Ne = /^[=+\-@\t\r]/;
function St(t, e, s) {
  const { field: a, decimal: r } = Ue(s), n = (h) => {
    const c = typeof h == "number" ? String(h).replace(".", r) : Ne.test(h) ? `'${h}` : h;
    return /["\r\n]/.test(c) || c.includes(a) ? `"${c.replace(/"/g, '""')}"` : c;
  };
  return "\uFEFF" + [t, ...e].map((h) => h.map(n).join(a)).join(`\r
`);
}
function Et(t, e) {
  const s = URL.createObjectURL(new Blob([e], { type: "text/csv;charset=utf-8" }));
  Object.assign(document.createElement("a"), { href: s, download: t }).click(), URL.revokeObjectURL(s);
}
class wt {
  #t = 0;
  next() {
    return ++this.#t;
  }
  isCurrent(e) {
    return e === this.#t;
  }
}
const Ft = "esatto.aiCosts.prefs", At = () => {
  try {
    return globalThis.localStorage;
  } catch {
    return;
  }
};
function Oe(t = At()) {
  try {
    const e = JSON.parse(t?.getItem(Ft) ?? "{}");
    return e && typeof e == "object" && !Array.isArray(e) ? e : {};
  } catch {
    return {};
  }
}
function Le(t, e = At()) {
  try {
    e?.setItem(Ft, JSON.stringify(t));
  } catch {
  }
}
function Pe(t) {
  if (!t.length) return [];
  const e = [...t].sort((s, a) => s - a);
  return [0.25, 0.5, 0.75].map((s) => e[Math.min(e.length - 1, Math.floor(s * e.length))]);
}
function We(t, e) {
  for (let s = 0; s < e.length; s++) if (t <= e[s]) return s;
  return 3;
}
function Ut(t) {
  if (t <= 0) return [0, 1];
  const e = t / 4, s = 10 ** Math.floor(Math.log10(e)), a = [1, 2, 2.5, 5, 10].map((n) => n * s).find((n) => n >= e) ?? 10 * s, r = [];
  for (let n = 0; n <= t + a * 0.999; n += a) r.push(Number(n.toFixed(10)));
  return r;
}
function Be(t, e, s, a) {
  const r = Math.min(4, s / 2, a), n = e - a;
  return `M${t},${e} V${n + r} Q${t},${n} ${t + r},${n} H${t + s - r} Q${t + s},${n} ${t + s},${n + r} V${e} Z`;
}
const D = 12, je = fe`
    /* The tab is the container the narrow layouts below ask about (@container ai-costs). */
    :host {
        display: block;
        container: ai-costs / inline-size;
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
    /* minmax(0, 1fr): a card is as wide as the tab, not as its widest content (the calendar and the
       tables scroll inside their cards instead). */
    .content {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
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
        width: ${D}px;
        height: ${D}px;
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
    /* Centred above its bar or cell; --tip-shift (#keepTipInCard) moves it back inside the card at an edge. */
    .tooltip {
        position: absolute;
        transform: translate(calc(-50% + var(--tip-shift, 0px)), calc(-100% - 8px));
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

    /* Narrow tab (phone, narrow window). A card's buttons (#cardActions) move from the header, which can't
       wrap, to the top of the card; "Cost by" shows its rows as a list instead of a 7-column table. */
    .card-actions-body,
    .cost-list {
        display: none;
    }
    @media (max-width: 600px) {
        :host {
            padding: var(--uui-size-space-4);
        }
    }
    @container ai-costs (width < 600px) {
        .card-actions-header {
            display: none;
        }
        .card-actions-body {
            display: flex;
            margin-bottom: var(--uui-size-space-4);
        }
    }
    @container ai-costs (width < 760px) {
        .cost-table {
            display: none;
        }
        .cost-list {
            display: block;
            list-style: none;
            margin: 0;
            padding: 0;
        }
        .cost-list li {
            display: grid;
            grid-template-columns: minmax(0, 1fr) auto;
            gap: var(--uui-size-space-1) var(--uui-size-space-4);
            align-items: baseline;
            padding: var(--uui-size-space-4) 0;
            border-bottom: 1px solid var(--uui-color-divider);
        }
        .cost-list li:last-child {
            border-bottom: 0;
        }
        .cost-list-name {
            font-weight: 600;
            overflow-wrap: anywhere;
        }
        .cost-list-cost {
            text-align: right;
            font-weight: 600;
            font-variant-numeric: tabular-nums;
        }
        .cost-list-tokens {
            color: var(--uui-color-text-alt);
            font-size: var(--uui-type-small-size);
            font-variant-numeric: tabular-nums;
        }
        .cost-list-share {
            font-size: var(--uui-type-small-size);
            font-variant-numeric: tabular-nums;
        }
    }
`;
var He = Object.defineProperty, Ve = Object.getOwnPropertyDescriptor, Nt = (t) => {
  throw TypeError(t);
}, x = (t, e, s, a) => {
  for (var r = a > 1 ? void 0 : a ? Ve(e, s) : e, n = t.length - 1, h; n >= 0; n--)
    (h = t[n]) && (r = (a ? h(e, s, r) : h(r)) || r);
  return a && r && He(e, s, r), r;
}, gt = (t, e, s) => e.has(t) || Nt("Cannot " + s), m = (t, e, s) => (gt(t, e, "read from private field"), s ? s.call(t) : e.get(t)), F = (t, e, s) => e.has(t) ? Nt("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, s), Ge = (t, e, s, a) => (gt(t, e, "write to private field"), e.set(t, s), s), o = (t, e, s) => (gt(t, e, "access private method"), s), G, Z, tt, et, i, yt, $t, mt, at, kt, Ot, Lt, Pt, Wt, I, L, R, j, Bt, jt, Ht, it, Vt, Gt, J, V, Xt, X, Jt, Qt, Yt, Kt, ot, H, rt, nt, Zt, lt, vt, ft, Q, te, ct, ut, ht, ee, se, ae, ie, oe, re, ne, _t, le, ce, ue;
const he = [30, 90, 180, 365], de = [
  { key: "cost", label: "Cost" },
  { key: "tokens", label: "Tokens" },
  { key: "requests", label: "Requests" }
], xt = [
  { key: "model", label: "Model" },
  { key: "profile", label: "Profile" },
  { key: "user", label: "User" },
  { key: "provider", label: "Provider" }
], U = 2, dt = 30, pt = 18, st = 10, S = 220, A = 200, zt = 56, y = 64, pe = 120, me = 26, Xe = 24;
let $ = class extends ge {
  constructor() {
    super(), F(this, i), this._loading = !1, this._error = null, this._tables = /* @__PURE__ */ new Set(), this._costView = "total", this._tooltip = null, this._dark = !1, this._chartWidth = 900, this._dimension = "model", this._breakdowns = /* @__PURE__ */ new Map(), this._breakdownLoading = !1, this._breakdownErrors = /* @__PURE__ */ new Map(), this._monthHover = null, F(this, G, new ResizeObserver((e) => {
      const s = Math.floor(e[0]?.contentRect.width ?? 0);
      s > 0 && s !== this._chartWidth && (this._chartWidth = s);
    })), F(this, Z), F(this, tt, new wt()), F(this, et, new wt()), F(this, J, () => {
      this._tooltip = null;
    }), F(this, H, (e) => bt(e, m(this, i, I))), F(this, rt, (e) => new Intl.NumberFormat(void 0, { style: "currency", currency: m(this, i, I), notation: "compact", maximumFractionDigits: e < 10 ? 2 : 0 }).format(e)), F(this, nt, (e) => f(e, { compact: !0 }));
    const t = Oe();
    this._days = he.includes(t.days ?? 0) ? t.days : 365, this._currency = t.currency ?? null, this._metric = de.some((e) => e.key === t.metric) ? t.metric : "cost";
  }
  connectedCallback() {
    super.connectedCallback(), o(this, i, yt).call(this);
  }
  disconnectedCallback() {
    m(this, G).disconnect(), super.disconnectedCallback();
  }
  firstUpdated() {
    o(this, i, kt).call(this);
  }
  willUpdate(t) {
    t.has("_dark") && this.toggleAttribute("dark", this._dark);
  }
  updated(t) {
    t.has("_report") && o(this, i, Vt).call(this), t.has("_tooltip") && o(this, i, Gt).call(this);
    const e = this.shadowRoot?.querySelector(".column-chart");
    e && e !== m(this, Z) && (m(this, G).disconnect(), m(this, G).observe(e), Ge(this, Z, e));
  }
  // ---- render -------------------------------------------------------------------------
  render() {
    return l`
      <span class="theme-probe" aria-hidden="true"></span>
      ${o(this, i, Xt).call(this)}
      ${this._error ? l`<uui-box><p class="error">${this._error}</p></uui-box>` : _}
      ${this._report ? l`<div class="content ${this._loading ? "is-loading" : ""}">
            ${o(this, i, Qt).call(this)} ${o(this, i, ee).call(this)} ${o(this, i, Kt).call(this)} ${o(this, i, ae).call(this)}
            ${o(this, i, ie).call(this)} ${o(this, i, oe).call(this)} ${o(this, i, ne).call(this)}
            ${o(this, i, ue).call(this)}
          </div>` : this._error ? _ : l`<uui-loader-bar></uui-loader-bar>`}
    `;
  }
};
G = /* @__PURE__ */ new WeakMap();
Z = /* @__PURE__ */ new WeakMap();
tt = /* @__PURE__ */ new WeakMap();
et = /* @__PURE__ */ new WeakMap();
i = /* @__PURE__ */ new WeakSet();
yt = async function() {
  const t = m(this, tt).next();
  this._loading = !0, o(this, i, kt).call(this);
  const { data: e, error: s } = await Tt(this, ye(this._days), { disableNotifications: !0 });
  if (m(this, tt).isCurrent(t)) {
    if (this._loading = !1, s || !e) {
      this._error = "Could not load AI cost data.";
      return;
    }
    this._error = null, this._report = ke(e), (!this._currency || !this._report.currencies.includes(this._currency)) && (this._currency = this._report.defaultCurrency), this._dimension !== "model" && o(this, i, $t).call(this, this._dimension);
  }
};
$t = async function(t) {
  const e = `${t}:${this._days}`;
  if (this._breakdowns.has(e)) return;
  const s = m(this, et).next();
  this._breakdownLoading = !0, o(this, i, mt).call(this, e, null);
  const { data: a, error: r } = await Tt(this, $e(t, this._days), { disableNotifications: !0 });
  if (m(this, et).isCurrent(s)) {
    if (this._breakdownLoading = !1, r || !a) {
      o(this, i, mt).call(this, e, "Could not load the breakdown.");
      return;
    }
    this._breakdowns = new Map(this._breakdowns).set(e, a);
  }
};
mt = function(t, e) {
  const s = new Map(this._breakdownErrors);
  e ? s.set(t, e) : s.delete(t), this._breakdownErrors = s;
};
at = function() {
  Le({ days: this._days, currency: this._currency ?? void 0, metric: this._metric });
};
kt = function() {
  const t = this.shadowRoot?.querySelector(".theme-probe");
  if (!t) return;
  const e = getComputedStyle(t).color.match(/\d+(\.\d+)?/g);
  if (!e) return;
  const [s, a, r] = e.map(Number);
  this._dark = (0.2126 * s + 0.7152 * a + 0.0722 * r) / 255 < 0.45;
};
Ot = function(t) {
  t !== this._days && (this._days = t, this._breakdowns = /* @__PURE__ */ new Map(), this._breakdownErrors = /* @__PURE__ */ new Map(), o(this, i, at).call(this), o(this, i, yt).call(this));
};
Lt = function(t) {
  this._currency = t.target.value, o(this, i, at).call(this);
};
Pt = function(t) {
  this._metric = t, o(this, i, at).call(this);
};
Wt = function(t) {
  this._dimension = t, t !== "model" && o(this, i, $t).call(this, t);
};
I = function() {
  return this._report ? _e(this._report, this._currency) : "USD";
};
L = function(t) {
  return this._report ? It(this._report, t, m(this, i, I)) : t;
};
R = function(t, e = {}) {
  return bt(o(this, i, L).call(this, t), m(this, i, I), e);
};
j = function(t, { hero: e = !1 } = {}) {
  return l`<div class="tile-value ${e ? "hero" : ""}"><span style="--chars: ${t.length}">${t}</span></div>`;
};
Bt = function(t) {
  return this._metric === "tokens" ? t.totalTokens : this._metric === "requests" ? t.requests : t.cost;
};
jt = function(t) {
  return this._metric === "cost" ? o(this, i, R).call(this, t) : this._metric === "tokens" ? `${f(t)} tokens` : `${f(t)} ${t === 1 ? "request" : "requests"}`;
};
Ht = function() {
  return this._days === 365 ? "12 months" : `${this._days} days`;
};
it = function(t, e, s) {
  const a = t.currentTarget, r = a.closest(".card");
  if (!r) return;
  const n = r.getBoundingClientRect(), h = a.getBoundingClientRect();
  this._tooltip = { card: r, x: h.left - n.left + h.width / 2, y: h.top - n.top, title: e, lines: s };
};
Vt = async function() {
  const t = this.shadowRoot?.querySelector(".heatmap-scroll");
  await t?.closest("uui-box")?.updateComplete, t && (t.scrollLeft = t.scrollWidth);
};
Gt = function() {
  const t = this.shadowRoot?.querySelector(".tooltip"), e = this._tooltip?.card;
  if (!t || !e) return;
  t.style.setProperty("--tip-shift", "0px");
  const s = 4, a = e.getBoundingClientRect(), r = t.getBoundingClientRect(), n = r.left < a.left + s ? a.left + s - r.left : r.right > a.right - s ? a.right - s - r.right : 0;
  t.style.setProperty("--tip-shift", `${n}px`);
};
J = /* @__PURE__ */ new WeakMap();
V = function(t) {
  const e = this._tooltip;
  return !e || e.card.dataset.card !== t ? _ : l`<div class="tooltip" style="left:${e.x}px;top:${e.y}px" role="status">
      <div class="tooltip-title">${e.title}</div>
      ${e.lines.map((s) => l`<div class="tooltip-line"><strong>${s.value}</strong><span>${s.label}</span></div>`)}
    </div>`;
};
Xt = function() {
  const t = this._report?.currencies ?? [], e = !!this._report && !this._report.rates;
  return l`<div class="filters">
      <uui-button-group aria-label="Period">
        ${he.map(
    (s) => l`<uui-button
            compact
            look=${s === this._days ? "primary" : "secondary"}
            aria-pressed=${s === this._days}
            @click=${() => o(this, i, Ot).call(this, s)}
            >${s === 365 ? "12 months" : `${s} days`}</uui-button
          >`
  )}
      </uui-button-group>
      <label class="currency">
        <span>Currency</span>
        <select @change=${o(this, i, Lt)} ?disabled=${e} aria-label="Currency">
          ${De(t, m(this, i, I)).map((s) => l`<option value=${s} ?selected=${s === m(this, i, I)}>${s}</option>`)}
        </select>
      </label>
    </div>`;
};
X = function(t, e) {
  if (!this._report?.previous || e === void 0) return _;
  const s = Ae(t, e), a = `vs previous ${o(this, i, Ht).call(this)}`;
  return s.kind === "new" ? l`<div class="tile-change">New ${a}</div>` : s.kind === "change" ? l`<div class="tile-change">${s.percent > 0 ? "+" : ""}${s.percent} % ${a}</div>` : _;
};
Jt = function() {
  const t = this._report, e = Rt(t, m(this, i, I));
  return l`<uui-box class="tile">
      <div class="tile-label">This month</div>
      ${o(this, i, j).call(this, o(this, i, R).call(this, t.month.cost))}
      <div class="tile-note">Projected ${o(this, i, R).call(this, t.month.projectedCost)} by month end</div>
      ${e.kind === "set" ? l`<div
              class="budget budget-${e.level}"
              role="meter"
              aria-valuemin="0"
              aria-valuemax="100"
              aria-valuenow=${Math.round(e.spentShare * 100)}
              aria-label="Share of the monthly budget used">
              <span class="budget-fill" style="width:${Math.min(100, e.spentShare * 100)}%"></span>
            </div>
            <div class="tile-note">${f(e.spentShare * 100)} % of ${bt(e.amount, m(this, i, I))}</div>` : e.kind === "unavailable" ? l`<div class="tile-note">${e.reason}</div>` : _}
    </uui-box>`;
};
Qt = function() {
  const t = this._report, e = t.totals, s = t.previous?.totals, a = t.days.reduce((c, u) => c + u.inputTokens, 0), r = t.days.reduce((c, u) => c + u.outputTokens, 0), n = e.requests > 0 ? e.cost / e.requests : 0, h = s && s.requests > 0 && e.requests > 0 ? s.cost / s.requests : void 0;
  return l`<div class="tiles">
      ${o(this, i, Jt).call(this)}
      <uui-box class="tile tile-hero">
        <div class="tile-label">Estimated cost</div>
        ${o(this, i, j).call(this, o(this, i, R).call(this, e.cost), { hero: !0 })}
        ${e.unpricedTokens > 0 ? l`<div class="tile-note">
              <uui-icon name="icon-alert"></uui-icon> ${f(e.unpricedTokens)} tokens from models without a price
            </div>` : l`<div class="tile-note">${o(this, i, Yt).call(this)}</div>`}
        ${o(this, i, X).call(this, e.cost, s?.cost)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Tokens</div>
        ${o(this, i, j).call(this, f(e.totalTokens, { compact: !0 }))}
        <div class="tile-note">${f(a, { compact: !0 })} in · ${f(r, { compact: !0 })} out</div>
        ${o(this, i, X).call(this, e.totalTokens, s?.totalTokens)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Requests</div>
        ${o(this, i, j).call(this, f(e.requests))}
        <div class="tile-note">${f(e.requests / t.days.length)} per day on average</div>
        ${o(this, i, X).call(this, e.requests, s?.requests)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Cost per request</div>
        ${o(this, i, j).call(this, e.requests > 0 ? o(this, i, R).call(this, n) : "–")}
        <div class="tile-note">Priced models only</div>
        ${o(this, i, X).call(this, n, h)}
      </uui-box>
    </div>`;
};
Yt = function() {
  const t = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" });
  return `${t.format(q(this._report.from))} – ${t.format(q(this._report.to))}`;
};
Kt = function() {
  const t = this._report.days, e = t.map((d) => o(this, i, Bt).call(this, d)), s = Pe(e.filter((d) => d > 0)), a = (q(t[0].date).getUTCDay() + 6) % 7, r = Math.ceil((a + t.length) / 7), n = dt + r * (D + U), h = pt + 7 * (D + U), c = new Intl.DateTimeFormat(void 0, { month: "short" }), u = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short", year: "numeric" }), v = new Intl.DateTimeFormat(void 0, { weekday: "short" }), b = [], w = t.map((d, g) => {
    const N = a + g, P = Math.floor(N / 7), W = N % 7, k = q(d.date);
    (k.getUTCDate() === 1 || g === 0) && b.push({ col: P, label: c.format(k) });
    const p = e[g], C = p > 0 ? We(p, s) : -1;
    return M`<rect
        class="cell ${C < 0 ? "cell-empty" : `cell-${C}`}"
        x=${dt + P * (D + U)}
        y=${pt + W * (D + U)}
        width=${D}
        height=${D}
        rx="2"
        @pointerenter=${(E) => o(this, i, it).call(this, E, u.format(k), [
      { value: o(this, i, R).call(this, d.cost), label: "cost" },
      { value: f(d.totalTokens), label: "tokens" },
      { value: f(d.requests), label: d.requests === 1 ? "request" : "requests" }
    ])}
        @pointerleave=${m(this, J)}
      ></rect>`;
  }), z = b.filter((d, g) => g === 0 || d.col - b[g - 1].col >= 3);
  return l`<uui-box class="card" data-card="heatmap" headline="Usage calendar">
      ${o(this, i, lt).call(this, l`<uui-button-group aria-label="Heatmap metric">
        ${de.map(
    (d) => l`<uui-button
            compact
            look=${d.key === this._metric ? "primary" : "secondary"}
            aria-pressed=${d.key === this._metric}
            @click=${() => o(this, i, Pt).call(this, d.key)}
            >${d.label}</uui-button
          >`
  )}
      </uui-button-group>`)}
      <div class="heatmap-scroll">
        <svg
          class="heatmap"
          width=${n}
          height=${h}
          viewBox="0 0 ${n} ${h}"
          role="img"
          aria-label="Daily ${this._metric} over the selected period. The table under the cost chart lists every value.">
          ${z.map((d) => M`<text class="axis-label" x=${dt + d.col * (D + U)} y="11">${d.label}</text>`)}
          ${[0, 2, 4].map((d) => {
    const g = v.format(new Date(Date.UTC(2024, 0, 1 + d)));
    return M`<text class="axis-label" x="0" y=${pt + d * (D + U) + D - 2}>${g}</text>`;
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
        ${s.length ? l`<span class="scale-range">up to ${o(this, i, jt).call(this, Math.max(...e))} a day</span>` : _}
      </div>
      ${o(this, i, V).call(this, "heatmap")}
    </uui-box>`;
};
ot = function() {
  const t = this._report.days.length > 92;
  return { buckets: t ? Fe(this._report.days) : Ee(this._report.days), weekly: t };
};
H = /* @__PURE__ */ new WeakMap();
rt = /* @__PURE__ */ new WeakMap();
nt = /* @__PURE__ */ new WeakMap();
Zt = function(t) {
  const e = new Set(this._tables);
  e.has(t) ? e.delete(t) : e.add(t), this._tables = e;
};
lt = function(t) {
  return l`<div slot="header-actions" class="header-actions card-actions-header">${t}</div>
      <div class="header-actions card-actions-body">${t}</div>`;
};
vt = function(t, e) {
  return t.cost === null ? l`<uui-tag look="secondary" title=${e}>No price</uui-tag>` : o(this, i, R).call(this, t.cost);
};
ft = function(t, e) {
  if (t === null || e <= 0) return "–";
  const s = t / e * 100;
  return l`<span class="share"
      ><span class="share-track"><span class="share-fill" style="width:${s}%"></span></span>${f(s)}%</span
    >`;
};
Q = function(t) {
  return l`<uui-button compact look="secondary" @click=${() => o(this, i, Zt).call(this, t)}>
      ${this._tables.has(t) ? "Show chart" : "Show as table"}
    </uui-button>`;
};
te = function(t) {
  if (this._costView === "total")
    return [{ id: "cost", label: "Cost", color: "var(--viz-cat-1)", values: t.map((a) => o(this, i, L).call(this, a.cost)) }];
  const { ids: e, values: s } = xe(this._report, t);
  return e.map((a, r) => ({ id: a, label: a, color: K(a, r), values: s[r].map((n) => o(this, i, L).call(this, n)) }));
};
ct = function(t) {
  return t.length < 2 ? _ : l`<div class="legend">
      ${t.map(
    (e) => l`<span class="legend-item"
          >${e.icon ? l`<uui-icon name=${e.icon} style="color:${e.color}"></uui-icon>` : l`<span class="legend-swatch" style="background:${e.color}"></span>`}${e.label}</span
        >`
  )}
    </div>`;
};
ut = function(t, e, s, a, r, n) {
  const h = t.map((k, p) => s.reduce((C, E) => C + E.values[p], 0)), c = Math.max(0, ...h), u = Ut(c), v = u[u.length - 1] || 1, b = Math.max(pe, this._chartWidth - y), w = b / t.length, z = Math.max(1, Math.min(Xe, w - U)), d = (k) => k / v * S, g = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short" }), N = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), P = Math.max(1, Math.ceil(t.length / Math.max(2, Math.floor(b / 90)))), W = Ie(ze(s.map((k) => k.values)), d, S, U);
  return l`<div class="column-chart">
      <svg width=${y + b} height=${st + S + me} role="img" aria-label="${n}. Use “Show as table” for the values.">
        <g transform="translate(0 ${st})">
          ${u.map(
    (k) => M`
              <line class="grid" x1=${y} x2=${y + b} y1=${S - d(k)} y2=${S - d(k)}></line>
              <text class="axis-label tick" x=${y - 8} y=${S - d(k) + 4}>${r(k)}</text>`
  )}
          ${t.map((k, p) => {
    const C = y + p * w + (w - z) / 2, E = e ? `Week of ${g.format(q(k.start))}` : N.format(q(k.start)), Y = s.map((O) => ({ value: a(O.values[p]), label: O.label.toLowerCase() }));
    return s.length > 1 && Y.push({ value: a(h[p]), label: "total" }), M`
              <rect class="hit" x=${y + p * w} y="0" width=${w} height=${S}
                @pointerenter=${(O) => o(this, i, it).call(this, O, E, Y)}
                @pointerleave=${m(this, J)}></rect>
              ${s.map((O, ve) => {
      const B = W[ve][p];
      return B ? B.kind === "rounded" ? M`<path class="bar" style="fill:${O.color}" d=${Be(C, B.base, z, B.height)}></path>` : M`<rect class="bar" style="fill:${O.color}" x=${C} y=${B.y} width=${z} height=${B.height}></rect>` : _;
    })}
              ${p % P === 0 ? M`<text class="axis-label" x=${C + z / 2} y=${S + 18} text-anchor="middle">${g.format(q(k.start))}</text>` : _}`;
  })}
          <line class="baseline" x1=${y} x2=${y + b} y1=${S} y2=${S}></line>
        </g>
      </svg>
    </div>`;
};
ht = function(t, e, s, a, r) {
  const n = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }), h = t.map((c, u) => ({ b: c, i: u })).filter(({ i: c }) => s.some((u) => u.values[c] > 0)).reverse();
  return h.length ? l`<div class="table-scroll">
      <uui-table aria-label=${r}>
        <uui-table-head>
          <uui-table-head-cell>${e ? "Week of" : "Date"}</uui-table-head-cell>
          ${s.map((c) => l`<uui-table-head-cell class="num">${c.label}</uui-table-head-cell>`)}
          ${s.length > 1 ? l`<uui-table-head-cell class="num">Total</uui-table-head-cell>` : _}
        </uui-table-head>
        ${h.map(
    ({ b: c, i: u }) => l`<uui-table-row>
            <uui-table-cell>${n.format(q(c.start))}</uui-table-cell>
            ${s.map((v) => l`<uui-table-cell class="num">${a(v.values[u])}</uui-table-cell>`)}
            ${s.length > 1 ? l`<uui-table-cell class="num">${a(s.reduce((v, b) => v + b.values[u], 0))}</uui-table-cell>` : _}
          </uui-table-row>`
  )}
      </uui-table>
    </div>` : l`<p class="empty">No AI usage in this period.</p>`;
};
ee = function() {
  const t = this._report, e = q(t.month.to), s = new Date(Date.UTC(e.getUTCFullYear(), e.getUTCMonth() + 1, 0)).getUTCDate(), a = Rt(t, m(this, i, I)), r = a.kind === "set" ? a.amount : void 0, n = we(t.month, s), h = (p) => ({ day: p.day, value: o(this, i, L).call(this, p.value) }), c = n.actual.map(h), u = n.projection.map(h), v = Math.max(o(this, i, L).call(this, n.max), r ?? 0), b = Ut(v), w = b[b.length - 1] || 1, z = Math.max(pe, this._chartWidth - y - zt), d = (p) => qe(p, s, y, z), g = (p) => A - p / w * A, N = (p) => p.map((C, E) => `${E ? "L" : "M"}${d(C.day)},${g(C.value)}`).join(" "), P = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), W = z / Math.max(1, s - 1), k = this._tables.has("month");
  return l`<uui-box class="card" data-card="month" headline="This month">
      <div slot="header-actions" class="header-actions">${o(this, i, Q).call(this, "month")}</div>
      ${k ? o(this, i, se).call(this) : l`<div class="legend">
              <span class="legend-item"><span class="legend-line"></span>Cost so far</span>
              <span class="legend-item"><span class="legend-line dashed"></span>Projected ${o(this, i, R).call(this, t.month.projectedCost)}</span>
              ${r !== void 0 ? l`<span class="legend-item"><span class="legend-line budget"></span>Budget ${m(this, H).call(this, r)}</span>` : _}
            </div>
            <div class="column-chart">
              <svg width=${y + z + zt} height=${st + A + me} role="img"
                aria-label="Cumulative cost this month with the projection to month end. Use “Show as table” for the values.">
                <g transform="translate(0 ${st})">
                  ${b.map(
    (p) => M`
                      <line class="grid" x1=${y} x2=${y + z} y1=${g(p)} y2=${g(p)}></line>
                      <text class="axis-label tick" x=${y - 8} y=${g(p) + 4}>${m(this, rt).call(this, p)}</text>`
  )}
                  ${r !== void 0 ? M`<line class="budget-line" x1=${y} x2=${y + z} y1=${g(r)} y2=${g(r)}></line>
                        <text class="axis-label" x=${y + z + 6} y=${g(r) + 4}>Budget</text>` : _}
                  ${this._monthHover !== null ? M`<line class="crosshair" x1=${d(this._monthHover)} x2=${d(this._monthHover)} y1="0" y2=${A}></line>` : _}
                  <path class="line projection" d=${N(u)}></path>
                  <path class="line" d=${N(c)}></path>
                  ${c.length ? M`<circle class="dot" cx=${d(c[c.length - 1].day)} cy=${g(c[c.length - 1].value)} r="4"></circle>` : _}
                  ${[1, 8, 15, 22, s].map(
    (p) => M`<text class="axis-label" x=${d(p)} y=${A + 18} text-anchor="middle">${p}</text>`
  )}
                  ${c.map((p, C) => {
    const E = q(t.month.days[C].date);
    return M`<rect class="hit" x=${d(p.day) - W / 2} y="0" width=${W} height=${A}
                      @pointerenter=${(Y) => {
      this._monthHover = p.day, o(this, i, it).call(this, Y, P.format(E), [
        { value: m(this, H).call(this, p.value), label: "so far" },
        { value: o(this, i, R).call(this, t.month.days[C].cost), label: "that day" }
      ]);
    }}
                      @pointerleave=${() => {
      this._monthHover = null, m(this, J).call(this);
    }}></rect>`;
  })}
                  <line class="baseline" x1=${y} x2=${y + z} y1=${A} y2=${A}></line>
                </g>
              </svg>
            </div>`}
      ${o(this, i, V).call(this, "month")}
    </uui-box>`;
};
se = function() {
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
    ({ d: a, total: r }) => l`<uui-table-row>
            <uui-table-cell>${t.format(q(a.date))}</uui-table-cell>
            <uui-table-cell class="num">${o(this, i, R).call(this, a.cost)}</uui-table-cell>
            <uui-table-cell class="num">${o(this, i, R).call(this, r)}</uui-table-cell>
          </uui-table-row>`
  )}
      </uui-table>
    </div>`;
};
ae = function() {
  const { buckets: t, weekly: e } = o(this, i, ot).call(this), s = o(this, i, te).call(this, t), a = Se(s.every((c) => c.values.every((u) => u === 0)), this._report.totals.unpricedTokens), r = `${e ? "Weekly" : "Daily"} cost`, n = this._tables.has("columns"), h = this._report.totals.unpricedTokens > 0;
  return l`<uui-box class="card" data-card="columns" headline=${r}>
      ${o(this, i, lt).call(this, l`<uui-button-group aria-label="Cost per">
          ${["total", "model"].map(
    (c) => l`<uui-button compact look=${c === this._costView ? "primary" : "secondary"} aria-pressed=${c === this._costView}
              @click=${() => this._costView = c}>${c === "total" ? "Total" : "By model"}</uui-button>`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" @click=${() => o(this, i, le).call(this, t, e, s)}>Download CSV</uui-button>
        ${o(this, i, Q).call(this, "columns")}`)}
      ${o(this, i, ct).call(this, s)}
      ${a ? l`<p class="empty">${a}</p>` : n ? o(this, i, ht).call(this, t, e, s, m(this, H), `${r} per ${this._costView === "total" ? "period" : "model"}`) : o(this, i, ut).call(this, t, e, s, m(this, H), m(this, rt), `${r}${this._costView === "model" ? " by model" : ""}`)}
      ${this._costView === "model" && h ? l`<p class="chart-note">Models without a configured price aren’t included.</p>` : _}
      ${o(this, i, V).call(this, "columns")}
    </uui-box>`;
};
ie = function() {
  const { buckets: t, weekly: e } = o(this, i, ot).call(this), s = Me(t), a = (n) => f(n), r = `Tokens by type${e ? " per week" : ""}`;
  return l`<uui-box class="card" data-card="tokens" headline=${r}>
      <div slot="header-actions" class="header-actions">${o(this, i, Q).call(this, "tokens")}</div>
      <div class="figures">
        <div><div class="tile-label">Caching saved</div><div class="figure">${o(this, i, R).call(this, this._report.cachingSavings)}</div></div>
      </div>
      ${o(this, i, ct).call(this, s)}
      ${this._tables.has("tokens") ? o(this, i, ht).call(this, t, e, s, a, r) : s.every((n) => n.values.every((h) => h === 0)) ? l`<p class="empty">No AI usage in this period.</p>` : o(this, i, ut).call(this, t, e, s, a, m(this, nt), r)}
      ${o(this, i, V).call(this, "tokens")}
    </uui-box>`;
};
oe = function() {
  const t = this._report, { buckets: e, weekly: s } = o(this, i, ot).call(this), a = t.days.reduce((u, v) => u + v.succeededRequests, 0), r = t.days.reduce((u, v) => u + v.failedRequests, 0), n = [
    { id: "succeeded", label: "Succeeded", color: "var(--viz-cat-1)", values: e.map((u) => u.succeededRequests) },
    { id: "failed", label: "Failed", color: "var(--viz-critical)", icon: "icon-alert", values: e.map((u) => u.failedRequests) }
  ], h = (u) => f(u), c = `Requests${s ? " per week" : ""}`;
  return l`<uui-box class="card" data-card="reliability" headline="Reliability">
      <div slot="header-actions" class="header-actions">${o(this, i, Q).call(this, "reliability")}</div>
      <div class="figures">
        <div><div class="tile-label">Failed requests</div><div class="figure">${f(r)} <span class="figure-note">(${f(Te(a, r) * 100)} %)</span></div></div>
        <div><div class="tile-label">Average response time</div><div class="figure">${t.averageDurationMs > 0 ? Ce(t.averageDurationMs) : "–"}</div></div>
      </div>
      ${o(this, i, ct).call(this, n)}
      ${this._tables.has("reliability") ? o(this, i, ht).call(this, e, s, n, h, c) : a + r === 0 ? l`<p class="empty">No AI usage in this period.</p>` : o(this, i, ut).call(this, e, s, n, h, m(this, nt), c)}
      ${o(this, i, V).call(this, "reliability")}
    </uui-box>`;
};
re = function() {
  if (this._dimension === "model")
    return this._report.models.map((e) => ({ ...e, name: e.modelId }));
  const t = Re(`${this._dimension}:${this._days}`, this._breakdowns, this._breakdownErrors);
  return t.kind === "rows" ? t.breakdown.rows : t.kind === "error" ? { error: t.message } : void 0;
};
ne = function() {
  const t = xt.find((n) => n.key === this._dimension).label, e = o(this, i, re).call(this), s = Array.isArray(e) ? e : void 0, a = this._report.totals.cost, r = this._dimension === "model" ? "Add this model to Esatto:AICosts:PricesPerMillionTokens in appsettings" : "Some of this usage is from models without a configured price";
  return l`<uui-box class="card" headline="Cost by">
      ${o(this, i, lt).call(this, l`<uui-button-group aria-label="Group cost by">
          ${xt.map(
    (n) => l`<uui-button
              compact
              look=${n.key === this._dimension ? "primary" : "secondary"}
              aria-pressed=${n.key === this._dimension}
              @click=${() => o(this, i, Wt).call(this, n.key)}
              >${n.label}</uui-button
            >`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" ?disabled=${!s?.length} @click=${() => o(this, i, ce).call(this, t, s ?? [])}
          >Download CSV</uui-button
        >`)}
      ${e && "error" in e ? l`<p class="error">${e.error}</p>` : s ? s.length === 0 ? l`<p class="empty">No AI usage in this period.</p>` : l`<div class="table-scroll cost-table">
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
    (n) => l`<uui-table-row>
                      <uui-table-cell>${n.name}</uui-table-cell>
                      <uui-table-cell class="num">${f(n.requests)}</uui-table-cell>
                      <uui-table-cell class="num">${f(n.inputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${f(n.cachedInputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${f(n.outputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${o(this, i, vt).call(this, n, r)}</uui-table-cell>
                      <uui-table-cell class="num">${o(this, i, ft).call(this, n.cost, a)}</uui-table-cell>
                    </uui-table-row>`
  )}
                </uui-table>
              </div>
              <ul class="cost-list" aria-label="Cost by ${t.toLowerCase()}">
                ${s.map(
    (n) => l`<li>
                    <span class="cost-list-name">${n.name}</span>
                    <span class="cost-list-cost">${o(this, i, vt).call(this, n, r)}</span>
                    <span class="cost-list-tokens"
                      >${f(n.requests)} requests · ${f(n.inputTokens, { compact: !0 })} in ·
                      ${f(n.cachedInputTokens, { compact: !0 })} cached · ${f(n.outputTokens, { compact: !0 })} out</span
                    >
                    <span class="cost-list-share">${o(this, i, ft).call(this, n.cost, a)}</span>
                  </li>`
  )}
              </ul>` : l`<uui-loader-bar></uui-loader-bar>`}
      ${this._breakdownLoading && s ? l`<uui-loader-bar></uui-loader-bar>` : _}
    </uui-box>`;
};
_t = function(t) {
  return Number(o(this, i, L).call(this, t).toFixed(4));
};
le = function(t, e, s) {
  const a = this._report, r = this._costView === "model", n = [
    e ? "Week of" : "Date",
    "Requests",
    "Input tokens",
    "Cached input tokens",
    "Output tokens",
    "Total tokens",
    `Cost (${m(this, i, I)})`,
    "Unpriced tokens",
    ...r ? s.map((c) => `${c.label} (${m(this, i, I)})`) : []
  ], h = t.map((c, u) => [
    c.start,
    c.requests,
    c.inputTokens,
    c.cachedInputTokens,
    c.outputTokens,
    c.totalTokens,
    o(this, i, _t).call(this, c.cost),
    c.unpricedTokens,
    ...r ? s.map((v) => Number(v.values[u].toFixed(4))) : []
  ]);
  Et(`ai-costs-${a.from}-${a.to}${r ? "-by-model" : ""}.csv`, St(n, h));
};
ce = function(t, e) {
  const s = this._report, a = [t, "Requests", "Input tokens", "Cached input tokens", "Output tokens", "Total tokens", `Cost (${m(this, i, I)})`], r = e.map((n) => [n.name, n.requests, n.inputTokens, n.cachedInputTokens, n.outputTokens, n.totalTokens, n.cost === null ? "" : o(this, i, _t).call(this, n.cost)]);
  Et(`ai-costs-${s.from}-${s.to}-${this._dimension}.csv`, St(a, r));
};
ue = function() {
  const t = this._report, e = t.rates ? new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }).format(q(t.rates.date)) : null;
  return l`<p class="footnote">
      Estimates: tokens × list prices per model${t.pricesSource ? l` (${t.pricesSource})` : _}, in ${t.baseCurrency}.
      Cached input tokens are billed at the cache-read price. Excludes taxes, discounts and provider surcharges.
      ${t.rates ? l`Converted with ${t.rates.source} reference rates of ${e}.` : l`<strong>Exchange rates are unavailable right now — amounts are shown in ${t.baseCurrency}.</strong>`}
      Times are UTC.
    </p>`;
};
$.styles = [je];
x([
  T()
], $.prototype, "_report", 2);
x([
  T()
], $.prototype, "_loading", 2);
x([
  T()
], $.prototype, "_error", 2);
x([
  T()
], $.prototype, "_days", 2);
x([
  T()
], $.prototype, "_currency", 2);
x([
  T()
], $.prototype, "_metric", 2);
x([
  T()
], $.prototype, "_tables", 2);
x([
  T()
], $.prototype, "_costView", 2);
x([
  T()
], $.prototype, "_tooltip", 2);
x([
  T()
], $.prototype, "_dark", 2);
x([
  T()
], $.prototype, "_chartWidth", 2);
x([
  T()
], $.prototype, "_dimension", 2);
x([
  T()
], $.prototype, "_breakdowns", 2);
x([
  T()
], $.prototype, "_breakdownLoading", 2);
x([
  T()
], $.prototype, "_breakdownErrors", 2);
x([
  T()
], $.prototype, "_monthHover", 2);
$ = x([
  be("esatto-ai-costs-view")
], $);
const Ze = $;
export {
  $ as EsattoAICostsViewElement,
  Ze as default
};
//# sourceMappingURL=ai-costs-view.js.map
