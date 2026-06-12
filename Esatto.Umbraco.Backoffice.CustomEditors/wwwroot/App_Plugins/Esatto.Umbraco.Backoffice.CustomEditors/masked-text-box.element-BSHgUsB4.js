import { LitElement as f, html as h, css as y, property as v, state as d, customElement as g } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as w } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent as b } from "@umbraco-cms/backoffice/event";
var k = Object.defineProperty, x = Object.getOwnPropertyDescriptor, m = (t) => {
  throw TypeError(t);
}, i = (t, e, a, n) => {
  for (var s = n > 1 ? void 0 : n ? x(e, a) : e, u = t.length - 1, p; u >= 0; u--)
    (p = t[u]) && (s = (n ? p(e, a, s) : p(s)) || s);
  return n && s && k(e, a, s), s;
}, E = (t, e, a) => e.has(t) || m("Cannot " + a), $ = (t, e, a) => e.has(t) ? m("Cannot add the same private member more than once") : e instanceof WeakSet ? e.add(t) : e.set(t, a), l = (t, e, a) => (E(t, e, "access private method"), a), r, c, _;
let o = class extends w(f) {
  constructor() {
    super(...arguments), $(this, r), this.value = "", this._mask = !0, this._reveal = !1;
  }
  set config(t) {
    const e = t?.getValueByAlias("mask");
    this._mask = e === void 0 ? !0 : !!e;
  }
  // Belt-and-suspenders against password-manager extensions: the attributes they
  // honour (1Password, LastPass, Dashlane) must sit on the *native* <input>, which
  // uui-input renders inside its own shadow root. Tag it once it exists.
  async firstUpdated() {
    const t = this.shadowRoot?.querySelector("uui-input");
    await t?.updateComplete;
    const e = t?.shadowRoot?.querySelector("input");
    e && (e.setAttribute("data-1p-ignore", "true"), e.setAttribute("data-lpignore", "true"), e.setAttribute("data-form-type", "other"), e.setAttribute("autocomplete", "new-password"));
  }
  render() {
    const t = this._mask && !this._reveal ? "password" : "text", e = this._reveal ? "Hide value" : "Show value";
    return h`
      <uui-input
        type=${t}
        .value=${this.value ?? ""}
        name="bce-masked-value"
        autocomplete="new-password"
        spellcheck="false"
        data-1p-ignore="true"
        data-lpignore="true"
        data-form-type="other"
        @input=${l(this, r, c)}
        @change=${l(this, r, c)}
      >
        ${this._mask ? h`<uui-button
              slot="append"
              compact
              look="default"
              label=${e}
              title=${e}
              @click=${l(this, r, _)}
            >
              <uui-icon name=${this._reveal ? "icon-eye-off" : "icon-eye"}></uui-icon>
            </uui-button>` : ""}
      </uui-input>
    `;
  }
};
r = /* @__PURE__ */ new WeakSet();
c = function(t) {
  this.value = t.target.value, this.dispatchEvent(new b());
};
_ = function(t) {
  t.preventDefault(), t.stopPropagation(), this._reveal = !this._reveal;
};
o.styles = y`
    :host {
      display: block;
    }
    uui-input {
      width: 100%;
    }
  `;
i([
  v({ type: String })
], o.prototype, "value", 2);
i([
  d()
], o.prototype, "_mask", 2);
i([
  d()
], o.prototype, "_reveal", 2);
i([
  v({ attribute: !1 })
], o.prototype, "config", 1);
o = i([
  g("bce-masked-text-box")
], o);
export {
  o as default
};
//# sourceMappingURL=masked-text-box.element-BSHgUsB4.js.map
