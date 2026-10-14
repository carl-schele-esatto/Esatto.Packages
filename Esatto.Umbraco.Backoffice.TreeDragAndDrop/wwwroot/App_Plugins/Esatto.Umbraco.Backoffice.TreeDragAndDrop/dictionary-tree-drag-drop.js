import { LitElement as Q } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as tt } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as et } from "@umbraco-cms/backoffice/notification";
import { UMB_ACTION_EVENT_CONTEXT as nt } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadChildrenOfEntityEvent as rt } from "@umbraco-cms/backoffice/entity-action";
import { DictionaryService as ot } from "@umbraco-cms/backoffice/external/backend-api";
import { tryExecute as it } from "@umbraco-cms/backoffice/resources";
const K = /* @__PURE__ */ new Map();
function st(e) {
  K.set(e.entityType, e);
}
function X(e) {
  return e ? K.get(e) : void 0;
}
function U(e) {
  if (!e) return null;
  if (e.parentElement) return e.parentElement;
  const t = e.getRootNode?.();
  return t && t.host ? t.host : null;
}
function h(e, t) {
  if (!e || e.nodeType !== 1) return;
  const n = e;
  t(n);
  for (const r of n.children)
    h(r, t);
  if (n.shadowRoot)
    for (const r of n.shadowRoot.children)
      h(r, t);
}
function b(e) {
  return e.getAttribute("entitytype") ?? e.getAttribute("entity-type");
}
function c(e) {
  const t = e;
  return !t || t.nodeType !== 1 || t.tagName !== "UMB-TREE-ITEM" || t.props?.isMenu !== !0 ? !1 : X(b(t)) !== void 0;
}
function ut(e) {
  return e.getClientRects().length > 0;
}
function ct(e) {
  const t = x(e)?.innerTag ?? "umb-default-tree-item";
  e.querySelector(`:scope > ${t}`)?.api?.showChildren?.();
}
function x(e) {
  return X(b(e));
}
function p(e) {
  return e.props?.item?.unique ?? e.api?.unique ?? e._item?.unique ?? e.getAttribute?.("data-unique") ?? null;
}
function E(e) {
  const t = e.props?.item?.parent;
  if (t !== void 0)
    return t.unique ?? null;
  let n = U(e);
  for (; n; ) {
    if (c(n)) return p(n);
    n = U(n);
  }
  return null;
}
function v(e) {
  return e?.props?.item?.name ?? "item";
}
function q(e) {
  function t(n) {
    if (!(!n || n.nodeType !== 1)) {
      if (n.tagName !== "SLOT")
        try {
          n.setAttribute("draggable", "true");
        } catch {
        }
      for (const r of n.children) t(r);
      if (n.shadowRoot)
        for (const r of n.shadowRoot.children) t(r);
    }
  }
  for (const n of e.children) t(n);
  if (e.shadowRoot)
    for (const n of e.shadowRoot.children) t(n);
}
function at(e) {
  let t = e;
  for (; t; ) {
    if (c(t)) return t;
    t = U(t);
  }
  return null;
}
function y(e, t) {
  let n = null;
  return h(document.documentElement, (r) => {
    n || c(r) && b(r) === t && p(r) === e && (n = r);
  }), n;
}
function j(e) {
  const t = [];
  return h(document.documentElement, (n) => {
    c(n) && b(n) === e && ut(n) && t.push(n);
  }), t;
}
const N = "esattoTreeDndAttached", dt = 700, lt = "application/x-esatto-tree-dnd", w = "esatto-tree-dnd", I = "[esatto-tree-dnd]", O = "__esattoTreeDndPatched", C = /* @__PURE__ */ new WeakSet();
let g = null;
function L(e) {
  g = e, e && W();
}
function W() {
  A(document);
}
function A(e) {
  if (!e) return;
  const t = e.nodeType === 9 ? e.documentElement : e;
  h(t, (n) => {
    c(n) && (g?.attachItem(n), q(n)), n.shadowRoot && !C.has(n.shadowRoot) && R(n.shadowRoot);
  });
}
function R(e) {
  if (!e || C.has(e)) return;
  C.add(e), new MutationObserver((n) => {
    for (const r of n) {
      if (r.type === "attributes" && c(r.target)) {
        g?.attachItem(r.target);
        continue;
      }
      for (const o of r.addedNodes) {
        if (!(o instanceof Element)) continue;
        c(o) && g?.attachItem(o), A(o);
        const i = at(o);
        i && (g?.attachItem(i), q(i));
      }
    }
  }).observe(e, {
    childList: !0,
    subtree: !0,
    attributes: !0,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ["entitytype", "entity-type"]
  }), A(e);
}
function ft() {
  const e = window;
  if (e[O]) return;
  e[O] = !0;
  const t = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function(n) {
    const r = t.call(this, n);
    try {
      R(r);
    } catch (o) {
      console.warn(`${I} observeRoot failed:`, o);
    }
    return r;
  }, R(document);
}
const pt = "umb-default-tree-item";
function D(e) {
  const t = [...new Set([x(e)?.innerTag, pt].filter((s) => !!s))], r = (e.querySelector(t.map((s) => `:scope > ${s}`).join(", ")) ?? e.shadowRoot?.querySelector(t.join(", ")))?.shadowRoot?.querySelector("uui-menu-item"), o = r?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (o) return o.getBoundingClientRect();
  if (r) {
    const s = r.getBoundingClientRect();
    return new DOMRect(s.left, s.top, s.width, Math.min(s.height, 32));
  }
  const i = e.getBoundingClientRect();
  return new DOMRect(i.left, i.top, i.width, Math.min(i.height, 32));
}
function ht(e, t, n) {
  const r = n - e, o = t / 3;
  return r < o ? "before" : r > t - o ? "after" : "into";
}
function P(e, t) {
  const n = D(e);
  return ht(n.top, n.height, t);
}
function mt(e) {
  const t = /* @__PURE__ */ new Set();
  return h(e, (n) => {
    if (n !== e && c(n)) {
      const r = p(n);
      r && t.add(r);
    }
  }), t;
}
function $(e, t, n) {
  return e === t || n.has(e);
}
function yt(e) {
  const t = e.zone === "into" ? e.targetUnique : e.targetParentUnique;
  return t === e.sourceParentUnique ? { kind: "noop" } : { kind: "move", parentUnique: t };
}
function _(e) {
  return e?.kind === "move";
}
function gt(e, t, n, r) {
  return e.kind === "noop" ? "No change: already here" : t === "into" ? `Move into ${n}` : r === null ? `Move to root (next to ${n})` : `Move next to ${n} (under ${r})`;
}
const B = "var(--uui-color-selected, #3879ff)", bt = "rgba(56, 121, 255, 0.08)", V = (() => {
  const e = document.createElement("div");
  return e.id = "esatto-tree-dnd-indicator", e.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `, document.body.appendChild(e), e;
})();
function z(e, t) {
  const n = D(e), r = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${n.left}px;
    width: ${n.width}px;
  `;
  V.style.cssText = t === "line-above" ? `${r}
      top: ${n.top - 1}px;
      height: 2px;
      background: ${B};
    ` : `${r}
      top: ${n.top}px;
      height: ${n.height}px;
      border: 2px solid ${B};
      background: ${bt};
    `;
}
function u() {
  V.style.display = "none";
}
function G(e, t) {
  if (e.kind === "noop") {
    u();
    return;
  }
  if (e.parentUnique !== null) {
    const r = y(e.parentUnique, t.entityType);
    r ? z(r, "outline") : u();
    return;
  }
  const n = j(t.entityType).find((r) => E(r) === null);
  n ? z(n, "line-above") : u();
}
const f = (() => {
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
function Tt(e) {
  const t = D(e);
  f.style.left = `${t.left}px`, f.style.top = `${t.top}px`, f.style.width = `${Math.min(t.height, 28)}px`, f.style.height = `${t.height}px`, f.style.display = "flex";
}
function vt() {
  f.style.display = "none";
}
function H(e, t, n) {
  let r = e + t;
  for (; r >= 0 && r < n.length; ) {
    if (!n[r].blocked) return r;
    r += t;
  }
  return e;
}
function wt(e, t, n) {
  switch (t) {
    case "ArrowDown":
      return { type: "none", state: { ...e, targetIndex: H(e.targetIndex, 1, n), zone: "before" } };
    case "ArrowUp":
      return { type: "none", state: { ...e, targetIndex: H(e.targetIndex, -1, n), zone: "before" } };
    case "ArrowRight":
      return { type: "none", state: { ...e, zone: "into" } };
    case "ArrowLeft": {
      const r = n[e.targetIndex]?.parentUnique ?? null;
      if (!r) return { type: "none", state: e };
      const o = n.findIndex((i) => i.unique === r);
      return o < 0 ? { type: "none", state: e } : { type: "none", state: { ...e, targetIndex: o, zone: "after" } };
    }
    case " ":
    case "Enter": {
      const r = n[e.targetIndex];
      return r ? r.blocked ? { type: "blocked", state: e } : { type: "commit", state: e } : { type: "none", state: e };
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
function a(e) {
  F.textContent = "", F.textContent = e;
}
const Et = /* @__PURE__ */ new Set([
  "UMB-ENTITY-ACTIONS-BUNDLE",
  // "…" button and the row's first action
  "UMB-ENTITY-ACTIONS-DROPDOWN",
  // the open actions menu
  "UUI-ACTION-BAR",
  "UUI-BUTTON",
  // e.g. "Load more"
  "INPUT",
  "TEXTAREA",
  "SELECT"
]);
function xt(e) {
  return e.tagName !== void 0 && Et.has(e.tagName) || e.id === "caret-button" || e.isContentEditable === !0;
}
function qt(e, t) {
  for (const n of e) {
    if (n === t) return !0;
    if (xt(n)) return !1;
  }
  return !1;
}
function Y(e, t) {
  return e.some((n) => c(n) && b(n) === t);
}
const Ut = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Enter", "Escape"];
class It extends tt(Q) {
  #d;
  #c;
  #t = null;
  #e = null;
  #r = !1;
  #o = null;
  #i = null;
  #l = !1;
  constructor() {
    super(), this.consumeContext(et, (t) => {
      this.#d = t;
    }), this.consumeContext(nt, (t) => {
      this.#c = t;
    });
  }
  connectedCallback() {
    super.connectedCallback(), L(this), this.#y();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), L(null), this.#s();
  }
  attachItem(t) {
    const n = t;
    if (n.dataset[N]) return;
    n.dataset[N] = "1", t.setAttribute("draggable", "true"), q(t), new MutationObserver(() => q(t)).observe(t, { childList: !0, subtree: !0 });
  }
  #y() {
    this.#l || (this.#l = !0, document.addEventListener("dragstart", (t) => {
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
      n && this.#v(t, n);
    }, !0), document.addEventListener("drop", (t) => {
      const n = this.#n(t);
      n && this.#w(t, n);
    }, !0), document.addEventListener("dragend", () => this.#E(), !0), document.addEventListener("keydown", (t) => this.#q(t), !0), document.addEventListener("pointerdown", (t) => {
      this.#e && this.#t && !Y(t.composedPath(), this.#t.adapter.entityType) && (this.#u(), a("Move cancelled."));
    }, !0));
  }
  #n(t) {
    for (const n of t.composedPath?.() ?? [])
      if (c(n)) return n;
    return null;
  }
  #f(t) {
    const n = x(t), r = p(t);
    return !n || !r ? null : {
      adapter: n,
      sourceUnique: r,
      sourceParentUnique: E(t),
      descendantUniques: mt(t)
    };
  }
  // What dropping on `el` in `zone` would do, or null if `el` is not a legal
  // target for the current drag (other tree, itself, or one of its descendants).
  #a(t, n) {
    const r = this.#t;
    if (!r || x(t) !== r.adapter) return null;
    const o = p(t);
    return !o || $(o, r.sourceUnique, r.descendantUniques) ? null : yt({
      zone: n,
      sourceParentUnique: r.sourceParentUnique,
      targetUnique: o,
      targetParentUnique: E(t)
    });
  }
  #g(t, n) {
    if (this.#r) {
      t.preventDefault();
      return;
    }
    const r = this.#f(n);
    if (!r) {
      t.preventDefault();
      return;
    }
    t.dataTransfer.effectAllowed = "move", t.dataTransfer.setData(lt, r.sourceUnique), this.#t = r;
  }
  #b(t, n) {
    if (!this.#t) return;
    const r = this.#a(n, P(n, t.clientY));
    if (r === null) {
      u();
      return;
    }
    if (this.#T(n), r.kind === "noop") {
      u();
      return;
    }
    t.preventDefault(), t.dataTransfer.dropEffect = "move", G(r, this.#t.adapter);
  }
  #T(t) {
    this.#i !== t && (this.#s(), this.#i = t, this.#o = setTimeout(() => ct(t), dt));
  }
  #s() {
    this.#o && clearTimeout(this.#o), this.#o = null, this.#i = null;
  }
  #v(t, n) {
    t.relatedTarget && n.contains(t.relatedTarget) || (u(), this.#i === n && this.#s());
  }
  async #w(t, n) {
    const r = this.#a(n, P(n, t.clientY));
    _(r) && (t.preventDefault(), u(), await this.#p(r));
  }
  #E() {
    this.#t = null, u(), this.#s();
  }
  // Run a move plan for the current #dragState. Returns whether it succeeded.
  // Both affected branches are reloaded either way: on success the tree
  // re-sorts them; on failure it re-syncs with the server (e.g. a cycle
  // through a collapsed branch the client couldn't see).
  async #p(t) {
    const n = this.#t;
    if (!n || this.#r) return !1;
    this.#r = !0;
    const r = y(n.sourceUnique, n.adapter.entityType);
    r && (r.style.opacity = "0.4", Tt(r));
    let o = !1;
    try {
      await n.adapter.move(this, n.sourceUnique, t.parentUnique), o = !0;
    } catch (i) {
      this.#U(i?.message ?? String(i));
    } finally {
      this.#m(n.adapter, n.sourceParentUnique), this.#m(n.adapter, t.parentUnique), vt(), r && (r.style.opacity = ""), this.#r = !1;
    }
    return o;
  }
  // --- Keyboard "grab & place" ------------------------------------------------
  // All rendered rows of the grabbed item's tree, in visual order, with a
  // parallel element array so a reducer index maps back to a DOM row.
  #h() {
    const t = this.#t, n = [], r = [];
    if (!t) return { candidates: n, els: r };
    for (const o of j(t.adapter.entityType)) {
      const i = p(o);
      i && (n.push({
        unique: i,
        parentUnique: E(o),
        blocked: $(i, t.sourceUnique, t.descendantUniques)
      }), r.push(o));
    }
    return { candidates: n, els: r };
  }
  #x(t, n, r) {
    const o = this.#t.adapter, i = t.kind === "move" && t.parentUnique !== null ? y(t.parentUnique, o.entityType) : null;
    return gt(t, n, v(r), i ? v(i) : null);
  }
  #u() {
    u(), this.#e = null, this.#t = null;
  }
  #q(t) {
    if (this.#e === null) {
      if (t.key !== " " || this.#r) return;
      const l = this.#n(t);
      if (!l || !qt(t.composedPath(), l)) return;
      const d = this.#f(l);
      if (!d) return;
      t.preventDefault(), t.stopPropagation(), this.#t = d;
      const { candidates: T } = this.#h(), Z = T.findIndex((J) => J.unique === d.sourceUnique);
      this.#e = { sourceUnique: d.sourceUnique, targetIndex: Math.max(Z, 0), zone: "before" }, u(), a(`Grabbed ${v(l)}. Use arrow keys to choose where to move it, space to drop, escape to cancel.`);
      return;
    }
    if (!Ut.includes(t.key)) return;
    if (!Y(t.composedPath(), this.#t.adapter.entityType)) {
      this.#u(), a("Move cancelled.");
      return;
    }
    t.preventDefault(), t.stopPropagation();
    const { candidates: n, els: r } = this.#h();
    if (n.length === 0) {
      this.#u();
      return;
    }
    const o = Math.min(Math.max(this.#e.targetIndex, 0), n.length - 1), i = wt({ ...this.#e, targetIndex: o }, t.key, n);
    if (i.type === "cancel") {
      this.#u(), a("Move cancelled.");
      return;
    }
    if (i.type === "blocked") {
      a("Can't move an item into itself or its own children. Choose another position, or press escape to cancel.");
      return;
    }
    const s = r[i.state.targetIndex], m = s ? this.#a(s, i.state.zone) : null;
    if (i.type === "none") {
      if (this.#e = i.state, !s || !m) {
        u();
        return;
      }
      G(m, this.#t.adapter), a(this.#x(m, i.state.zone, s));
      return;
    }
    if (!_(m)) {
      a("Nothing to move here. Choose another position, or press escape to cancel.");
      return;
    }
    const { sourceUnique: S, adapter: k } = this.#t, M = v(y(S, k.entityType));
    this.#e = null, u(), (async () => {
      const l = await this.#p(m);
      a(l ? `Moved ${M}.` : `Could not move ${M}.`);
      const d = y(S, k.entityType), T = d?.querySelector("[tabindex],a,button");
      T?.focus ? T.focus() : d?.focus?.(), this.#t = null;
    })();
  }
  // --- Server sync -------------------------------------------------------------
  #m(t, n) {
    if (!this.#c) {
      console.warn(`${I} reload: action-event context not available — tree won't auto-refresh`);
      return;
    }
    this.#c.dispatchEvent(
      new rt({
        unique: n,
        entityType: n ? t.entityType : t.rootEntityType
      })
    );
  }
  #U(t) {
    console.error(I, t), this.#d?.peek("danger", { data: { message: t } });
  }
  render() {
    return null;
  }
}
function Ct(e) {
  st(e), ft(), At(), W();
}
function At() {
  if (customElements.get(w) || customElements.define(w, It), document.querySelector(w)) return;
  const e = document.createElement(w);
  e.style.display = "none";
  const t = document.querySelector("umb-app");
  if (t) {
    t.appendChild(e);
    return;
  }
  document.body.appendChild(e);
  const n = new MutationObserver(() => {
    const r = document.querySelector("umb-app");
    r && (r.appendChild(e), n.disconnect());
  });
  n.observe(document.body, { childList: !0 });
}
const Rt = {
  entityType: "dictionary",
  // The dictionary menu item sets hideTreeRoot: true, so no row carries this
  // entity type; it is only used for top-level reload events.
  rootEntityType: "dictionary-root",
  // The dictionary tree registers its tree items with kind: 'default'.
  innerTag: "umb-default-tree-item",
  async move(e, t, n) {
    const { error: r } = await it(
      e,
      ot.putDictionaryByIdMove({
        path: { id: t },
        body: { target: n ? { id: n } : null }
      }),
      { disableNotifications: !0 }
    );
    if (r) throw r;
  }
};
Ct(Rt);
//# sourceMappingURL=dictionary-tree-drag-drop.js.map
