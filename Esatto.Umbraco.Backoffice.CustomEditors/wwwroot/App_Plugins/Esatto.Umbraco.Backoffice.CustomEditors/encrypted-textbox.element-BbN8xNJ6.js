import { LitElement as f, html as l, css as y, property as h, state as v, customElement as w } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as g } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent as b } from "@umbraco-cms/backoffice/event";
var E = Object.defineProperty, x = Object.getOwnPropertyDescriptor, d = (e) => {
  throw TypeError(e);
}, s = (e, t, a, i) => {
  for (var r = i > 1 ? void 0 : i ? x(t, a) : t, u = e.length - 1, p; u >= 0; u--)
    (p = e[u]) && (r = (i ? p(t, a, r) : p(r)) || r);
  return i && r && E(t, a, r), r;
}, k = (e, t, a) => t.has(e) || d("Cannot " + a), $ = (e, t, a) => t.has(e) ? d("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, a), c = (e, t, a) => (k(e, t, "access private method"), a), n, m, _;
let o = class extends g(f) {
  constructor() {
    super(...arguments), $(this, n), this.value = "", this._mask = !0, this._reveal = !1;
  }
  set config(e) {
    const t = e?.getValueByAlias("mask");
    this._mask = t === void 0 ? !0 : !!t;
  }
  // Belt-and-suspenders against password-manager extensions: the attributes they
  // honour (1Password, LastPass, Dashlane) must sit on the *native* <input>, which
  // uui-input renders inside its own shadow root. Tag it once it exists.
  async firstUpdated() {
    const e = this.shadowRoot?.querySelector("uui-input");
    await e?.updateComplete;
    const t = e?.shadowRoot?.querySelector("input");
    t && (t.setAttribute("data-1p-ignore", "true"), t.setAttribute("data-lpignore", "true"), t.setAttribute("data-form-type", "other"), t.setAttribute("autocomplete", "new-password"));
  }
  render() {
    const e = this._mask && !this._reveal ? "password" : "text", t = this._reveal ? "Hide value" : "Show value";
    return l`
      <uui-input
        type=${e}
        .value=${this.value ?? ""}
        name="esatto-encrypted-value"
        autocomplete="new-password"
        spellcheck="false"
        data-1p-ignore="true"
        data-lpignore="true"
        data-form-type="other"
        @input=${c(this, n, m)}
      >
        ${this._mask ? l`<uui-button
              slot="append"
              compact
              look="default"
              label=${t}
              @click=${c(this, n, _)}
            >
              <uui-icon
                name=${this._reveal ? "icon-eye-off" : "icon-eye"}
                aria-hidden="true"
              ></uui-icon>
            </uui-button>` : ""}
      </uui-input>
    `;
  }
};
n = /* @__PURE__ */ new WeakSet();
m = function(e) {
  const t = e.target.value;
  t !== this.value && (this.value = t, this.dispatchEvent(new b()));
};
_ = function(e) {
  e.preventDefault(), e.stopPropagation(), this._reveal = !this._reveal;
};
o.styles = y`
    :host {
      display: block;
    }
    uui-input {
      width: 100%;
    }
  `;
s([
  h({ type: String })
], o.prototype, "value", 2);
s([
  v()
], o.prototype, "_mask", 2);
s([
  v()
], o.prototype, "_reveal", 2);
s([
  h({ attribute: !1 })
], o.prototype, "config", 1);
o = s([
  w("esatto-encrypted-textbox")
], o);
export {
  o as default
};
//# sourceMappingURL=encrypted-textbox.element-BbN8xNJ6.js.map
