import { LitElement as L, html as f, css as F, property as D, state as B, customElement as U } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as J } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent as et } from "@umbraco-cms/backoffice/event";
import { UMB_VALIDATION_CONTEXT as at } from "@umbraco-cms/backoffice/validation";
const q = { from: null, to: null };
function it(t) {
  if (t && typeof t == "object") {
    const e = t;
    return {
      from: typeof e.from == "string" ? e.from : null,
      to: typeof e.to == "string" ? e.to : null
    };
  }
  return { ...q };
}
function nt(t) {
  return t.from === null || t.to === null ? !0 : new Date(t.from).getTime() <= new Date(t.to).getTime();
}
function V(t, e) {
  const a = t.to !== null && new Date(t.to).getTime() < new Date(e).getTime();
  return { from: e, to: a ? null : t.to };
}
function R(t, e) {
  return { from: t.from, to: e };
}
function C(t) {
  const e = t.getFullYear(), a = String(t.getMonth() + 1).padStart(2, "0"), n = String(t.getDate()).padStart(2, "0");
  return `${e}-${a}-${n}`;
}
function w(t) {
  return t ? t.slice(0, 10) : null;
}
function k(t) {
  const [e, a, n] = t.slice(0, 10).split("-").map(Number);
  return new Date(e, a - 1, n);
}
function rt(t, e) {
  const a = new Date(t, e, 1), n = a.getTime(), i = (a.getDay() + 6) % 7, l = new Date(t, e, 1 - i), s = [], c = new Date(l);
  do
    for (let p = 0; p < 7; p++)
      s.push({
        key: C(c),
        day: c.getDate(),
        inCurrentMonth: c.getMonth() === e
      }), c.setDate(c.getDate() + 1);
  while (c.getMonth() === e || c.getTime() < n);
  return s;
}
function st(t, e, a) {
  const n = w(e), i = w(a);
  return n !== null && t < n || i !== null && t > i;
}
var ot = Object.defineProperty, lt = Object.getOwnPropertyDescriptor, H = (t) => {
  throw TypeError(t);
}, y = (t, e, a, n) => {
  for (var i = n > 1 ? void 0 : n ? lt(e, a) : e, l = t.length - 1, s; l >= 0; l--)
    (s = t[l]) && (i = (n ? s(e, a, i) : s(i)) || i);
  return n && i && ot(e, a, i), i;
}, X = (t, e, a) => e.has(t) || H("Cannot " + a), G = (t, e, a) => (X(t, e, "read from private field"), a ? a.call(t) : e.get(t)), S = (t, e, a) => e.has(t) ? H("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, a), _ = (t, e, a) => (X(t, e, "access private method"), a), h, Q, Z, j, x, Y, A;
const ct = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"], dt = [
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
let u = class extends J(L) {
  constructor() {
    super(...arguments), S(this, h), this.value = null, this.min = null, this.max = null, this.disabled = !1, this._viewYear = (/* @__PURE__ */ new Date()).getFullYear(), this._viewMonth = (/* @__PURE__ */ new Date()).getMonth(), this._todayKey = C(/* @__PURE__ */ new Date()), this._grid = [], S(this, Y, (t) => {
      const e = t.target?.closest(
        "button.day"
      );
      if (!e || e.disabled) return;
      const a = e.dataset.key;
      a && _(this, h, j).call(this, a);
    }), S(this, A, (t) => {
      let e = 0;
      switch (t.key) {
        case "ArrowLeft":
          e = -1;
          break;
        case "ArrowRight":
          e = 1;
          break;
        case "ArrowUp":
          e = -7;
          break;
        case "ArrowDown":
          e = 7;
          break;
        default:
          return;
      }
      t.preventDefault();
      const n = t.target?.closest(
        "button.day"
      )?.dataset.key ?? this.value ?? this._grid.find((p) => p.inCurrentMonth)?.key;
      if (!n) return;
      const i = k(n);
      i.setDate(i.getDate() + e);
      const l = C(i), s = i.getFullYear(), c = i.getMonth();
      (s !== this._viewYear || c !== this._viewMonth) && (this._viewYear = s, this._viewMonth = c), this.updateComplete.then(() => {
        this.shadowRoot?.querySelector(
          `button.day[data-key="${l}"]`
        )?.focus();
      });
    });
  }
  willUpdate(t) {
    if (t.has("value") || t.has("min")) {
      const e = this.value ? k(this.value) : this.min ? k(this.min) : /* @__PURE__ */ new Date();
      this._viewYear = e.getFullYear(), this._viewMonth = e.getMonth();
    }
    (t.has("_viewYear") || t.has("_viewMonth") || this._grid.length === 0) && (this._grid = rt(this._viewYear, this._viewMonth));
  }
  render() {
    const t = this._grid, e = this.value, a = `${dt[this._viewMonth]} ${this._viewYear}`, n = t.find((i) => i.key === e && !_(this, h, x).call(this, i))?.key ?? t.find((i) => i.inCurrentMonth && !_(this, h, x).call(this, i))?.key ?? null;
    return f`
      <div class="header">
        <uui-button
          compact
          look="secondary"
          label="Previous month"
          ?disabled=${this.disabled}
          @click=${_(this, h, Q)}
        >‹</uui-button>
        <span class="title">${a}</span>
        <uui-button
          compact
          look="secondary"
          label="Next month"
          ?disabled=${this.disabled}
          @click=${_(this, h, Z)}
        >›</uui-button>
      </div>

      <div
        class="grid ${this.disabled ? "dimmed" : ""}"
        role="grid"
        aria-label=${a}
        @click=${G(this, Y)}
        @keydown=${G(this, A)}
      >
        ${ct.map((i) => f`<span class="weekday">${i}</span>`)}
        ${t.map((i) => {
      const l = _(this, h, x).call(this, i), s = i.key === e, c = i.key === this._todayKey;
      if (!i.inCurrentMonth)
        return f`<button class="day muted" disabled aria-hidden="true" tabindex="-1">
              ${i.day}
            </button>`;
      const p = k(i.key).toLocaleDateString(void 0, {
        dateStyle: "full"
      });
      return f`
            <button
              class="day ${s ? "selected" : ""}"
              data-key=${i.key}
              ?disabled=${l}
              aria-label=${p}
              aria-selected=${s ? "true" : "false"}
              aria-current=${c ? "date" : "false"}
              tabindex=${i.key === n ? "0" : "-1"}
            >
              ${i.day}
            </button>
          `;
    })}
      </div>
    `;
  }
};
h = /* @__PURE__ */ new WeakSet();
Q = function() {
  this._viewMonth === 0 ? (this._viewMonth = 11, this._viewYear -= 1) : this._viewMonth -= 1;
};
Z = function() {
  this._viewMonth === 11 ? (this._viewMonth = 0, this._viewYear += 1) : this._viewMonth += 1;
};
j = function(t) {
  const e = this.value;
  this.value = e === t ? null : t, this.dispatchEvent(new CustomEvent("change", { bubbles: !0, composed: !0 }));
};
x = function(t) {
  return this.disabled || !t.inCurrentMonth || st(t.key, this.min, this.max);
};
Y = /* @__PURE__ */ new WeakMap();
A = /* @__PURE__ */ new WeakMap();
u.styles = F`
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
y([
  D({ type: String })
], u.prototype, "value", 2);
y([
  D({ type: String })
], u.prototype, "min", 2);
y([
  D({ type: String })
], u.prototype, "max", 2);
y([
  D({ type: Boolean })
], u.prototype, "disabled", 2);
y([
  B()
], u.prototype, "_viewYear", 2);
y([
  B()
], u.prototype, "_viewMonth", 2);
u = y([
  U("esatto-date-range-calendar")
], u);
var ut = Object.defineProperty, ht = Object.getOwnPropertyDescriptor, tt = (t) => {
  throw TypeError(t);
}, T = (t, e, a, n) => {
  for (var i = n > 1 ? void 0 : n ? ht(e, a) : e, l = t.length - 1, s; l >= 0; l--)
    (s = t[l]) && (i = (n ? s(e, a, i) : s(i)) || i);
  return n && i && ut(e, a, i), i;
}, N = (t, e, a) => e.has(t) || tt("Cannot " + a), d = (t, e, a) => (N(t, e, "read from private field"), a ? a.call(t) : e.get(t)), b = (t, e, a) => e.has(t) ? tt("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, a), ft = (t, e, a, n) => (N(t, e, "write to private field"), e.set(t, a), a), o = (t, e, a) => (N(t, e, "access private method"), a), E, r, m, v, z, $, M, O, K, P, W;
const I = "esatto-date-range-invalid";
let g = class extends J(L) {
  constructor() {
    super(), b(this, r), this.value = null, this._config = { includeTime: !1, minDate: null, maxDate: null }, b(this, E), b(this, O, (t) => {
      const e = d(this, r, m), a = t.target.value;
      if (!a) {
        o(this, r, v).call(this, { ...q });
        return;
      }
      const n = o(this, r, $).call(this, a, o(this, r, M).call(this, e.from));
      o(this, r, v).call(this, V(e, n));
    }), b(this, K, (t) => {
      const e = d(this, r, m);
      if (!e.from) return;
      const a = t.target.value;
      if (!a) {
        o(this, r, v).call(this, { from: e.from, to: null });
        return;
      }
      const n = o(this, r, $).call(this, a, o(this, r, M).call(this, e.to));
      o(this, r, v).call(this, R(e, n));
    }), b(this, P, (t) => {
      const e = d(this, r, m), a = t.target.value, n = w(e.from);
      n && o(this, r, v).call(this, V(e, o(this, r, $).call(this, n, a)));
    }), b(this, W, (t) => {
      const e = d(this, r, m), a = t.target.value, n = w(e.to);
      n && o(this, r, v).call(this, R(e, o(this, r, $).call(this, n, a)));
    }), this.consumeContext(at, (t) => {
      ft(this, E, t), o(this, r, z).call(this);
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
    const t = d(this, r, m), e = t.from ?? this._config.minDate;
    return f`
      <div class="calendars">
        <div class="cal-col">
          <span class="label">Start</span>
          <esatto-date-range-calendar
            .value=${w(t.from)}
            .min=${this._config.minDate}
            .max=${this._config.maxDate}
            @change=${d(this, O)}
          ></esatto-date-range-calendar>
          ${this._config.includeTime && t.from ? f`<input
                type="time"
                aria-label="Start time"
                .value=${o(this, r, M).call(this, t.from)}
                @change=${d(this, P)}
              />` : null}
        </div>

        <div class="cal-col">
          <span class="label">End</span>
          <esatto-date-range-calendar
            .value=${w(t.to)}
            .min=${e}
            .max=${this._config.maxDate}
            ?disabled=${!t.from}
            @change=${d(this, K)}
          ></esatto-date-range-calendar>
          ${t.from ? null : f`<span class="hint">Select a start date first.</span>`}
          ${this._config.includeTime && t.to ? f`<input
                type="time"
                aria-label="End time"
                .value=${o(this, r, M).call(this, t.to)}
                @change=${d(this, W)}
              />` : null}
        </div>
      </div>
    `;
  }
};
E = /* @__PURE__ */ new WeakMap();
r = /* @__PURE__ */ new WeakSet();
m = function() {
  return it(this.value);
};
v = function(t) {
  this.value = t.from === null && t.to === null ? null : t, this.dispatchEvent(new et()), o(this, r, z).call(this);
};
z = function() {
  const t = d(this, E);
  t && (t.messages.removeMessageByKey(I), nt(d(this, r, m)) || t.messages.addMessage(
    "client",
    "$",
    "The end date cannot be before the start date.",
    I
  ));
};
$ = function(t, e) {
  return this._config.includeTime ? `${t}T${e && e.length ? e : "00:00"}:00` : t;
};
M = function(t) {
  return !t || t.length < 16 ? "00:00" : t.slice(11, 16);
};
O = /* @__PURE__ */ new WeakMap();
K = /* @__PURE__ */ new WeakMap();
P = /* @__PURE__ */ new WeakMap();
W = /* @__PURE__ */ new WeakMap();
g.styles = F`
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
T([
  D({ type: Object })
], g.prototype, "value", 2);
T([
  B()
], g.prototype, "_config", 2);
T([
  D({ attribute: !1 })
], g.prototype, "config", 1);
g = T([
  U("esatto-date-range")
], g);
const yt = g;
export {
  g as BackofficeDateRangeEditorElement,
  yt as default
};
//# sourceMappingURL=date-range.element-DkIb_LWs.js.map
