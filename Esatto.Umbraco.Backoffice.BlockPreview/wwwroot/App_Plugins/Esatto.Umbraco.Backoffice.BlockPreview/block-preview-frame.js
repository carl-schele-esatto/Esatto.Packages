import { css as A, property as p, state as O, customElement as R, html as u, nothing as _ } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as I } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as K } from "@umbraco-cms/backoffice/resources";
import { UMB_BLOCK_ENTRY_CONTEXT as D } from "@umbraco-cms/backoffice/block";
import { UMB_CONTENT_WORKSPACE_CONTEXT as z } from "@umbraco-cms/backoffice/content";
import { UMB_PROPERTY_CONTEXT as F, UMB_PROPERTY_DATASET_CONTEXT as q } from "@umbraco-cms/backoffice/property";
import { umbHttpClient as H } from "@umbraco-cms/backoffice/http-client";
function L(e) {
  return {
    documentKey: e.documentKey,
    documentTypeKey: e.documentTypeKey,
    propertyAlias: e.propertyAlias,
    culture: e.culture ?? null,
    segment: e.segment ?? null,
    nonce: e.nonce,
    content: e.content,
    settings: e.settings ?? null
  };
}
function X(e) {
  const { nonce: t, ...i } = e;
  return JSON.stringify(i);
}
const Y = "esatto-block-preview:height";
function G(e, t, i) {
  if (e.source !== t) return null;
  const n = e.data;
  if (!n || typeof n != "object" || n.type !== Y || n.id !== i) return null;
  const { height: r } = n;
  return typeof r != "number" || !Number.isFinite(r) || r <= 0 ? null : Math.ceil(r);
}
const V = "/umbraco/management/api/v1/esatto/block-preview/render";
function J(e) {
  return H.post({
    security: [{ scheme: "bearer", type: "http" }],
    url: V,
    body: e,
    headers: { "Content-Type": "application/json" }
  });
}
var j = Object.defineProperty, Q = Object.getOwnPropertyDescriptor, S = (e) => {
  throw TypeError(e);
}, c = (e, t, i, n) => {
  for (var r = n > 1 ? void 0 : n ? Q(t, i) : t, v = e.length - 1, f; v >= 0; v--)
    (f = e[v]) && (r = (n ? f(t, i, r) : f(r)) || r);
  return n && r && j(t, i, r), r;
}, P = (e, t, i) => t.has(e) || S("Cannot " + i), s = (e, t, i) => (P(e, t, "read from private field"), t.get(e)), l = (e, t, i) => t.has(e) ? S("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), h = (e, t, i, n) => (P(e, t, "write to private field"), t.set(e, i), i), d = (e, t, i) => (P(e, t, "access private method"), i), w, b, k, T, y, g, C, a, m, N, B, E, $, W;
const Z = 400;
let o = class extends I {
  constructor() {
    super(), l(this, a), this._view = { kind: "loading" }, this._height = 0, l(this, w), l(this, b), l(this, k), l(this, T), l(this, y), l(this, g), l(this, C, ""), l(this, E, (e) => {
      const t = this.shadowRoot?.querySelector("iframe"), i = G(e, t?.contentWindow, s(this, C));
      i !== null && (this._height = i);
    }), this.consumeContext(D, (e) => {
      h(this, w, e), d(this, a, m).call(this, 0);
    }), this.consumeContext(z, (e) => {
      h(this, b, e), d(this, a, m).call(this, 0);
    }), this.consumeContext(F, (e) => {
      h(this, k, e), d(this, a, m).call(this, 0);
    }), this.consumeContext(q, (e) => {
      h(this, T, e), d(this, a, m).call(this, 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), window.addEventListener("message", s(this, E));
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.removeEventListener("message", s(this, E)), clearTimeout(s(this, y));
  }
  updated(e) {
    (e.has("content") || e.has("settings")) && d(this, a, m).call(this, Z);
  }
  render() {
    return u`${d(this, a, $).call(this)}${d(this, a, W).call(this)}`;
  }
};
w = /* @__PURE__ */ new WeakMap();
b = /* @__PURE__ */ new WeakMap();
k = /* @__PURE__ */ new WeakMap();
T = /* @__PURE__ */ new WeakMap();
y = /* @__PURE__ */ new WeakMap();
g = /* @__PURE__ */ new WeakMap();
C = /* @__PURE__ */ new WeakMap();
a = /* @__PURE__ */ new WeakSet();
m = function(e) {
  clearTimeout(s(this, y)), h(this, y, setTimeout(() => {
    d(this, a, N).call(this);
  }, e));
};
N = async function() {
  const e = s(this, w)?.getContent(), t = s(this, b)?.getUnique(), i = s(this, b)?.structure.getOwnerContentTypeUnique(), n = s(this, k)?.getAlias();
  if (!e || !t || !i || !n) return;
  const r = s(this, T)?.getVariantId(), v = crypto.randomUUID(), f = L({
    documentKey: t,
    documentTypeKey: i,
    propertyAlias: n,
    culture: r?.culture,
    segment: r?.segment,
    nonce: v,
    content: e,
    settings: s(this, w)?.getSettings()
  }), x = X(f);
  if (x === s(this, g)) return;
  h(this, g, x);
  const { data: M, error: U } = await K(this, J(f), { disableNotifications: !0 });
  if (x === s(this, g)) {
    if (U || !M) {
      h(this, g, void 0), this._view = { kind: "error", message: "The preview could not be loaded." };
      return;
    }
    this._view = d(this, a, B).call(this, M, v);
  }
};
B = function(e, t) {
  switch (e.status) {
    case "rendered":
      return h(this, C, t), { kind: "rendered", html: e.html, nonce: t };
    case "notPreviewable":
      return e.reason === "unsavedDocument" ? { kind: "unsaved" } : { kind: "plain" };
    default:
      return { kind: "error", message: e.message };
  }
};
E = /* @__PURE__ */ new WeakMap();
$ = function() {
  const e = this.contentInvalid || this.settingsInvalid;
  return u`
      <div class="bar ${e ? "invalid" : ""}">
        <a class="label" href=${this.config?.editContentPath ?? _}>
          ${this.icon ? u`<umb-icon name=${this.icon}></umb-icon>` : _}
          <span>${this.label}</span>
        </a>
        ${this.config?.showSettingsEdit ? u`<a class="settings" href=${this.config.editSettingsPath ?? _} title="Settings"><umb-icon name="icon-settings"></umb-icon></a>` : _}
      </div>`;
};
W = function() {
  switch (this._view.kind) {
    case "loading":
      return u`<div class="placeholder"></div>`;
    case "rendered":
      return u`<iframe
          title=${this.label ?? "Block preview"}
          sandbox="allow-scripts"
          .srcdoc=${this._view.html}
          style="height:${this._height || 80}px"></iframe>`;
    case "unsaved":
      return u`<div class="note">Save the page once to see a preview.</div>`;
    case "error":
      return u`<div class="note warning">Preview failed: ${this._view.message}</div>`;
    default:
      return _;
  }
};
o.styles = A`
    :host { display: block; border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); overflow: hidden; background: var(--uui-color-surface); }
    .bar { display: flex; align-items: center; min-height: 40px; padding: 0 var(--uui-size-space-4); gap: var(--uui-size-space-2); }
    .bar.invalid { color: var(--uui-color-danger); }
    .label { display: flex; align-items: center; gap: var(--uui-size-space-2); flex: 1; color: inherit; text-decoration: none; font-weight: 600; }
    .label:hover { text-decoration: underline; }
    .settings { color: inherit; }
    iframe { display: block; width: 100%; border: 0; border-top: 1px solid var(--uui-color-border); }
    .placeholder { min-height: 80px; border-top: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt); }
    .note { padding: var(--uui-size-space-3) var(--uui-size-space-4); border-top: 1px solid var(--uui-color-border); color: var(--uui-color-text-alt); font-size: 0.9em; }
    .note.warning { color: var(--uui-color-warning-standalone); }
  `;
c([
  p({ attribute: !1 })
], o.prototype, "content", 2);
c([
  p({ attribute: !1 })
], o.prototype, "settings", 2);
c([
  p({ attribute: !1 })
], o.prototype, "label", 2);
c([
  p({ attribute: !1 })
], o.prototype, "icon", 2);
c([
  p({ attribute: !1 })
], o.prototype, "config", 2);
c([
  p({ attribute: !1 })
], o.prototype, "contentInvalid", 2);
c([
  p({ attribute: !1 })
], o.prototype, "settingsInvalid", 2);
c([
  O()
], o.prototype, "_view", 2);
c([
  O()
], o.prototype, "_height", 2);
o = c([
  R("esatto-block-preview-frame")
], o);
const ae = o;
export {
  o as EsattoBlockPreviewFrameElement,
  ae as default
};
//# sourceMappingURL=block-preview-frame.js.map
