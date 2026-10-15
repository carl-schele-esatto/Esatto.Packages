import { css as K, property as f, state as B, customElement as q, html as v, nothing as w } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as z } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as H } from "@umbraco-cms/backoffice/resources";
import { UMB_BLOCK_ENTRY_CONTEXT as L } from "@umbraco-cms/backoffice/block";
import { UMB_CONTENT_WORKSPACE_CONTEXT as X } from "@umbraco-cms/backoffice/content";
import { UMB_PROPERTY_CONTEXT as G, UMB_PROPERTY_DATASET_CONTEXT as V } from "@umbraco-cms/backoffice/property";
import { UMB_WORKSPACE_CONTEXT as Y } from "@umbraco-cms/backoffice/workspace";
import { umbHttpClient as j } from "@umbraco-cms/backoffice/http-client";
function J(e) {
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
function Q(e) {
  const { nonce: t, ...i } = e;
  return JSON.stringify(i);
}
function Z() {
  if (typeof globalThis.crypto?.randomUUID == "function") return globalThis.crypto.randomUUID();
  const e = globalThis.crypto.getRandomValues(new Uint8Array(16));
  return Array.from(e, (t) => t.toString(16).padStart(2, "0")).join("");
}
const ee = "esatto-block-preview:height", te = 3e3;
function ie(e, t, i) {
  if (e.source !== t) return null;
  const r = e.data;
  if (!r || typeof r != "object" || r.type !== ee || r.id !== i) return null;
  const { height: s } = r;
  return typeof s != "number" || !Number.isFinite(s) || s <= 0 ? null : Math.min(Math.ceil(s), te);
}
const re = "/umbraco/management/api/v1/esatto/block-preview/render";
function ne(e, t) {
  return j.post({
    security: [{ scheme: "bearer", type: "http" }],
    url: re,
    body: e,
    signal: t,
    headers: { "Content-Type": "application/json" }
  });
}
function se(e, t) {
  switch (e.status) {
    case "rendered":
      return { view: { kind: "rendered", html: e.html, nonce: t }, keepFingerprint: !0 };
    case "notPreviewable":
      return e.reason === "unsavedDocument" ? { view: { kind: "unsaved" }, keepFingerprint: !1 } : { view: { kind: "plain" }, keepFingerprint: !0 };
    default:
      return { view: { kind: "error", message: e.message }, keepFingerprint: !1 };
  }
}
function oe(e) {
  return e === "document";
}
function ae(e, t) {
  const i = ["content", "settings"].filter((r) => e.has(r));
  return i.length === 0 ? null : i.some((r) => e.get(r) !== void 0) ? t : 0;
}
class ce {
  #e;
  next() {
    return this.#e?.abort(), this.#e = new AbortController(), this.#e.signal;
  }
  cancel() {
    this.#e?.abort(), this.#e = void 0;
  }
}
var le = Object.defineProperty, he = Object.getOwnPropertyDescriptor, A = (e) => {
  throw TypeError(e);
}, d = (e, t, i, r) => {
  for (var s = r > 1 ? void 0 : r ? he(t, i) : t, g = e.length - 1, m; g >= 0; g--)
    (m = e[g]) && (s = (r ? m(t, i, s) : m(s)) || s);
  return r && s && le(t, i, s), s;
}, N = (e, t, i) => t.has(e) || A("Cannot " + i), n = (e, t, i) => (N(e, t, "read from private field"), t.get(e)), h = (e, t, i) => t.has(e) ? A("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), l = (e, t, i, r) => (N(e, t, "write to private field"), t.set(e, i), i), c = (e, t, i) => (N(e, t, "access private method"), i), _, b, C, x, y, u, M, P, k, o, p, U, R, T, $, I;
const ue = 400;
let a = class extends z {
  constructor() {
    super(), h(this, o), this._view = { kind: "loading" }, this._height = 0, h(this, _), h(this, b), h(this, C), h(this, x), h(this, y), h(this, u), h(this, M, new ce()), h(this, P, ""), h(this, k), h(this, T, (e) => {
      const t = this.shadowRoot?.querySelector("iframe"), i = ie(e, t?.contentWindow, n(this, P));
      i !== null && (this._height = i);
    }), this.consumeContext(L, (e) => {
      l(this, _, e), c(this, o, p).call(this, 0);
    }), this.consumeContext(Y, (e) => {
      l(this, k, e?.getEntityType()), c(this, o, p).call(this, 0);
    }), this.consumeContext(X, (e) => {
      l(this, b, e), c(this, o, p).call(this, 0), this.observe(e?.isNew, (t) => {
        t === !1 && this._view.kind === "unsaved" && c(this, o, p).call(this, 0);
      }, "_observeIsNew");
    }), this.consumeContext(G, (e) => {
      l(this, C, e), c(this, o, p).call(this, 0);
    }), this.consumeContext(V, (e) => {
      l(this, x, e), c(this, o, p).call(this, 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), window.addEventListener("message", n(this, T)), c(this, o, p).call(this, 0);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.removeEventListener("message", n(this, T)), clearTimeout(n(this, y)), n(this, M).cancel();
  }
  updated(e) {
    const t = ae(e, ue);
    t !== null && c(this, o, p).call(this, t);
  }
  render() {
    return v`${c(this, o, $).call(this)}${c(this, o, I).call(this)}`;
  }
};
_ = /* @__PURE__ */ new WeakMap();
b = /* @__PURE__ */ new WeakMap();
C = /* @__PURE__ */ new WeakMap();
x = /* @__PURE__ */ new WeakMap();
y = /* @__PURE__ */ new WeakMap();
u = /* @__PURE__ */ new WeakMap();
M = /* @__PURE__ */ new WeakMap();
P = /* @__PURE__ */ new WeakMap();
k = /* @__PURE__ */ new WeakMap();
o = /* @__PURE__ */ new WeakSet();
p = function(e) {
  clearTimeout(n(this, y)), l(this, y, setTimeout(() => {
    c(this, o, U).call(this);
  }, e));
};
U = async function() {
  try {
    await c(this, o, R).call(this);
  } catch (e) {
    l(this, u, void 0), this._view = { kind: "error", message: e?.message ?? "The preview could not be loaded." };
  }
};
R = async function() {
  if (n(this, k) !== void 0 && !oe(n(this, k))) {
    this._view = { kind: "plain" };
    return;
  }
  const e = n(this, _)?.getContent(), t = n(this, b)?.getUnique(), i = n(this, b)?.structure.getOwnerContentTypeUnique(), r = n(this, C)?.getAlias();
  if (!e || !t || !i || !r) return;
  const s = n(this, x)?.getVariantId(), g = Z(), m = J({
    documentKey: t,
    documentTypeKey: i,
    propertyAlias: r,
    culture: s?.culture,
    segment: s?.segment,
    nonce: g,
    content: e,
    settings: n(this, _)?.getSettings()
  }), E = Q(m);
  if (E === n(this, u)) return;
  l(this, u, E);
  const S = n(this, M).next(), { data: W, error: F } = await H(this, ne(m, S), { disableNotifications: !0, abortSignal: S });
  if (S.aborted) {
    E === n(this, u) && l(this, u, void 0);
    return;
  }
  if (E !== n(this, u)) return;
  if (F || !W) {
    l(this, u, void 0), this._view = { kind: "error", message: "The preview could not be loaded." };
    return;
  }
  const { view: O, keepFingerprint: D } = se(W, g);
  D || l(this, u, void 0), O.kind === "rendered" && l(this, P, O.nonce), this._view = O;
};
T = /* @__PURE__ */ new WeakMap();
$ = function() {
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
I = function() {
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
a.styles = K`
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
d([
  f({ attribute: !1 })
], a.prototype, "content", 2);
d([
  f({ attribute: !1 })
], a.prototype, "settings", 2);
d([
  f({ attribute: !1 })
], a.prototype, "label", 2);
d([
  f({ attribute: !1 })
], a.prototype, "icon", 2);
d([
  f({ attribute: !1 })
], a.prototype, "config", 2);
d([
  f({ attribute: !1 })
], a.prototype, "contentInvalid", 2);
d([
  f({ attribute: !1 })
], a.prototype, "settingsInvalid", 2);
d([
  B()
], a.prototype, "_view", 2);
d([
  B()
], a.prototype, "_height", 2);
a = d([
  q("esatto-block-preview-frame")
], a);
const be = a;
export {
  a as EsattoBlockPreviewFrameElement,
  be as default
};
//# sourceMappingURL=block-preview-frame.js.map
