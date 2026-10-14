import { LitElement as Q } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as tt } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as et } from "@umbraco-cms/backoffice/notification";
import { UMB_ACTION_EVENT_CONTEXT as nt } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadChildrenOfEntityEvent as ot } from "@umbraco-cms/backoffice/entity-action";
import { DictionaryService as rt } from "@umbraco-cms/backoffice/external/backend-api";
import { tryExecute as it } from "@umbraco-cms/backoffice/resources";
const K = /* @__PURE__ */ new Map();
function st(e) {
  K.set(e.entityType, e);
}
function X(e) {
  return e ? K.get(e) : void 0;
}
function q(e) {
  if (!e) return null;
  if (e.parentElement) return e.parentElement;
  const t = e.getRootNode?.();
  return t && t.host ? t.host : null;
}
function f(e, t) {
  if (!e || e.nodeType !== 1) return;
  const n = e;
  t(n);
  for (const o of n.children)
    f(o, t);
  if (n.shadowRoot)
    for (const o of n.shadowRoot.children)
      f(o, t);
}
function E(e) {
  return e.getAttribute("entitytype") ?? e.getAttribute("entity-type");
}
function c(e) {
  const t = e;
  return !t || t.nodeType !== 1 || t.tagName !== "UMB-TREE-ITEM" ? !1 : X(E(t)) !== void 0;
}
function U(e) {
  return X(E(e));
}
function l(e) {
  return e.props?.item?.unique ?? e.api?.unique ?? e._item?.unique ?? e.getAttribute?.("data-unique") ?? null;
}
function w(e) {
  const t = e.props?.item?.parent;
  if (t !== void 0)
    return t.unique ?? null;
  let n = q(e);
  for (; n; ) {
    if (c(n)) return l(n);
    n = q(n);
  }
  return null;
}
function b(e) {
  return e?.props?.item?.name ?? "item";
}
function T(e) {
  function t(n) {
    if (!(!n || n.nodeType !== 1)) {
      if (n.tagName !== "SLOT")
        try {
          n.setAttribute("draggable", "true");
        } catch {
        }
      for (const o of n.children) t(o);
      if (n.shadowRoot)
        for (const o of n.shadowRoot.children) t(o);
    }
  }
  for (const n of e.children) t(n);
  if (e.shadowRoot)
    for (const n of e.shadowRoot.children) t(n);
}
function ut(e) {
  let t = e;
  for (; t; ) {
    if (c(t)) return t;
    t = q(t);
  }
  return null;
}
function y(e, t) {
  let n = null;
  return f(document.documentElement, (o) => {
    n || c(o) && E(o) === t && l(o) === e && (n = o);
  }), n;
}
function Y(e) {
  const t = [];
  return f(document.documentElement, (n) => {
    c(n) && E(n) === e && t.push(n);
  }), t;
}
const $ = "esattoTreeDndAttached", ct = 700, at = "application/x-esatto-tree-dnd", v = "esatto-tree-dnd", I = "[esatto-tree-dnd]", L = "__esattoTreeDndPatched", A = /* @__PURE__ */ new WeakSet(), j = [];
let x = null;
function O(e) {
  x = e, e && V();
}
function V() {
  for (const e of j)
    D(e);
}
function D(e) {
  if (!e) return;
  const t = e.nodeType === 9 ? e.documentElement : e;
  f(t, (n) => {
    c(n) && (x?.attachItem(n), T(n)), n.shadowRoot && !A.has(n.shadowRoot) && R(n.shadowRoot);
  });
}
function R(e) {
  if (!e || A.has(e)) return;
  A.add(e), j.push(e), new MutationObserver((n) => {
    for (const o of n) {
      if (o.type === "attributes" && c(o.target)) {
        x?.attachItem(o.target);
        continue;
      }
      for (const r of o.addedNodes) {
        if (!(r instanceof Element)) continue;
        c(r) && x?.attachItem(r), D(r);
        const i = ut(r);
        i && T(i);
      }
    }
  }).observe(e, {
    childList: !0,
    subtree: !0,
    attributes: !0,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ["entitytype", "entity-type"]
  }), D(e);
}
function dt() {
  const e = window;
  if (e[L]) return;
  e[L] = !0;
  const t = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function(n) {
    const o = t.call(this, n);
    try {
      R(o);
    } catch (r) {
      console.warn(`${I} observeRoot failed:`, r);
    }
    return o;
  }, R(document);
}
const lt = "umb-default-tree-item";
function C(e) {
  const t = [...new Set([U(e)?.innerTag, lt].filter((s) => !!s))], o = (e.querySelector(t.map((s) => `:scope > ${s}`).join(", ")) ?? e.shadowRoot?.querySelector(t.join(", ")))?.shadowRoot?.querySelector("uui-menu-item"), r = o?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (r) return r.getBoundingClientRect();
  if (o) {
    const s = o.getBoundingClientRect();
    return new DOMRect(s.left, s.top, s.width, Math.min(s.height, 32));
  }
  const i = e.getBoundingClientRect();
  return new DOMRect(i.left, i.top, i.width, Math.min(i.height, 32));
}
function ft(e, t, n) {
  const o = n - e, r = t / 3;
  return o < r ? "before" : o > t - r ? "after" : "into";
}
function P(e, t) {
  const n = C(e);
  return ft(n.top, n.height, t);
}
function ht(e) {
  const t = /* @__PURE__ */ new Set();
  return f(e, (n) => {
    if (n !== e && c(n)) {
      const o = l(n);
      o && t.add(o);
    }
  }), t;
}
function N(e, t, n) {
  return e === t || n.has(e);
}
function pt(e) {
  const t = e.zone === "into" ? e.targetUnique : e.targetParentUnique;
  return t === e.sourceParentUnique ? { kind: "noop" } : { kind: "move", parentUnique: t };
}
function _(e) {
  return e?.kind === "move";
}
function mt(e, t, n, o) {
  return e.kind === "noop" ? "No change: already here" : t === "into" ? `Move into ${n}` : o === null ? `Move to root (next to ${n})` : `Move next to ${n} (under ${o})`;
}
const z = "var(--uui-color-selected, #3879ff)", yt = "rgba(56, 121, 255, 0.08)", Z = (() => {
  const e = document.createElement("div");
  return e.id = "esatto-tree-dnd-indicator", e.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `, document.body.appendChild(e), e;
})();
function B(e, t) {
  const n = C(e), o = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${n.left}px;
    width: ${n.width}px;
  `;
  Z.style.cssText = t === "line-above" ? `${o}
      top: ${n.top - 1}px;
      height: 2px;
      background: ${z};
    ` : `${o}
      top: ${n.top}px;
      height: ${n.height}px;
      border: 2px solid ${z};
      background: ${yt};
    `;
}
function u() {
  Z.style.display = "none";
}
function G(e, t) {
  if (e.kind === "noop") {
    u();
    return;
  }
  if (e.parentUnique !== null) {
    const o = y(e.parentUnique, t.entityType);
    o ? B(o, "outline") : u();
    return;
  }
  const n = Y(t.entityType).find((o) => w(o) === null);
  n ? B(n, "line-above") : u();
}
const d = (() => {
  const e = document.createElement("div");
  return e.id = "esatto-tree-dnd-spinner", e.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 100000;
    display: none;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
    color: var(--uui-color-selected, #3879ff);
  `, e.innerHTML = '<uui-loader-circle style="font-size: 1.2em;"></uui-loader-circle>', document.body.appendChild(e), e;
})();
function gt(e) {
  const t = C(e);
  d.style.left = `${t.left}px`, d.style.top = `${t.top}px`, d.style.width = `${Math.min(t.height, 28)}px`, d.style.height = `${t.height}px`, d.style.display = "flex";
}
function bt() {
  d.style.display = "none";
}
function H(e, t, n) {
  let o = e + t;
  for (; o >= 0 && o < n.length; ) {
    if (!n[o].blocked) return o;
    o += t;
  }
  return e;
}
function vt(e, t, n) {
  switch (t) {
    case "ArrowDown":
      return { type: "none", state: { ...e, targetIndex: H(e.targetIndex, 1, n), zone: "before" } };
    case "ArrowUp":
      return { type: "none", state: { ...e, targetIndex: H(e.targetIndex, -1, n), zone: "before" } };
    case "ArrowRight":
      return { type: "none", state: { ...e, zone: "into" } };
    case "ArrowLeft": {
      const o = n[e.targetIndex]?.parentUnique ?? null;
      if (!o) return { type: "none", state: e };
      const r = n.findIndex((i) => i.unique === o);
      return r < 0 ? { type: "none", state: e } : { type: "none", state: { ...e, targetIndex: r, zone: "after" } };
    }
    case " ":
    case "Enter": {
      const o = n[e.targetIndex];
      return !o || o.blocked ? { type: "none", state: e } : { type: "commit", state: e };
    }
    case "Escape":
      return { type: "cancel" };
    default:
      return { type: "none", state: e };
  }
}
const F = (() => {
  const e = document.createElement("div");
  return e.id = "esatto-tree-dnd-live", e.setAttribute("aria-live", "polite"), e.setAttribute("aria-atomic", "true"), e.style.cssText = `
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0;
    overflow: hidden; clip: rect(0 0 0 0); clip-path: inset(50%); border: 0;
  `, document.body.appendChild(e), e;
})();
function m(e) {
  F.textContent = "", F.textContent = e;
}
const wt = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Enter", "Escape"];
class Tt extends tt(Q) {
  #a;
  #u;
  #t = null;
  #e = null;
  #o = !1;
  #r = null;
  #i = null;
  #d = !1;
  constructor() {
    super(), this.consumeContext(et, (t) => {
      this.#a = t;
    }), this.consumeContext(nt, (t) => {
      this.#u = t;
    });
  }
  connectedCallback() {
    super.connectedCallback(), O(this), this.#y();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), O(null), this.#s();
  }
  attachItem(t) {
    const n = t;
    if (n.dataset[$]) return;
    n.dataset[$] = "1", t.setAttribute("draggable", "true"), T(t), new MutationObserver(() => T(t)).observe(t, { childList: !0, subtree: !0 });
  }
  #y() {
    this.#d || (this.#d = !0, document.addEventListener("dragstart", (t) => {
      const n = this.#n(t);
      n && this.#g(t, n);
    }, !0), document.addEventListener("dragenter", (t) => {
      !this.#n(t) || !this.#t || t.preventDefault();
    }, !0), document.addEventListener("dragover", (t) => {
      const n = this.#n(t);
      if (!n) {
        u();
        return;
      }
      this.#b(t, n);
    }, !0), document.addEventListener("dragleave", (t) => {
      const n = this.#n(t);
      n && this.#w(t, n);
    }, !0), document.addEventListener("drop", (t) => {
      const n = this.#n(t);
      n && this.#T(t, n);
    }, !0), document.addEventListener("dragend", () => this.#x(), !0), document.addEventListener("keydown", (t) => this.#q(t), !0));
  }
  #n(t) {
    for (const n of t.composedPath?.() ?? [])
      if (c(n)) return n;
    return null;
  }
  #l(t) {
    const n = U(t), o = l(t);
    return !n || !o ? null : {
      adapter: n,
      sourceUnique: o,
      sourceParentUnique: w(t),
      descendantUniques: ht(t)
    };
  }
  // What dropping on `el` in `zone` would do, or null if `el` is not a legal
  // target for the current drag (other tree, itself, or one of its descendants).
  #c(t, n) {
    const o = this.#t;
    if (!o || U(t) !== o.adapter) return null;
    const r = l(t);
    return !r || N(r, o.sourceUnique, o.descendantUniques) ? null : pt({
      zone: n,
      sourceParentUnique: o.sourceParentUnique,
      targetUnique: r,
      targetParentUnique: w(t)
    });
  }
  #g(t, n) {
    if (this.#o) {
      t.preventDefault();
      return;
    }
    const o = this.#l(n);
    if (!o) {
      t.preventDefault();
      return;
    }
    t.dataTransfer.effectAllowed = "move", t.dataTransfer.setData(at, o.sourceUnique), this.#t = o;
  }
  #b(t, n) {
    if (!this.#t) return;
    const o = this.#c(n, P(n, t.clientY));
    if (o === null) {
      u();
      return;
    }
    if (this.#v(n), o.kind === "noop") {
      u();
      return;
    }
    t.preventDefault(), t.dataTransfer.dropEffect = "move", G(o, this.#t.adapter);
  }
  #v(t) {
    this.#i !== t && (this.#s(), this.#i = t, this.#r = setTimeout(() => {
      t.hasAttribute("show-children") || t.hasAttribute("is-expanded") || t.hasAttribute("open") || (typeof t.toggleChildren == "function" ? t.toggleChildren() : t.shadowRoot?.querySelector('[name="caret"], [data-mark="chevron"]')?.click?.());
    }, ct));
  }
  #s() {
    this.#r && clearTimeout(this.#r), this.#r = null, this.#i = null;
  }
  #w(t, n) {
    t.relatedTarget && n.contains(t.relatedTarget) || (u(), this.#i === n && this.#s());
  }
  async #T(t, n) {
    const o = this.#c(n, P(n, t.clientY));
    _(o) && (t.preventDefault(), u(), await this.#f(o));
  }
  #x() {
    this.#t = null, u(), this.#s();
  }
  // Run a move plan for the current #dragState. Returns whether it succeeded.
  // Both affected branches are reloaded either way: on success the tree
  // re-sorts them; on failure it re-syncs with the server (e.g. a cycle
  // through a collapsed branch the client couldn't see).
  async #f(t) {
    const n = this.#t;
    if (!n || this.#o) return !1;
    this.#o = !0;
    const o = y(n.sourceUnique, n.adapter.entityType);
    o && (o.style.opacity = "0.4", gt(o));
    let r = !1;
    try {
      await n.adapter.move(this, n.sourceUnique, t.parentUnique), r = !0;
    } catch (i) {
      this.#U(i?.message ?? String(i));
    } finally {
      this.#m(n.adapter, n.sourceParentUnique), this.#m(n.adapter, t.parentUnique), bt(), o && (o.style.opacity = ""), this.#o = !1;
    }
    return r;
  }
  // --- Keyboard "grab & place" ------------------------------------------------
  // All rendered rows of the grabbed item's tree, in visual order, with a
  // parallel element array so a reducer index maps back to a DOM row.
  #h() {
    const t = this.#t, n = [], o = [];
    if (!t) return { candidates: n, els: o };
    for (const r of Y(t.adapter.entityType)) {
      const i = l(r);
      i && (n.push({
        unique: i,
        parentUnique: w(r),
        blocked: N(i, t.sourceUnique, t.descendantUniques)
      }), o.push(r));
    }
    return { candidates: n, els: o };
  }
  #E(t, n, o) {
    const r = this.#t.adapter, i = t.kind === "move" && t.parentUnique !== null ? y(t.parentUnique, r.entityType) : null;
    return mt(t, n, b(o), i ? b(i) : null);
  }
  #p() {
    u(), this.#e = null, this.#t = null;
  }
  #q(t) {
    if (this.#e === null) {
      if (t.key !== " " || this.#o) return;
      const p = this.#n(t);
      if (!p) return;
      const a = this.#l(p);
      if (!a) return;
      t.preventDefault(), t.stopPropagation(), this.#t = a;
      const { candidates: g } = this.#h(), W = g.findIndex((J) => J.unique === a.sourceUnique);
      this.#e = { sourceUnique: a.sourceUnique, targetIndex: Math.max(W, 0), zone: "before" }, u(), m(`Grabbed ${b(p)}. Use arrow keys to choose where to move it, space to drop, escape to cancel.`);
      return;
    }
    if (!wt.includes(t.key)) return;
    t.preventDefault(), t.stopPropagation();
    const { candidates: n, els: o } = this.#h();
    if (n.length === 0) {
      this.#p();
      return;
    }
    const r = Math.min(Math.max(this.#e.targetIndex, 0), n.length - 1), i = vt({ ...this.#e, targetIndex: r }, t.key, n);
    if (i.type === "cancel") {
      this.#p(), m("Move cancelled.");
      return;
    }
    const s = o[i.state.targetIndex], h = s ? this.#c(s, i.state.zone) : null;
    if (i.type === "none") {
      if (this.#e = i.state, !s || !h) {
        u();
        return;
      }
      G(h, this.#t.adapter), m(this.#E(h, i.state.zone, s));
      return;
    }
    if (!_(h)) {
      m("Nothing to move here. Choose another position, or press escape to cancel.");
      return;
    }
    const { sourceUnique: k, adapter: M } = this.#t, S = b(y(k, M.entityType));
    this.#e = null, u(), (async () => {
      const p = await this.#f(h);
      m(p ? `Moved ${S}.` : `Could not move ${S}.`);
      const a = y(k, M.entityType), g = a?.querySelector("[tabindex],a,button");
      g?.focus ? g.focus() : a?.focus?.(), this.#t = null;
    })();
  }
  // --- Server sync -------------------------------------------------------------
  #m(t, n) {
    if (!this.#u) {
      console.warn(`${I} reload: action-event context not available — tree won't auto-refresh`);
      return;
    }
    this.#u.dispatchEvent(
      new ot({
        unique: n,
        entityType: n ? t.entityType : t.rootEntityType
      })
    );
  }
  #U(t) {
    console.error(I, t), this.#a?.peek("danger", { data: { message: t } });
  }
  render() {
    return null;
  }
}
function xt(e) {
  st(e), dt(), Et(), V();
}
function Et() {
  if (customElements.get(v) || customElements.define(v, Tt), document.querySelector(v)) return;
  const e = document.createElement(v);
  e.style.display = "none";
  const t = document.querySelector("umb-app");
  if (t) {
    t.appendChild(e);
    return;
  }
  document.body.appendChild(e);
  const n = new MutationObserver(() => {
    const o = document.querySelector("umb-app");
    o && (o.appendChild(e), n.disconnect());
  });
  n.observe(document.body, { childList: !0 });
}
const qt = {
  entityType: "dictionary",
  // The dictionary menu item sets hideTreeRoot: true, so no row carries this
  // entity type; it is only used for top-level reload events.
  rootEntityType: "dictionary-root",
  // The dictionary tree registers its tree items with kind: 'default'.
  innerTag: "umb-default-tree-item",
  async move(e, t, n) {
    const { error: o } = await it(
      e,
      rt.putDictionaryByIdMove({
        path: { id: t },
        body: { target: n ? { id: n } : null }
      }),
      { disableNotifications: !0 }
    );
    if (o) throw o;
  }
};
xt(qt);
//# sourceMappingURL=dictionary-tree-drag-drop.js.map
