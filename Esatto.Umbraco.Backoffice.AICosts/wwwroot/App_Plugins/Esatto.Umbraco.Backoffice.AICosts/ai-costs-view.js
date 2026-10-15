import { css as Ut, state as f, customElement as Nt, nothing as $, html as c, svg as C } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as Pt } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as et } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient as st } from "@umbraco-cms/backoffice/http-client";
const at = "/umbraco/management/api/v1/esatto/ai-costs", it = () => [{ scheme: "bearer", type: "http" }], Ot = (t) => st.get({ url: `${at}/report?days=${t}`, security: it() }), At = (t, e) => st.get({ url: `${at}/breakdown?dimension=${t}&days=${e}`, security: it() });
function Wt(t) {
  if (!t.rates) return t;
  const e = Object.fromEntries(
    Object.entries(t.rates.perBaseUnit).map(([s, i]) => [s.toUpperCase(), i])
  );
  return { ...t, rates: { ...t.rates, perBaseUnit: e } };
}
function Lt(t, e) {
  return e && (e === t.baseCurrency || t.rates?.perBaseUnit[e]) ? e : t.baseCurrency;
}
function ot(t, e, s) {
  return s === t.baseCurrency ? e : e * (t.rates?.perBaseUnit[s] ?? 1);
}
function rt(t, e, { compact: s = !1 } = {}) {
  const i = Math.abs(t) > 0 && Math.abs(t) < 1;
  return new Intl.NumberFormat(void 0, {
    style: "currency",
    currency: e,
    notation: s ? "compact" : "standard",
    minimumFractionDigits: s ? 0 : 2,
    maximumFractionDigits: s ? 1 : i ? 4 : 2
  }).format(t);
}
function d(t, { compact: e = !1 } = {}) {
  return new Intl.NumberFormat(void 0, { notation: e ? "compact" : "standard", maximumFractionDigits: 1 }).format(t);
}
function Bt(t, e) {
  const s = t.month.budget;
  if (!s) return { kind: "none" };
  const i = s.currency === t.baseCurrency ? 1 : t.rates?.perBaseUnit[s.currency];
  if (!i)
    return { kind: "unavailable", reason: `Exchange rates are unavailable, so the ${s.currency} budget can’t be compared right now.` };
  const a = s.amount / i, r = a > 0 ? t.month.cost / a : 0, u = t.month.cost > a ? "over" : r >= 0.8 || t.month.projectedCost > a ? "warning" : "ok";
  return { kind: "set", amount: ot(t, a, e), spentShare: r, level: u };
}
function k(t) {
  const [e, s, i] = t.split("-").map(Number);
  return new Date(Date.UTC(e, s - 1, i));
}
const nt = (t) => ({
  start: t.date,
  end: t.date,
  requests: t.requests,
  inputTokens: t.inputTokens,
  outputTokens: t.outputTokens,
  cachedInputTokens: t.cachedInputTokens,
  totalTokens: t.totalTokens,
  cost: t.cost,
  unpricedTokens: t.unpricedTokens
}), Ht = (t) => t.map(nt);
function jt(t) {
  const e = [];
  for (const s of t) {
    const i = e[e.length - 1];
    if (!i || k(s.date).getUTCDay() === 1) {
      e.push(nt(s));
      continue;
    }
    i.end = s.date, i.requests += s.requests, i.inputTokens += s.inputTokens, i.outputTokens += s.outputTokens, i.cachedInputTokens += s.cachedInputTokens, i.totalTokens += s.totalTokens, i.cost += s.cost, i.unpricedTokens += s.unpricedTokens;
  }
  return e;
}
function Vt(t, e) {
  return e == null ? { kind: "none" } : e === 0 ? t === 0 ? { kind: "change", percent: 0 } : { kind: "new" } : { kind: "change", percent: Math.round((t - e) / e * 100) };
}
function Gt(t) {
  return new Intl.NumberFormat(t).formatToParts(1.5).find((s) => s.type === "decimal")?.value === "," ? { field: ";", decimal: "," } : { field: ",", decimal: "." };
}
function lt(t, e, s) {
  const { field: i, decimal: a } = Gt(s), r = (u) => {
    const v = typeof u == "number" ? String(u).replace(".", a) : u;
    return /["\r\n]/.test(v) || v.includes(i) ? `"${v.replace(/"/g, '""')}"` : v;
  };
  return "\uFEFF" + [t, ...e].map((u) => u.map(r).join(i)).join(`\r
`);
}
function ct(t, e) {
  const s = URL.createObjectURL(new Blob([e], { type: "text/csv;charset=utf-8" }));
  Object.assign(document.createElement("a"), { href: s, download: t }).click(), URL.revokeObjectURL(s);
}
const ut = "esatto.aiCosts.prefs", dt = () => {
  try {
    return globalThis.localStorage;
  } catch {
    return;
  }
};
function Xt(t = dt()) {
  try {
    const e = JSON.parse(t?.getItem(ut) ?? "{}");
    return e && typeof e == "object" && !Array.isArray(e) ? e : {};
  } catch {
    return {};
  }
}
function Jt(t, e = dt()) {
  try {
    e?.setItem(ut, JSON.stringify(t));
  } catch {
  }
}
function Qt(t) {
  if (!t.length) return [];
  const e = [...t].sort((s, i) => s - i);
  return [0.25, 0.5, 0.75].map((s) => e[Math.min(e.length - 1, Math.floor(s * e.length))]);
}
function Zt(t, e) {
  for (let s = 0; s < e.length; s++) if (t <= e[s]) return s;
  return 3;
}
function Kt(t) {
  if (t <= 0) return [0, 1];
  const e = t / 4, s = 10 ** Math.floor(Math.log10(e)), i = [1, 2, 2.5, 5, 10].map((r) => r * s).find((r) => r >= e) ?? 10 * s, a = [];
  for (let r = 0; r <= t + i * 0.999; r += i) a.push(Number(r.toFixed(10)));
  return a;
}
function Yt(t, e, s, i) {
  const a = Math.min(4, s / 2, i), r = e - i;
  return `M${t},${e} V${r + a} Q${t},${r} ${t + a},${r} H${t + s - a} Q${t + s},${r} ${t + s},${r + a} V${e} Z`;
}
const g = 12, te = Ut`
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
    }
    :host([dark]) {
        --viz-column: #3987e5;
        --viz-heat-0: #256abf;
        --viz-heat-1: #3987e5;
        --viz-heat-2: #86b6ef;
        --viz-heat-3: #cde2fb;
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
        width: ${g}px;
        height: ${g}px;
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
    .header-actions {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
    }
`;
var ee = Object.defineProperty, se = Object.getOwnPropertyDescriptor, ht = (t) => {
  throw TypeError(t);
}, m = (t, e, s, i) => {
  for (var a = i > 1 ? void 0 : i ? se(e, s) : e, r = t.length - 1, u; r >= 0; r--)
    (u = t[r]) && (a = (i ? u(e, s, a) : u(a)) || a);
  return i && a && ee(e, s, a), a;
}, j = (t, e, s) => e.has(t) || ht("Cannot " + s), b = (t, e, s) => (j(t, e, "read from private field"), s ? s.call(t) : e.get(t)), U = (t, e, s) => e.has(t) ? ht("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, s), ae = (t, e, s, i) => (j(t, e, "write to private field"), e.set(t, s), s), n = (t, e, s) => (j(t, e, "access private method"), s), E, N, o, V, G, P, X, pt, mt, vt, bt, x, F, _, ft, yt, gt, J, O, Q, $t, S, kt, _t, wt, xt, Tt, zt, Ct, qt, It, Z, Mt, Dt, Et;
const St = [30, 90, 180, 365], Ft = [
  { key: "cost", label: "Cost" },
  { key: "tokens", label: "Tokens" },
  { key: "requests", label: "Requests" }
], tt = [
  { key: "model", label: "Model" },
  { key: "profile", label: "Profile" },
  { key: "user", label: "User" },
  { key: "provider", label: "Provider" }
], z = 2, B = 30, H = 18, p = 220, w = 64, ie = 26, oe = 24;
let h = class extends Pt {
  constructor() {
    super(), U(this, o), this._loading = !1, this._error = null, this._showTable = !1, this._tooltip = null, this._dark = !1, this._chartWidth = 900, this._dimension = "model", this._breakdowns = /* @__PURE__ */ new Map(), this._breakdownLoading = !1, this._breakdownError = null, U(this, E, new ResizeObserver((e) => {
      const s = Math.floor(e[0]?.contentRect.width ?? 0);
      s > 0 && s !== this._chartWidth && (this._chartWidth = s);
    })), U(this, N), U(this, O, () => {
      this._tooltip = null;
    });
    const t = Xt();
    this._days = St.includes(t.days ?? 0) ? t.days : 365, this._currency = t.currency ?? null, this._metric = Ft.some((e) => e.key === t.metric) ? t.metric : "cost";
  }
  connectedCallback() {
    super.connectedCallback(), n(this, o, V).call(this);
  }
  disconnectedCallback() {
    b(this, E).disconnect(), super.disconnectedCallback();
  }
  firstUpdated() {
    n(this, o, X).call(this);
  }
  willUpdate(t) {
    t.has("_dark") && this.toggleAttribute("dark", this._dark);
  }
  updated() {
    const t = this.shadowRoot?.querySelector(".column-chart");
    t && t !== b(this, N) && (b(this, E).disconnect(), b(this, E).observe(t), ae(this, N, t));
  }
  // ---- render -------------------------------------------------------------------------
  render() {
    return c`
      <span class="theme-probe" aria-hidden="true"></span>
      ${n(this, o, $t).call(this)}
      ${this._error ? c`<uui-box><p class="error">${this._error}</p></uui-box>` : $}
      ${this._report ? c`<div class="content ${this._loading ? "is-loading" : ""}">
            ${n(this, o, _t).call(this)} ${n(this, o, xt).call(this)} ${n(this, o, zt).call(this)} ${n(this, o, It).call(this)}
            ${n(this, o, Et).call(this)}
          </div>` : this._error ? $ : c`<uui-loader-bar></uui-loader-bar>`}
    `;
  }
};
E = /* @__PURE__ */ new WeakMap();
N = /* @__PURE__ */ new WeakMap();
o = /* @__PURE__ */ new WeakSet();
V = async function() {
  this._loading = !0, n(this, o, X).call(this);
  const { data: t, error: e } = await et(this, Ot(this._days), { disableNotifications: !0 });
  if (this._loading = !1, e || !t) {
    this._error = "Could not load AI cost data.";
    return;
  }
  this._error = null, this._report = Wt(t), (!this._currency || !this._report.currencies.includes(this._currency)) && (this._currency = this._report.defaultCurrency), this._dimension !== "model" && n(this, o, G).call(this, this._dimension);
};
G = async function(t) {
  const e = `${t}:${this._days}`;
  if (this._breakdowns.has(e)) return;
  this._breakdownLoading = !0, this._breakdownError = null;
  const { data: s, error: i } = await et(this, At(t, this._days), { disableNotifications: !0 });
  if (this._breakdownLoading = !1, i || !s) {
    this._breakdownError = "Could not load the breakdown.";
    return;
  }
  this._breakdowns = new Map(this._breakdowns).set(e, s);
};
P = function() {
  Jt({ days: this._days, currency: this._currency ?? void 0, metric: this._metric });
};
X = function() {
  const t = this.shadowRoot?.querySelector(".theme-probe");
  if (!t) return;
  const e = getComputedStyle(t).color.match(/\d+(\.\d+)?/g);
  if (!e) return;
  const [s, i, a] = e.map(Number);
  this._dark = (0.2126 * s + 0.7152 * i + 0.0722 * a) / 255 < 0.45;
};
pt = function(t) {
  t !== this._days && (this._days = t, this._breakdowns = /* @__PURE__ */ new Map(), n(this, o, P).call(this), n(this, o, V).call(this));
};
mt = function(t) {
  this._currency = t.target.value, n(this, o, P).call(this);
};
vt = function(t) {
  this._metric = t, n(this, o, P).call(this);
};
bt = function(t) {
  this._dimension = t, t !== "model" && n(this, o, G).call(this, t);
};
x = function() {
  return this._report ? Lt(this._report, this._currency) : "USD";
};
F = function(t) {
  return this._report ? ot(this._report, t, b(this, o, x)) : t;
};
_ = function(t, e = {}) {
  return rt(n(this, o, F).call(this, t), b(this, o, x), e);
};
ft = function(t) {
  return this._metric === "tokens" ? t.totalTokens : this._metric === "requests" ? t.requests : t.cost;
};
yt = function(t) {
  return this._metric === "cost" ? n(this, o, _).call(this, t) : this._metric === "tokens" ? `${d(t)} tokens` : `${d(t)} ${t === 1 ? "request" : "requests"}`;
};
gt = function() {
  return this._days === 365 ? "12 months" : `${this._days} days`;
};
J = function(t, e, s) {
  const i = t.currentTarget, a = i.closest(".card");
  if (!a) return;
  const r = a.getBoundingClientRect(), u = i.getBoundingClientRect();
  this._tooltip = { card: a, x: u.left - r.left + u.width / 2, y: u.top - r.top, title: e, lines: s };
};
O = /* @__PURE__ */ new WeakMap();
Q = function(t) {
  const e = this._tooltip;
  return !e || e.card.dataset.card !== t ? $ : c`<div class="tooltip" style="left:${e.x}px;top:${e.y}px" role="status">
      <div class="tooltip-title">${e.title}</div>
      ${e.lines.map((s) => c`<div class="tooltip-line"><strong>${s.value}</strong><span>${s.label}</span></div>`)}
    </div>`;
};
$t = function() {
  const t = this._report?.currencies ?? [], e = !!this._report && !this._report.rates;
  return c`<div class="filters">
      <uui-button-group aria-label="Period">
        ${St.map(
    (s) => c`<uui-button
            compact
            look=${s === this._days ? "primary" : "secondary"}
            aria-pressed=${s === this._days}
            @click=${() => n(this, o, pt).call(this, s)}
            >${s === 365 ? "12 months" : `${s} days`}</uui-button
          >`
  )}
      </uui-button-group>
      <label class="currency">
        <span>Currency</span>
        <select @change=${n(this, o, mt)} ?disabled=${e} aria-label="Currency">
          ${t.map((s) => c`<option value=${s} ?selected=${s === b(this, o, x)}>${s}</option>`)}
        </select>
      </label>
    </div>`;
};
S = function(t, e) {
  if (!this._report?.previous || e === void 0) return $;
  const s = Vt(t, e), i = `vs previous ${n(this, o, gt).call(this)}`;
  return s.kind === "new" ? c`<div class="tile-change">New ${i}</div>` : s.kind === "change" ? c`<div class="tile-change">${s.percent > 0 ? "+" : ""}${s.percent} % ${i}</div>` : $;
};
kt = function() {
  const t = this._report, e = Bt(t, b(this, o, x));
  return c`<uui-box class="tile">
      <div class="tile-label">This month</div>
      <div class="tile-value">${n(this, o, _).call(this, t.month.cost)}</div>
      <div class="tile-note">Projected ${n(this, o, _).call(this, t.month.projectedCost)} by month end</div>
      ${e.kind === "set" ? c`<div
              class="budget budget-${e.level}"
              role="meter"
              aria-valuemin="0"
              aria-valuemax="100"
              aria-valuenow=${Math.round(e.spentShare * 100)}
              aria-label="Share of the monthly budget used">
              <span class="budget-fill" style="width:${Math.min(100, e.spentShare * 100)}%"></span>
            </div>
            <div class="tile-note">${d(e.spentShare * 100)} % of ${rt(e.amount, b(this, o, x))}</div>` : e.kind === "unavailable" ? c`<div class="tile-note">${e.reason}</div>` : $}
    </uui-box>`;
};
_t = function() {
  const t = this._report, e = t.totals, s = t.previous?.totals, i = t.days.reduce((v, T) => v + T.inputTokens, 0), a = t.days.reduce((v, T) => v + T.outputTokens, 0), r = e.requests > 0 ? e.cost / e.requests : 0, u = s && s.requests > 0 && e.requests > 0 ? s.cost / s.requests : void 0;
  return c`<div class="tiles">
      ${n(this, o, kt).call(this)}
      <uui-box class="tile tile-hero">
        <div class="tile-label">Estimated cost</div>
        <div class="tile-value hero">${n(this, o, _).call(this, e.cost)}</div>
        ${e.unpricedTokens > 0 ? c`<div class="tile-note">
              <uui-icon name="icon-alert"></uui-icon> ${d(e.unpricedTokens)} tokens from models without a price
            </div>` : c`<div class="tile-note">${n(this, o, wt).call(this)}</div>`}
        ${n(this, o, S).call(this, e.cost, s?.cost)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Tokens</div>
        <div class="tile-value">${d(e.totalTokens, { compact: !0 })}</div>
        <div class="tile-note">${d(i, { compact: !0 })} in · ${d(a, { compact: !0 })} out</div>
        ${n(this, o, S).call(this, e.totalTokens, s?.totalTokens)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Requests</div>
        <div class="tile-value">${d(e.requests)}</div>
        <div class="tile-note">${d(e.requests / t.days.length)} per day on average</div>
        ${n(this, o, S).call(this, e.requests, s?.requests)}
      </uui-box>
      <uui-box class="tile">
        <div class="tile-label">Cost per request</div>
        <div class="tile-value">${e.requests > 0 ? n(this, o, _).call(this, r) : "–"}</div>
        <div class="tile-note">Priced models only</div>
        ${n(this, o, S).call(this, r, u)}
      </uui-box>
    </div>`;
};
wt = function() {
  const t = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" });
  return `${t.format(k(this._report.from))} – ${t.format(k(this._report.to))}`;
};
xt = function() {
  const t = this._report.days, e = t.map((l) => n(this, o, ft).call(this, l)), s = Qt(e.filter((l) => l > 0)), i = (k(t[0].date).getUTCDay() + 6) % 7, a = Math.ceil((i + t.length) / 7), r = B + a * (g + z), u = H + 7 * (g + z), v = new Intl.DateTimeFormat(void 0, { month: "short" }), T = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short", year: "numeric" }), R = new Intl.DateTimeFormat(void 0, { weekday: "short" }), q = [], A = t.map((l, y) => {
    const I = i + y, M = Math.floor(I / 7), L = I % 7, D = k(l.date);
    (D.getUTCDate() === 1 || y === 0) && q.push({ col: M, label: v.format(D) });
    const K = e[y], Y = K > 0 ? Zt(K, s) : -1;
    return C`<rect
        class="cell ${Y < 0 ? "cell-empty" : `cell-${Y}`}"
        x=${B + M * (g + z)}
        y=${H + L * (g + z)}
        width=${g}
        height=${g}
        rx="2"
        @pointerenter=${(Rt) => n(this, o, J).call(this, Rt, T.format(D), [
      { value: n(this, o, _).call(this, l.cost), label: "cost" },
      { value: d(l.totalTokens), label: "tokens" },
      { value: d(l.requests), label: l.requests === 1 ? "request" : "requests" }
    ])}
        @pointerleave=${b(this, O)}
      ></rect>`;
  }), W = q.filter((l, y) => y === 0 || l.col - q[y - 1].col >= 3);
  return c`<uui-box class="card" data-card="heatmap" headline="Usage calendar">
      <div slot="header-actions">
        <uui-button-group aria-label="Heatmap metric">
          ${Ft.map(
    (l) => c`<uui-button
              compact
              look=${l.key === this._metric ? "primary" : "secondary"}
              aria-pressed=${l.key === this._metric}
              @click=${() => n(this, o, vt).call(this, l.key)}
              >${l.label}</uui-button
            >`
  )}
        </uui-button-group>
      </div>
      <div class="heatmap-scroll">
        <svg
          class="heatmap"
          width=${r}
          height=${u}
          viewBox="0 0 ${r} ${u}"
          role="img"
          aria-label="Daily ${this._metric} over the selected period. The table under the cost chart lists every value.">
          ${W.map((l) => C`<text class="axis-label" x=${B + l.col * (g + z)} y="11">${l.label}</text>`)}
          ${[0, 2, 4].map((l) => {
    const y = R.format(new Date(Date.UTC(2024, 0, 1 + l)));
    return C`<text class="axis-label" x="0" y=${H + l * (g + z) + g - 2}>${y}</text>`;
  })}
          ${A}
        </svg>
      </div>
      <div class="scale-legend" aria-hidden="true">
        <span class="swatch cell-empty"></span><span>No usage</span>
        <span class="scale-gap"></span>
        <span>Less</span>
        ${[0, 1, 2, 3].map((l) => c`<span class="swatch cell-${l}"></span>`)}
        <span>More</span>
        ${s.length ? c`<span class="scale-range">up to ${n(this, o, yt).call(this, Math.max(...e))} a day</span>` : $}
      </div>
      ${n(this, o, Q).call(this, "heatmap")}
    </uui-box>`;
};
Tt = function() {
  const t = this._report.days.length > 92;
  return { buckets: t ? jt(this._report.days) : Ht(this._report.days), weekly: t };
};
zt = function() {
  const { buckets: t, weekly: e } = n(this, o, Tt).call(this), s = Math.max(0, ...t.map((l) => l.cost)), i = Kt(n(this, o, F).call(this, s)), a = i[i.length - 1] || 1, r = Math.max(200, this._chartWidth - w), u = r / t.length, v = Math.max(1, Math.min(oe, u - z)), T = (l) => p - n(this, o, F).call(this, l) / a * p, R = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short" }), q = new Intl.DateTimeFormat(void 0, { weekday: "short", day: "numeric", month: "short" }), A = Math.max(1, Math.ceil(t.length / Math.max(2, Math.floor(r / 90)))), W = s === 0;
  return c`<uui-box class="card" data-card="columns" headline=${e ? "Weekly cost" : "Daily cost"}>
      <div slot="header-actions" class="header-actions">
        <uui-button compact look="secondary" @click=${() => n(this, o, Mt).call(this, t, e)}>Download CSV</uui-button>
        <uui-button compact look="secondary" @click=${() => this._showTable = !this._showTable}>
          ${this._showTable ? "Show chart" : "Show as table"}
        </uui-button>
      </div>
      ${this._showTable ? n(this, o, Ct).call(this, t, e) : c`<div class="column-chart">
            ${W ? c`<p class="empty">
                  ${this._report.totals.unpricedTokens > 0 ? "No cost to show — the models used in this period have no configured price." : "No AI usage in this period."}
                </p>` : c`<svg
                  width=${w + r}
                  height=${p + ie}
                  role="img"
                  aria-label="${e ? "Weekly" : "Daily"} estimated cost. Use “Show as table” for the values.">
                  ${i.map(
    (l) => C`
                      <line class="grid" x1=${w} x2=${w + r} y1=${p - l / a * p} y2=${p - l / a * p}></line>
                      <text class="axis-label tick" x=${w - 8} y=${p - l / a * p + 4}>${new Intl.NumberFormat(void 0, {
      style: "currency",
      currency: b(this, o, x),
      notation: "compact",
      maximumFractionDigits: l < 10 ? 2 : 0
    }).format(l)}</text>`
  )}
                  ${t.map((l, y) => {
    const I = w + y * u + (u - v) / 2, M = p - T(l.cost), L = e ? `Week of ${R.format(k(l.start))}` : q.format(k(l.start));
    return C`
                      <rect class="hit" x=${w + y * u} y="0" width=${u} height=${p}
                        @pointerenter=${(D) => n(this, o, J).call(this, D, L, [
      { value: n(this, o, _).call(this, l.cost), label: "cost" },
      { value: d(l.totalTokens), label: "tokens" },
      { value: d(l.requests), label: l.requests === 1 ? "request" : "requests" }
    ])}
                        @pointerleave=${b(this, O)}></rect>
                      ${M > 0 ? C`<path class="bar" d=${Yt(I, p, v, M)}></path>` : $}
                      ${y % A === 0 ? C`<text class="axis-label" x=${I + v / 2} y=${p + 18} text-anchor="middle">${R.format(k(l.start))}</text>` : $}`;
  })}
                  <line class="baseline" x1=${w} x2=${w + r} y1=${p} y2=${p}></line>
                </svg>`}
          </div>`}
      ${n(this, o, Q).call(this, "columns")}
    </uui-box>`;
};
Ct = function(t, e) {
  const s = new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }), i = t.filter((a) => a.requests > 0).reverse();
  return i.length ? c`<div class="table-scroll">
      <uui-table aria-label="${e ? "Weekly" : "Daily"} usage and cost">
        <uui-table-head>
          <uui-table-head-cell>${e ? "Week of" : "Date"}</uui-table-head-cell>
          <uui-table-head-cell class="num">Requests</uui-table-head-cell>
          <uui-table-head-cell class="num">Input tokens</uui-table-head-cell>
          <uui-table-head-cell class="num">Output tokens</uui-table-head-cell>
          <uui-table-head-cell class="num">Cost</uui-table-head-cell>
        </uui-table-head>
        ${i.map(
    (a) => c`<uui-table-row>
            <uui-table-cell>${s.format(k(a.start))}</uui-table-cell>
            <uui-table-cell class="num">${d(a.requests)}</uui-table-cell>
            <uui-table-cell class="num">${d(a.inputTokens)}</uui-table-cell>
            <uui-table-cell class="num">${d(a.outputTokens)}</uui-table-cell>
            <uui-table-cell class="num">${n(this, o, _).call(this, a.cost)}</uui-table-cell>
          </uui-table-row>`
  )}
      </uui-table>
    </div>` : c`<p class="empty">No AI usage in this period.</p>`;
};
qt = function() {
  return this._dimension === "model" ? this._report.models.map((t) => ({ ...t, name: t.modelId })) : this._breakdowns.get(`${this._dimension}:${this._days}`)?.rows;
};
It = function() {
  const t = tt.find((a) => a.key === this._dimension).label, e = n(this, o, qt).call(this), s = this._report.totals.cost, i = this._dimension === "model" ? "Add this model to Esatto:AICosts:PricesPerMillionTokens in appsettings" : "Some of this usage is from models without a configured price";
  return c`<uui-box class="card" headline="Cost by">
      <div slot="header-actions" class="header-actions">
        <uui-button-group aria-label="Group cost by">
          ${tt.map(
    (a) => c`<uui-button
              compact
              look=${a.key === this._dimension ? "primary" : "secondary"}
              aria-pressed=${a.key === this._dimension}
              @click=${() => n(this, o, bt).call(this, a.key)}
              >${a.label}</uui-button
            >`
  )}
        </uui-button-group>
        <uui-button compact look="secondary" ?disabled=${!e?.length} @click=${() => n(this, o, Dt).call(this, t, e ?? [])}
          >Download CSV</uui-button
        >
      </div>
      ${this._breakdownError && this._dimension !== "model" ? c`<p class="error">${this._breakdownError}</p>` : e ? e.length === 0 ? c`<p class="empty">No AI usage in this period.</p>` : c`<div class="table-scroll">
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
    (a) => c`<uui-table-row>
                      <uui-table-cell>${a.name}</uui-table-cell>
                      <uui-table-cell class="num">${d(a.requests)}</uui-table-cell>
                      <uui-table-cell class="num">${d(a.inputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${d(a.cachedInputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">${d(a.outputTokens)}</uui-table-cell>
                      <uui-table-cell class="num">
                        ${a.cost === null ? c`<uui-tag look="secondary" title=${i}>No price</uui-tag>` : n(this, o, _).call(this, a.cost)}
                      </uui-table-cell>
                      <uui-table-cell class="num">
                        ${a.cost !== null && s > 0 ? c`<span class="share"
                              ><span class="share-track"
                                ><span class="share-fill" style="width:${a.cost / s * 100}%"></span></span
                              >${d(a.cost / s * 100)}%</span
                            >` : "–"}
                      </uui-table-cell>
                    </uui-table-row>`
  )}
                </uui-table>
              </div>` : c`<uui-loader-bar></uui-loader-bar>`}
      ${this._breakdownLoading && e ? c`<uui-loader-bar></uui-loader-bar>` : $}
    </uui-box>`;
};
Z = function(t) {
  return Number(n(this, o, F).call(this, t).toFixed(4));
};
Mt = function(t, e) {
  const s = this._report, i = [e ? "Week of" : "Date", "Requests", "Input tokens", "Cached input tokens", "Output tokens", "Total tokens", `Cost (${b(this, o, x)})`, "Unpriced tokens"], a = t.map((r) => [r.start, r.requests, r.inputTokens, r.cachedInputTokens, r.outputTokens, r.totalTokens, n(this, o, Z).call(this, r.cost), r.unpricedTokens]);
  ct(`ai-costs-${s.from}-${s.to}.csv`, lt(i, a));
};
Dt = function(t, e) {
  const s = this._report, i = [t, "Requests", "Input tokens", "Cached input tokens", "Output tokens", "Total tokens", `Cost (${b(this, o, x)})`], a = e.map((r) => [r.name, r.requests, r.inputTokens, r.cachedInputTokens, r.outputTokens, r.totalTokens, r.cost === null ? "" : n(this, o, Z).call(this, r.cost)]);
  ct(`ai-costs-${s.from}-${s.to}-${this._dimension}.csv`, lt(i, a));
};
Et = function() {
  const t = this._report, e = t.rates ? new Intl.DateTimeFormat(void 0, { day: "numeric", month: "short", year: "numeric" }).format(k(t.rates.date)) : null;
  return c`<p class="footnote">
      Estimates: tokens × list prices per model${t.pricesSource ? c` (${t.pricesSource})` : $}, in ${t.baseCurrency}.
      Cached input tokens are billed at the cache-read price. Excludes taxes, discounts and provider surcharges.
      ${t.rates ? c`Converted with ${t.rates.source} reference rates of ${e}.` : c`<strong>Exchange rates are unavailable right now — amounts are shown in ${t.baseCurrency}.</strong>`}
      Times are UTC.
    </p>`;
};
h.styles = [te];
m([
  f()
], h.prototype, "_report", 2);
m([
  f()
], h.prototype, "_loading", 2);
m([
  f()
], h.prototype, "_error", 2);
m([
  f()
], h.prototype, "_days", 2);
m([
  f()
], h.prototype, "_currency", 2);
m([
  f()
], h.prototype, "_metric", 2);
m([
  f()
], h.prototype, "_showTable", 2);
m([
  f()
], h.prototype, "_tooltip", 2);
m([
  f()
], h.prototype, "_dark", 2);
m([
  f()
], h.prototype, "_chartWidth", 2);
m([
  f()
], h.prototype, "_dimension", 2);
m([
  f()
], h.prototype, "_breakdowns", 2);
m([
  f()
], h.prototype, "_breakdownLoading", 2);
m([
  f()
], h.prototype, "_breakdownError", 2);
h = m([
  Nt("esatto-ai-costs-view")
], h);
const ue = h;
export {
  h as EsattoAICostsViewElement,
  ue as default
};
//# sourceMappingURL=ai-costs-view.js.map
