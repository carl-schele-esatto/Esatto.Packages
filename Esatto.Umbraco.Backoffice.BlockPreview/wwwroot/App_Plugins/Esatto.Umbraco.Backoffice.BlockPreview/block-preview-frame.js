import { css as F, property as f, state as N, customElement as K, html as v, nothing as w } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as D } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as z } from "@umbraco-cms/backoffice/resources";
import { UMB_BLOCK_ENTRY_CONTEXT as H } from "@umbraco-cms/backoffice/block";
import { UMB_CONTENT_WORKSPACE_CONTEXT as X } from "@umbraco-cms/backoffice/content";
import { UMB_PROPERTY_CONTEXT as q, UMB_PROPERTY_DATASET_CONTEXT as L } from "@umbraco-cms/backoffice/property";
import { UMB_WORKSPACE_CONTEXT as G } from "@umbraco-cms/backoffice/workspace";
import { umbHttpClient as V } from "@umbraco-cms/backoffice/http-client";
function Y(e) {
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
function j(e) {
  const { nonce: t, ...i } = e;
  return JSON.stringify(i);
}
function J() {
  if (typeof globalThis.crypto?.randomUUID == "function") return globalThis.crypto.randomUUID();
  const e = globalThis.crypto.getRandomValues(new Uint8Array(16));
  return Array.from(e, (t) => t.toString(16).padStart(2, "0")).join("");
}
const Q = "esatto-block-preview:height", Z = 3e3;
function ee(e, t, i) {
  if (e.source !== t) return null;
  const s = e.data;
  if (!s || typeof s != "object" || s.type !== Q || s.id !== i) return null;
  const { height: r } = s;
  return typeof r != "number" || !Number.isFinite(r) || r <= 0 ? null : Math.min(Math.ceil(r), Z);
}
const te = "/umbraco/management/api/v1/esatto/block-preview/render";
function ie(e) {
  return V.post({
    security: [{ scheme: "bearer", type: "http" }],
    url: te,
    body: e,
    headers: { "Content-Type": "application/json" }
  });
}
function re(e, t) {
  switch (e.status) {
    case "rendered":
      return { view: { kind: "rendered", html: e.html, nonce: t }, keepFingerprint: !0 };
    case "notPreviewable":
      return e.reason === "unsavedDocument" ? { view: { kind: "unsaved" }, keepFingerprint: !1 } : { view: { kind: "plain" }, keepFingerprint: !0 };
    default:
      return { view: { kind: "error", message: e.message }, keepFingerprint: !1 };
  }
}
function ne(e) {
  return e === "document";
}
var se = Object.defineProperty, oe = Object.getOwnPropertyDescriptor, W = (e) => {
  throw TypeError(e);
}, h = (e, t, i, s) => {
  for (var r = s > 1 ? void 0 : s ? oe(t, i) : t, m = e.length - 1, g; m >= 0; m--)
    (g = e[m]) && (r = (s ? g(t, i, r) : g(r)) || r);
  return s && r && se(t, i, r), r;
}, S = (e, t, i) => t.has(e) || W("Cannot " + i), n = (e, t, i) => (S(e, t, "read from private field"), t.get(e)), d = (e, t, i) => t.has(e) ? W("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), l = (e, t, i, s) => (S(e, t, "write to private field"), t.set(e, i), i), c = (e, t, i) => (S(e, t, "access private method"), i), _, b, T, C, y, p, P, k, o, u, B, U, E, A, $;
const ae = 400;
let a = class extends D {
  constructor() {
    super(), d(this, o), this._view = { kind: "loading" }, this._height = 0, d(this, _), d(this, b), d(this, T), d(this, C), d(this, y), d(this, p), d(this, P, ""), d(this, k), d(this, E, (e) => {
      const t = this.shadowRoot?.querySelector("iframe"), i = ee(e, t?.contentWindow, n(this, P));
      i !== null && (this._height = i);
    }), this.consumeContext(H, (e) => {
      l(this, _, e), c(this, o, u).call(this, 0);
    }), this.consumeContext(G, (e) => {
      l(this, k, e?.getEntityType()), c(this, o, u).call(this, 0);
    }), this.consumeContext(X, (e) => {
      l(this, b, e), c(this, o, u).call(this, 0), this.observe(e?.isNew, (t) => {
        t === !1 && this._view.kind === "unsaved" && c(this, o, u).call(this, 0);
      }, "_observeIsNew");
    }), this.consumeContext(q, (e) => {
      l(this, T, e), c(this, o, u).call(this, 0);
    }), this.consumeContext(L, (e) => {
      l(this, C, e), c(this, o, u).call(this, 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), window.addEventListener("message", n(this, E));
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.removeEventListener("message", n(this, E)), clearTimeout(n(this, y));
  }
  updated(e) {
    (e.has("content") || e.has("settings")) && c(this, o, u).call(this, ae);
  }
  render() {
    return v`${c(this, o, A).call(this)}${c(this, o, $).call(this)}`;
  }
};
_ = /* @__PURE__ */ new WeakMap();
b = /* @__PURE__ */ new WeakMap();
T = /* @__PURE__ */ new WeakMap();
C = /* @__PURE__ */ new WeakMap();
y = /* @__PURE__ */ new WeakMap();
p = /* @__PURE__ */ new WeakMap();
P = /* @__PURE__ */ new WeakMap();
k = /* @__PURE__ */ new WeakMap();
o = /* @__PURE__ */ new WeakSet();
u = function(e) {
  clearTimeout(n(this, y)), l(this, y, setTimeout(() => {
    c(this, o, B).call(this);
  }, e));
};
B = async function() {
  try {
    await c(this, o, U).call(this);
  } catch (e) {
    l(this, p, void 0), this._view = { kind: "error", message: e?.message ?? "The preview could not be loaded." };
  }
};
U = async function() {
  if (n(this, k) !== void 0 && !ne(n(this, k))) {
    this._view = { kind: "plain" };
    return;
  }
  const e = n(this, _)?.getContent(), t = n(this, b)?.getUnique(), i = n(this, b)?.structure.getOwnerContentTypeUnique(), s = n(this, T)?.getAlias();
  if (!e || !t || !i || !s) return;
  const r = n(this, C)?.getVariantId(), m = J(), g = Y({
    documentKey: t,
    documentTypeKey: i,
    propertyAlias: s,
    culture: r?.culture,
    segment: r?.segment,
    nonce: m,
    content: e,
    settings: n(this, _)?.getSettings()
  }), x = j(g);
  if (x === n(this, p)) return;
  l(this, p, x);
  const { data: O, error: I } = await z(this, ie(g), { disableNotifications: !0 });
  if (x !== n(this, p)) return;
  if (I || !O) {
    l(this, p, void 0), this._view = { kind: "error", message: "The preview could not be loaded." };
    return;
  }
  const { view: M, keepFingerprint: R } = re(O, m);
  R || l(this, p, void 0), M.kind === "rendered" && l(this, P, M.nonce), this._view = M;
};
E = /* @__PURE__ */ new WeakMap();
A = function() {
  const e = this.contentInvalid || this.settingsInvalid;
  return v`
      <div class="bar ${e ? "invalid" : ""}">
        <a class="label" href=${this.config?.editContentPath ?? w}>
          ${this.icon ? v`<umb-icon name=${this.icon}></umb-icon>` : w}
          <span>${this.label}</span>
        </a>
        ${this.config?.showSettingsEdit ? v`<a class="settings" href=${this.config.editSettingsPath ?? w} title="Settings"><umb-icon name="icon-settings"></umb-icon></a>` : w}
      </div>`;
};
$ = function() {
  switch (this._view.kind) {
    case "loading":
      return v`<div class="placeholder"></div>`;
    case "rendered":
      return v`<iframe
          title=${this.label ?? "Block preview"}
          sandbox="allow-scripts"
          .srcdoc=${this._view.html}
          style="height:${this._height || 80}px"></iframe>`;
    case "unsaved":
      return v`<div class="note">Save the page once to see a preview.</div>`;
    case "error":
      return v`<div class="note warning">Preview failed: ${this._view.message}</div>`;
    default:
      return w;
  }
};
a.styles = F`
    /* Space between stacked blocks: Block List puts custom views directly on top of each other. */
    :host { display: block; margin-bottom: var(--uui-size-space-4); border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); overflow: hidden; background: var(--uui-color-surface); }
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
h([
  f({ attribute: !1 })
], a.prototype, "content", 2);
h([
  f({ attribute: !1 })
], a.prototype, "settings", 2);
h([
  f({ attribute: !1 })
], a.prototype, "label", 2);
h([
  f({ attribute: !1 })
], a.prototype, "icon", 2);
h([
  f({ attribute: !1 })
], a.prototype, "config", 2);
h([
  f({ attribute: !1 })
], a.prototype, "contentInvalid", 2);
h([
  f({ attribute: !1 })
], a.prototype, "settingsInvalid", 2);
h([
  N()
], a.prototype, "_view", 2);
h([
  N()
], a.prototype, "_height", 2);
a = h([
  K("esatto-block-preview-frame")
], a);
const me = a;
export {
  a as EsattoBlockPreviewFrameElement,
  me as default
};
//# sourceMappingURL=block-preview-frame.js.map
