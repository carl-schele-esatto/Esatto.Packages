import { LitElement as V, html as f, css as N, property as g, state as T, customElement as I } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as K } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent as G } from "@umbraco-cms/backoffice/event";
import { UMB_VALIDATION_CONTEXT as H } from "@umbraco-cms/backoffice/validation";
function X(t) {
  if (t && typeof t == "object") {
    const e = t;
    return {
      from: typeof e.from == "string" ? e.from : null,
      to: typeof e.to == "string" ? e.to : null
    };
  }
  return { from: null, to: null };
}
function j(t) {
  return t.from === null || t.to === null ? !0 : new Date(t.from).getTime() <= new Date(t.to).getTime();
}
function B(t, e) {
  const a = t.to !== null && new Date(t.to).getTime() < new Date(e).getTime();
  return { from: e, to: a ? null : t.to };
}
function Y(t, e) {
  return { from: t.from, to: e };
}
function C(t) {
  const e = t.getFullYear(), a = String(t.getMonth() + 1).padStart(2, "0"), o = String(t.getDate()).padStart(2, "0");
  return `${e}-${a}-${o}`;
}
function W(t) {
  return t.slice(0, 10);
}
function q(t, e) {
  const o = (new Date(t, e, 1).getDay() + 6) % 7, r = new Date(t, e, 1 - o), u = [], l = new Date(r);
  do
    for (let A = 0; A < 7; A++)
      u.push({
        key: C(l),
        day: l.getDate(),
        inCurrentMonth: l.getMonth() === e
      }), l.setDate(l.getDate() + 1);
  while (l.getMonth() === e || l < new Date(t, e, 1));
  return u;
}
function Q(t, e, a) {
  return e !== null && t < W(e) || a !== null && t > W(a);
}
var Z = Object.defineProperty, tt = Object.getOwnPropertyDescriptor, F = (t) => {
  throw TypeError(t);
}, v = (t, e, a, o) => {
  for (var r = o > 1 ? void 0 : o ? tt(e, a) : e, u = t.length - 1, l; u >= 0; u--)
    (l = t[u]) && (r = (o ? l(e, a, r) : l(r)) || r);
  return o && r && Z(e, a, r), r;
}, et = (t, e, a) => e.has(t) || F("Cannot " + a), at = (t, e, a) => e.has(t) ? F("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, a), M = (t, e, a) => (et(t, e, "access private method"), a), y, R, U, J;
const it = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"], nt = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December"
];
let d = class extends K(V) {
  constructor() {
    super(...arguments), at(this, y), this.value = null, this.min = null, this.max = null, this.disabled = !1, this._viewYear = (/* @__PURE__ */ new Date()).getFullYear(), this._viewMonth = (/* @__PURE__ */ new Date()).getMonth();
  }
  willUpdate(t) {
    if (t.has("value") || t.has("min")) {
      const e = this.value ? new Date(this.value) : this.min ? new Date(this.min) : /* @__PURE__ */ new Date();
      this._viewYear = e.getFullYear(), this._viewMonth = e.getMonth();
    }
  }
  render() {
    const t = q(this._viewYear, this._viewMonth), e = this.value ? C(new Date(this.value)) : null;
    return f`
      <div class="header">
        <uui-button
          compact
          look="secondary"
          label="Previous month"
          ?disabled=${this.disabled}
          @click=${M(this, y, R)}
        >‹</uui-button>
        <span class="title">${nt[this._viewMonth]} ${this._viewYear}</span>
        <uui-button
          compact
          look="secondary"
          label="Next month"
          ?disabled=${this.disabled}
          @click=${M(this, y, U)}
        >›</uui-button>
      </div>

      <div class="grid ${this.disabled ? "dimmed" : ""}">
        ${it.map((a) => f`<span class="weekday">${a}</span>`)}
        ${t.map((a) => {
      const o = this.disabled || !a.inCurrentMonth || Q(a.key, this.min, this.max), r = a.key === e;
      return f`
            <button
              class="day ${r ? "selected" : ""} ${a.inCurrentMonth ? "" : "muted"}"
              ?disabled=${o}
              @click=${() => M(this, y, J).call(this, a.key)}
            >
              ${a.day}
            </button>
          `;
    })}
      </div>
    `;
  }
};
y = /* @__PURE__ */ new WeakSet();
R = function() {
  this._viewMonth === 0 ? (this._viewMonth = 11, this._viewYear -= 1) : this._viewMonth -= 1;
};
U = function() {
  this._viewMonth === 11 ? (this._viewMonth = 0, this._viewYear += 1) : this._viewMonth += 1;
};
J = function(t) {
  const e = this.value ? C(new Date(this.value)) : null;
  this.value = e === t ? null : t, this.dispatchEvent(new CustomEvent("change", { bubbles: !0, composed: !0 }));
};
d.styles = N`
    :host {
      display: block;
      border: 1px solid var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
      padding: var(--uui-size-space-3, 9px);
      background: var(--uui-color-surface, #fff);
      min-width: 16rem;
    }
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: var(--uui-size-space-2, 6px);
    }
    .title {
      font-weight: bold;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(7, 1fr);
      gap: 2px;
    }
    .grid.dimmed {
      opacity: 0.5;
    }
    .weekday {
      text-align: center;
      font-size: 0.75rem;
      color: var(--uui-color-text-alt, #68676b);
      padding: 2px 0;
    }
    .day {
      aspect-ratio: 1;
      border: none;
      background: transparent;
      border-radius: var(--uui-border-radius, 3px);
      cursor: pointer;
      color: inherit;
      font: inherit;
    }
    .day:hover:not(:disabled) {
      /* light blue tint of the selected colour, clearly visible (was faint gray) */
      background: #c5cdf5;
    }
    .day:disabled {
      color: var(--uui-color-disabled-contrast, #c4c4c4);
      cursor: not-allowed;
    }
    .day.muted {
      visibility: hidden;
    }
    .day.selected {
      background: var(--uui-color-selected, #3544b1);
      color: var(--uui-color-selected-contrast, #fff);
    }
    /* keep the selected day blue + readable on hover (don't let the
       generic hover background hide its white text) */
    .day.selected:hover:not(:disabled) {
      background: #283596;
      color: var(--uui-color-selected-contrast, #fff);
    }
  `;
v([
  g({ type: String })
], d.prototype, "value", 2);
v([
  g({ type: String })
], d.prototype, "min", 2);
v([
  g({ type: String })
], d.prototype, "max", 2);
v([
  g({ type: Boolean })
], d.prototype, "disabled", 2);
v([
  T()
], d.prototype, "_viewYear", 2);
v([
  T()
], d.prototype, "_viewMonth", 2);
d = v([
  I("bo-date-range-calendar")
], d);
var rt = Object.defineProperty, st = Object.getOwnPropertyDescriptor, L = (t) => {
  throw TypeError(t);
}, $ = (t, e, a, o) => {
  for (var r = o > 1 ? void 0 : o ? st(e, a) : e, u = t.length - 1, l; u >= 0; u--)
    (l = t[u]) && (r = (o ? l(e, a, r) : l(r)) || r);
  return o && r && rt(e, a, r), r;
}, O = (t, e, a) => e.has(t) || L("Cannot " + a), s = (t, e, a) => (O(t, e, "read from private field"), a ? a.call(t) : e.get(t)), m = (t, e, a) => e.has(t) ? L("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, a), ot = (t, e, a, o) => (O(t, e, "write to private field"), e.set(t, a), a), n = (t, e, a) => (O(t, e, "access private method"), a), D, i, c, h, P, _, b, w, x, E, k, S;
const z = "backoffice-date-range-invalid";
let p = class extends K(V) {
  constructor() {
    super(), m(this, i), this.value = null, this._config = { includeTime: !1, minDate: null, maxDate: null }, m(this, D), m(this, x, (t) => {
      const e = t.target.value;
      if (!e) {
        n(this, i, h).call(this, { from: null, to: null });
        return;
      }
      const a = n(this, i, _).call(this, e, n(this, i, w).call(this, s(this, i, c).from));
      n(this, i, h).call(this, B(s(this, i, c), a));
    }), m(this, E, (t) => {
      if (!s(this, i, c).from) return;
      const e = t.target.value;
      if (!e) {
        n(this, i, h).call(this, { from: s(this, i, c).from, to: null });
        return;
      }
      const a = n(this, i, _).call(this, e, n(this, i, w).call(this, s(this, i, c).to));
      n(this, i, h).call(this, Y(s(this, i, c), a));
    }), m(this, k, (t) => {
      const e = t.target.value, a = n(this, i, b).call(this, s(this, i, c).from);
      a && n(this, i, h).call(this, B(s(this, i, c), n(this, i, _).call(this, a, e)));
    }), m(this, S, (t) => {
      const e = t.target.value, a = n(this, i, b).call(this, s(this, i, c).to);
      a && n(this, i, h).call(this, Y(s(this, i, c), n(this, i, _).call(this, a, e)));
    }), this.consumeContext(H, (t) => {
      ot(this, D, t), n(this, i, P).call(this);
    });
  }
  set config(t) {
    this._config = {
      includeTime: t?.getValueByAlias("includeTime") ?? !1,
      minDate: t?.getValueByAlias("minDate") ?? null,
      maxDate: t?.getValueByAlias("maxDate") ?? null
    };
  }
  render() {
    const t = s(this, i, c), e = t.from ?? this._config.minDate;
    return f`
      <div class="calendars">
        <div class="cal-col">
          <span class="label">Start</span>
          <bo-date-range-calendar
            .value=${n(this, i, b).call(this, t.from)}
            .min=${this._config.minDate}
            .max=${this._config.maxDate}
            @change=${s(this, x)}
          ></bo-date-range-calendar>
          ${this._config.includeTime && t.from ? f`<input
                type="time"
                .value=${n(this, i, w).call(this, t.from)}
                @change=${s(this, k)}
              />` : null}
        </div>

        <div class="cal-col">
          <span class="label">End</span>
          <bo-date-range-calendar
            .value=${n(this, i, b).call(this, t.to)}
            .min=${e}
            .max=${this._config.maxDate}
            ?disabled=${!t.from}
            @change=${s(this, E)}
          ></bo-date-range-calendar>
          ${t.from ? null : f`<span class="hint">Select a start date first.</span>`}
          ${this._config.includeTime && t.to ? f`<input
                type="time"
                .value=${n(this, i, w).call(this, t.to)}
                @change=${s(this, S)}
              />` : null}
        </div>
      </div>
    `;
  }
};
D = /* @__PURE__ */ new WeakMap();
i = /* @__PURE__ */ new WeakSet();
c = function() {
  return X(this.value);
};
h = function(t) {
  this.value = t.from === null && t.to === null ? null : t, this.dispatchEvent(new G()), n(this, i, P).call(this);
};
P = function() {
  const t = s(this, D);
  t && (t.messages.removeMessageByKey(z), j(s(this, i, c)) || t.messages.addMessage(
    "client",
    "$",
    "The end date cannot be before the start date.",
    z
  ));
};
_ = function(t, e) {
  return this._config.includeTime ? `${t}T${e && e.length ? e : "00:00"}:00` : t;
};
b = function(t) {
  return t ? t.slice(0, 10) : null;
};
w = function(t) {
  return !t || t.length < 16 ? "00:00" : t.slice(11, 16);
};
x = /* @__PURE__ */ new WeakMap();
E = /* @__PURE__ */ new WeakMap();
k = /* @__PURE__ */ new WeakMap();
S = /* @__PURE__ */ new WeakMap();
p.styles = N`
    :host {
      display: block;
    }
    .calendars {
      display: flex;
      gap: var(--uui-size-layout-1, 24px);
      flex-wrap: wrap;
    }
    .cal-col {
      display: flex;
      flex-direction: column;
      gap: var(--uui-size-space-2, 6px);
    }
    .label {
      font-weight: bold;
    }
    .hint {
      font-size: 0.8rem;
      color: var(--uui-color-text-alt, #68676b);
    }
    input[type="time"] {
      padding: var(--uui-size-space-2, 6px);
      border: 1px solid var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
    }
  `;
$([
  g({ type: Object })
], p.prototype, "value", 2);
$([
  T()
], p.prototype, "_config", 2);
$([
  g({ attribute: !1 })
], p.prototype, "config", 1);
p = $([
  I("bo-date-range")
], p);
const ht = p;
export {
  p as BackofficeDateRangeEditorElement,
  ht as default
};
//# sourceMappingURL=date-range.element-Dj37r0-E.js.map
