import { LitElement as f, html as h, css as y, property as v, state as d, customElement as g } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as w } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent as b } from "@umbraco-cms/backoffice/event";
var E = Object.defineProperty, x = Object.getOwnPropertyDescriptor, m = (e) => {
  throw TypeError(e);
}, i = (e, t, a, n) => {
  for (var o = n > 1 ? void 0 : n ? x(t, a) : t, u = e.length - 1, p; u >= 0; u--)
    (p = e[u]) && (o = (n ? p(t, a, o) : p(o)) || o);
  return n && o && E(t, a, o), o;
}, k = (e, t, a) => t.has(e) || m("Cannot " + a), $ = (e, t, a) => t.has(e) ? m("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, a), l = (e, t, a) => (k(e, t, "access private method"), a), s, c, _;
let r = class extends w(f) {
  constructor() {
    super(...arguments), $(this, s), this.value = "", this._mask = !0, this._reveal = !1;
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
    return h`
      <uui-input
        type=${e}
        .value=${this.value ?? ""}
        name="esatto-encrypted-value"
        autocomplete="new-password"
        spellcheck="false"
        data-1p-ignore="true"
        data-lpignore="true"
        data-form-type="other"
        @input=${l(this, s, c)}
        @change=${l(this, s, c)}
      >
        ${this._mask ? h`<uui-button
              slot="append"
              compact
              look="default"
              label=${t}
              title=${t}
              @click=${l(this, s, _)}
            >
              <uui-icon name=${this._reveal ? "icon-eye-off" : "icon-eye"}></uui-icon>
            </uui-button>` : ""}
      </uui-input>
    `;
  }
};
s = /* @__PURE__ */ new WeakSet();
c = function(e) {
  this.value = e.target.value, this.dispatchEvent(new b());
};
_ = function(e) {
  e.preventDefault(), e.stopPropagation(), this._reveal = !this._reveal;
};
r.styles = y`
    :host {
      display: block;
    }
    uui-input {
      width: 100%;
    }
  `;
i([
  v({ type: String })
], r.prototype, "value", 2);
i([
  d()
], r.prototype, "_mask", 2);
i([
  d()
], r.prototype, "_reveal", 2);
i([
  v({ attribute: !1 })
], r.prototype, "config", 1);
r = i([
  g("esatto-encrypted-textbox")
], r);
export {
  r as default
};
//# sourceMappingURL=encrypted-textbox.element-BEdSRqvJ.js.map
