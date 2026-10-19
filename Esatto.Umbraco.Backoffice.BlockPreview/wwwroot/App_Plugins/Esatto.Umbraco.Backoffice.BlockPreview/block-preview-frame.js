import { css as D, property as w, state as P, customElement as K, html as v, nothing as b, repeat as q } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as z } from "@umbraco-cms/backoffice/lit-element";
import { tryExecute as H } from "@umbraco-cms/backoffice/resources";
import { UMB_BLOCK_ENTRY_CONTEXT as X } from "@umbraco-cms/backoffice/block";
import { UMB_CONTENT_WORKSPACE_CONTEXT as G } from "@umbraco-cms/backoffice/content";
import { UMB_PROPERTY_CONTEXT as V, UMB_PROPERTY_DATASET_CONTEXT as Y } from "@umbraco-cms/backoffice/property";
import { UMB_WORKSPACE_CONTEXT as j } from "@umbraco-cms/backoffice/workspace";
import { umbHttpClient as J } from "@umbraco-cms/backoffice/http-client";
function Q(e) {
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
function Z(e) {
  const { nonce: t, ...i } = e;
  return JSON.stringify(i);
}
function ee() {
  if (typeof globalThis.crypto?.randomUUID == "function") return globalThis.crypto.randomUUID();
  const e = globalThis.crypto.getRandomValues(new Uint8Array(16));
  return Array.from(e, (t) => t.toString(16).padStart(2, "0")).join("");
}
const te = "esatto-block-preview:height", ie = 3e3;
function re(e, t, i) {
  if (e.source !== t) return null;
  const r = e.data;
  if (!r || typeof r != "object" || r.type !== te || r.id !== i) return null;
  const { height: n } = r;
  return typeof n != "number" || !Number.isFinite(n) || n <= 0 ? null : Math.min(Math.ceil(n), ie);
}
const ne = "esatto-block-preview:embeds", se = /* @__PURE__ */ new Set(["www.youtube-nocookie.com", "youtube-nocookie.com", "www.youtube.com", "youtube.com"]);
function oe(e) {
  let t;
  try {
    t = new URL(e);
  } catch {
    return !1;
  }
  return t.protocol === "https:" && se.has(t.hostname) && t.pathname.startsWith("/embed/");
}
function ae(e) {
  const t = e;
  if (!t || typeof t != "object") return null;
  const i = [t.top, t.left, t.width, t.height];
  if (!i.every((g) => typeof g == "number" && Number.isFinite(g))) return null;
  const [r, n, o, c] = i;
  return o <= 0 || c <= 0 ? null : { top: Math.max(0, r), left: Math.max(0, n), width: o, height: c };
}
function le(e, t, i) {
  if (e.source !== t) return null;
  const r = e.data;
  return !r || typeof r != "object" || r.type !== ne || r.id !== i || !Array.isArray(r.embeds) ? null : r.embeds.flatMap((n) => {
    const o = n;
    if (!o || typeof o.url != "string" || !oe(o.url)) return [];
    const c = ae(o.rect);
    return c ? [{ url: o.url, rect: c }] : [];
  });
}
const ce = "/umbraco/management/api/v1/esatto/block-preview/render";
function ue(e, t) {
  return J.post({
    security: [{ scheme: "bearer", type: "http" }],
    url: ce,
    body: e,
    signal: t,
    headers: { "Content-Type": "application/json" }
  });
}
function de(e, t) {
  switch (e.status) {
    case "rendered":
      return { view: { kind: "rendered", html: e.html, nonce: t }, keepFingerprint: !0 };
    case "notPreviewable":
      return e.reason === "unsavedDocument" ? { view: { kind: "unsaved" }, keepFingerprint: !1 } : { view: { kind: "plain" }, keepFingerprint: !0 };
    default:
      return { view: { kind: "error", message: e.message }, keepFingerprint: !1 };
  }
}
function he(e) {
  return e === "document";
}
function pe(e, t) {
  const i = ["content", "settings"].filter((r) => e.has(r));
  return i.length === 0 ? null : i.some((r) => e.get(r) !== void 0) ? t : 0;
}
class fe {
  #e;
  next() {
    return this.#e?.abort(), this.#e = new AbortController(), this.#e.signal;
  }
  cancel() {
    this.#e?.abort(), this.#e = void 0;
  }
}
var me = Object.defineProperty, ve = Object.getOwnPropertyDescriptor, A = (e) => {
  throw TypeError(e);
}, h = (e, t, i, r) => {
  for (var n = r > 1 ? void 0 : r ? ve(t, i) : t, o = e.length - 1, c; o >= 0; o--)
    (c = e[o]) && (n = (r ? c(t, i, n) : c(n)) || n);
  return r && n && me(t, i, n), n;
}, W = (e, t, i) => t.has(e) || A("Cannot " + i), s = (e, t, i) => (W(e, t, "read from private field"), t.get(e)), p = (e, t, i) => t.has(e) ? A("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), d = (e, t, i, r) => (W(e, t, "write to private field"), t.set(e, i), i), u = (e, t, i) => (W(e, t, "access private method"), i), _, k, C, M, E, f, S, y, T, l, m, B, U, x, R, F;
const ge = 400;
let a = class extends z {
  constructor() {
    super(), p(this, l), this._view = { kind: "loading" }, this._height = 0, this._embeds = [], this._resolvedLabel = "", p(this, _), p(this, k), p(this, C), p(this, M), p(this, E), p(this, f), p(this, S, new fe()), p(this, y, ""), p(this, T), p(this, x, (e) => {
      const t = this.shadowRoot?.querySelector("iframe.preview"), i = re(e, t?.contentWindow, s(this, y));
      i !== null && (this._height = i);
      const r = le(e, t?.contentWindow, s(this, y));
      r !== null && (this._embeds = r);
    }), this.consumeContext(X, (e) => {
      d(this, _, e), u(this, l, m).call(this, 0);
    }), this.consumeContext(j, (e) => {
      d(this, T, e?.getEntityType()), u(this, l, m).call(this, 0);
    }), this.consumeContext(G, (e) => {
      d(this, k, e), u(this, l, m).call(this, 0), this.observe(e?.isNew, (t) => {
        t === !1 && this._view.kind === "unsaved" && u(this, l, m).call(this, 0);
      }, "_observeIsNew");
    }), this.consumeContext(V, (e) => {
      d(this, C, e), u(this, l, m).call(this, 0);
    }), this.consumeContext(Y, (e) => {
      d(this, M, e), u(this, l, m).call(this, 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), window.addEventListener("message", s(this, x)), u(this, l, m).call(this, 0);
  }
  disconnectedCallback() {
    super.disconnectedCallback(), window.removeEventListener("message", s(this, x)), clearTimeout(s(this, E)), s(this, S).cancel();
  }
  updated(e) {
    const t = pe(e, ge);
    t !== null && u(this, l, m).call(this, t);
  }
  render() {
    return v`${u(this, l, R).call(this)}${u(this, l, F).call(this)}`;
  }
};
_ = /* @__PURE__ */ new WeakMap();
k = /* @__PURE__ */ new WeakMap();
C = /* @__PURE__ */ new WeakMap();
M = /* @__PURE__ */ new WeakMap();
E = /* @__PURE__ */ new WeakMap();
f = /* @__PURE__ */ new WeakMap();
S = /* @__PURE__ */ new WeakMap();
y = /* @__PURE__ */ new WeakMap();
T = /* @__PURE__ */ new WeakMap();
l = /* @__PURE__ */ new WeakSet();
m = function(e) {
  clearTimeout(s(this, E)), d(this, E, setTimeout(() => {
    u(this, l, B).call(this);
  }, e));
};
B = async function() {
  try {
    await u(this, l, U).call(this);
  } catch (e) {
    d(this, f, void 0), this._view = { kind: "error", message: e?.message ?? "The preview could not be loaded." };
  }
};
U = async function() {
  if (s(this, T) !== void 0 && !he(s(this, T))) {
    this._view = { kind: "plain" };
    return;
  }
  const e = s(this, _)?.getContent(), t = s(this, k)?.getUnique(), i = s(this, k)?.structure.getOwnerContentTypeUnique(), r = s(this, C)?.getAlias();
  if (!e || !t || !i || !r) return;
  const n = s(this, M)?.getVariantId(), o = ee(), c = Q({
    documentKey: t,
    documentTypeKey: i,
    propertyAlias: r,
    culture: n?.culture,
    segment: n?.segment,
    nonce: o,
    content: e,
    settings: s(this, _)?.getSettings()
  }), g = Z(c);
  if (g === s(this, f)) return;
  d(this, f, g);
  const $ = s(this, S).next(), { data: N, error: I } = await H(this, ue(c, $), { disableNotifications: !0, abortSignal: $ });
  if ($.aborted) {
    g === s(this, f) && d(this, f, void 0);
    return;
  }
  if (g !== s(this, f)) return;
  if (I || !N) {
    d(this, f, void 0), this._view = { kind: "error", message: "The preview could not be loaded." };
    return;
  }
  const { view: O, keepFingerprint: L } = de(N, o);
  L || d(this, f, void 0), O.kind === "rendered" && (d(this, y, O.nonce), this._embeds = []), this._view = O;
};
x = /* @__PURE__ */ new WeakMap();
R = function() {
  const e = this.contentInvalid || this.settingsInvalid;
  return v`
      <div class="bar ${e ? "invalid" : ""}">
        <a class="label" href=${this.config?.editContentPath ?? b}>
          ${this.icon ? v`<umb-icon name=${this.icon}></umb-icon>` : b}
          <umb-ufm-render
            inline
            .markdown=${this.label}
            .value=${this.content}
            @umb-ufm-resolved=${(t) => {
    this._resolvedLabel = t.detail.text;
  }}></umb-ufm-render>
        </a>
        ${this.config?.showSettingsEdit ? v`<a class="settings" href=${this.config.editSettingsPath ?? b} title="Settings"><umb-icon name="icon-settings"></umb-icon></a>` : b}
      </div>`;
};
F = function() {
  switch (this._view.kind) {
    case "loading":
      return v`<div class="placeholder"><uui-loader label="Loading preview"></uui-loader></div>`;
    case "rendered":
      return v`<div class="frame">
          <iframe
            class="preview"
            title=${this._resolvedLabel || "Block preview"}
            sandbox="allow-scripts"
            .srcdoc=${this._view.html}
            style="height:${this._height || 80}px"></iframe>
          ${q(
        this._embeds,
        (e) => e.url,
        (e) => v`<iframe
              class="embed"
              title="Video player"
              src=${e.url}
              allow="autoplay; encrypted-media; picture-in-picture; fullscreen"
              allowfullscreen
              referrerpolicy="strict-origin-when-cross-origin"
              style="top:${e.rect.top}px;left:${e.rect.left}px;width:${e.rect.width}px;height:${e.rect.height}px"></iframe>`
      )}
        </div>`;
    case "unsaved":
      return v`<div class="note">Save the page once to see a preview.</div>`;
    case "error":
      return v`<div class="note warning">Preview failed: ${this._view.message}</div>`;
    default:
      return b;
  }
};
a.styles = D`
    /* Space between stacked blocks: Block List puts custom views directly on top of each other. */
    :host { display: block; margin-bottom: var(--uui-size-space-4); border: 1px solid var(--uui-color-border); border-radius: var(--uui-border-radius); overflow: hidden; background: var(--uui-color-surface); }
    .bar { display: flex; align-items: center; min-height: 40px; padding: 0 var(--uui-size-space-4); gap: var(--uui-size-space-2); }
    .bar.invalid { color: var(--uui-color-danger); }
    .label { display: flex; align-items: center; gap: var(--uui-size-space-2); flex: 1; color: inherit; text-decoration: none; font-weight: 600; }
    .label:hover { text-decoration: underline; }
    .settings { color: inherit; }
    .frame { position: relative; border-top: 1px solid var(--uui-color-border); }
    .preview { display: block; width: 100%; border: 0; }
    .embed { position: absolute; border: 0; }
    .placeholder { display: flex; align-items: center; justify-content: center; min-height: 80px; border-top: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt); }
    .note { padding: var(--uui-size-space-3) var(--uui-size-space-4); border-top: 1px solid var(--uui-color-border); color: var(--uui-color-text-alt); font-size: 0.9em; }
    .note.warning { color: var(--uui-color-warning-standalone); }
  `;
h([
  w({ attribute: !1 })
], a.prototype, "content", 2);
h([
  w({ attribute: !1 })
], a.prototype, "settings", 2);
h([
  w({ attribute: !1 })
], a.prototype, "label", 2);
h([
  w({ attribute: !1 })
], a.prototype, "icon", 2);
h([
  w({ attribute: !1 })
], a.prototype, "config", 2);
h([
  w({ attribute: !1 })
], a.prototype, "contentInvalid", 2);
h([
  w({ attribute: !1 })
], a.prototype, "settingsInvalid", 2);
h([
  P()
], a.prototype, "_view", 2);
h([
  P()
], a.prototype, "_height", 2);
h([
  P()
], a.prototype, "_embeds", 2);
h([
  P()
], a.prototype, "_resolvedLabel", 2);
a = h([
  K("esatto-block-preview-frame")
], a);
const Ce = a;
export {
  a as EsattoBlockPreviewFrameElement,
  Ce as default
};
//# sourceMappingURL=block-preview-frame.js.map
