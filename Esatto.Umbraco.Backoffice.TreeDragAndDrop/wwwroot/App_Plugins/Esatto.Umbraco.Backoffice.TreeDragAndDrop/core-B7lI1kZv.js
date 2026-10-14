import { LitElement as re } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as oe } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as ie } from "@umbraco-cms/backoffice/notification";
import { UMB_ACTION_EVENT_CONTEXT as se } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadChildrenOfEntityEvent as ue } from "@umbraco-cms/backoffice/entity-action";
import { tryExecute as V } from "@umbraco-cms/backoffice/resources";
const Z = /* @__PURE__ */ new Map();
function ae(t) {
  Z.set(t.entityType, t);
}
function J(t) {
  return t ? Z.get(t) : void 0;
}
function D(t) {
  if (!t) return null;
  if (t.parentElement) return t.parentElement;
  const e = t.getRootNode?.();
  return e && e.host ? e.host : null;
}
function b(t, e) {
  if (!t || t.nodeType !== 1) return;
  const n = t;
  e(n);
  for (const r of n.children)
    b(r, e);
  if (n.shadowRoot)
    for (const r of n.shadowRoot.children)
      b(r, e);
}
function U(t) {
  return t.getAttribute("entitytype") ?? t.getAttribute("entity-type");
}
function f(t) {
  const e = t;
  return !e || e.nodeType !== 1 || e.tagName !== "UMB-TREE-ITEM" || e.props?.isMenu !== !0 || e.props?.item?.isTrashed === !0 ? !1 : J(U(e)) !== void 0;
}
function ce(t) {
  return t.getClientRects().length > 0;
}
function de(t) {
  const e = A(t)?.innerTag ?? "umb-default-tree-item";
  t.querySelector(`:scope > ${e}`)?.api?.showChildren?.();
}
function A(t) {
  return J(U(t));
}
function m(t) {
  return t.props?.item?.unique ?? t.api?.unique ?? t._item?.unique ?? t.getAttribute?.("data-unique") ?? null;
}
function v(t) {
  const e = t.props?.item?.parent;
  if (e !== void 0)
    return e.unique ?? null;
  let n = D(t);
  for (; n; ) {
    if (f(n)) return m(n);
    n = D(n);
  }
  return null;
}
function E(t) {
  return t?.props?.item?.name ?? "item";
}
function C(t) {
  function e(n) {
    if (!(!n || n.nodeType !== 1)) {
      if (n.tagName !== "SLOT")
        try {
          n.setAttribute("draggable", "true");
        } catch {
        }
      for (const r of n.children) e(r);
      if (n.shadowRoot)
        for (const r of n.shadowRoot.children) e(r);
    }
  }
  for (const n of t.children) e(n);
  if (t.shadowRoot)
    for (const n of t.shadowRoot.children) e(n);
}
function le(t) {
  let e = t;
  for (; e; ) {
    if (f(e)) return e;
    e = D(e);
  }
  return null;
}
function T(t, e) {
  let n = null;
  return b(document.documentElement, (r) => {
    n || f(r) && U(r) === e && m(r) === t && (n = r);
  }), n;
}
function N(t) {
  const e = [];
  return b(document.documentElement, (n) => {
    f(n) && U(n) === t && ce(n) && e.push(n);
  }), e;
}
function fe(t, e) {
  const n = [];
  for (const r of N(e)) {
    if (v(r) !== t) continue;
    const o = m(r);
    o && n.push(o);
  }
  return n;
}
const L = "esattoTreeDndAttached", he = 700, pe = "application/x-esatto-tree-dnd", x = "esatto-tree-dnd", k = "[esatto-tree-dnd]", _ = "__esattoTreeDndPatched", S = /* @__PURE__ */ new WeakSet();
let q = null;
function B(t) {
  q = t, t && Q();
}
function Q() {
  P(document);
}
function P(t) {
  if (!t) return;
  const e = t.nodeType === 9 ? t.documentElement : t;
  b(e, (n) => {
    f(n) && (q?.attachItem(n), C(n)), n.shadowRoot && !S.has(n.shadowRoot) && M(n.shadowRoot);
  });
}
function M(t) {
  if (!t || S.has(t)) return;
  S.add(t), new MutationObserver((n) => {
    for (const r of n) {
      if (r.type === "attributes" && f(r.target)) {
        q?.attachItem(r.target);
        continue;
      }
      for (const o of r.addedNodes) {
        if (!(o instanceof Element)) continue;
        f(o) && q?.attachItem(o), P(o);
        const i = le(o);
        i && (q?.attachItem(i), C(i));
      }
    }
  }).observe(t, {
    childList: !0,
    subtree: !0,
    attributes: !0,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ["entitytype", "entity-type"]
  }), P(t);
}
function me() {
  const t = window;
  if (t[_]) return;
  t[_] = !0;
  const e = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function(n) {
    const r = e.call(this, n);
    try {
      M(r);
    } catch (o) {
      console.warn(`${k} observeRoot failed:`, o);
    }
    return r;
  }, M(document);
}
const ye = "umb-default-tree-item";
function $(t) {
  const e = [...new Set([A(t)?.innerTag, ye].filter((s) => !!s))], r = (t.querySelector(e.map((s) => `:scope > ${s}`).join(", ")) ?? t.shadowRoot?.querySelector(e.join(", ")))?.shadowRoot?.querySelector("uui-menu-item"), o = r?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (o) return o.getBoundingClientRect();
  if (r) {
    const s = r.getBoundingClientRect();
    return new DOMRect(s.left, s.top, s.width, Math.min(s.height, 32));
  }
  const i = t.getBoundingClientRect();
  return new DOMRect(i.left, i.top, i.width, Math.min(i.height, 32));
}
function be(t, e, n) {
  const r = n - t, o = e / 3;
  return r < o ? "before" : r > e - o ? "after" : "into";
}
function z(t, e) {
  const n = $(t);
  return be(n.top, n.height, e);
}
function ge(t) {
  const e = /* @__PURE__ */ new Set();
  return b(t, (n) => {
    if (n !== t && f(n)) {
      const r = m(n);
      r && e.add(r);
    }
  }), e;
}
function G(t, e, n) {
  return t === e || n.has(t);
}
function we(t) {
  const e = t.zone === "into" ? t.targetUnique : t.targetParentUnique;
  return e === t.sourceParentUnique ? { kind: "noop" } : { kind: "move", parentUnique: e };
}
function Te(t) {
  const e = t.targetParentUnique === t.sourceParentUnique;
  if (t.zone === "into")
    return t.targetUnique === t.sourceParentUnique ? { kind: "noop" } : { kind: "move", parentUnique: t.targetUnique };
  const n = ee(t.siblings, t.sourceUnique, t.targetUnique, t.zone);
  return n === null ? e ? { kind: "noop" } : { kind: "move", parentUnique: t.targetParentUnique } : e ? n.length === t.siblings.length && n.every((o, i) => o === t.siblings[i]) ? { kind: "noop" } : { kind: "reorder", parentUnique: t.targetParentUnique, order: n } : { kind: "move+sort", parentUnique: t.targetParentUnique, order: n };
}
function ee(t, e, n, r) {
  const o = t.filter((a) => a !== e), i = o.indexOf(n);
  if (i === -1) return null;
  const s = r === "before" ? i : i + 1;
  return [...o.slice(0, s), e, ...o.slice(s)];
}
function H(t) {
  return t !== null && t.kind !== "noop";
}
function ve(t, e, n, r) {
  return t.kind === "noop" ? "No change: already here" : e === "into" ? `Move into ${n}` : t.kind === "reorder" || t.kind === "move+sort" ? e === "before" ? `Move above ${n}` : `Move below ${n}` : r === null ? `Move to root (next to ${n})` : `Move next to ${n} (under ${r})`;
}
function qe(t, e, n) {
  if (!t || !e || t === e) return;
  const r = e.parentNode;
  r && r.insertBefore(t, n === "before" ? e : e.nextSibling);
}
const F = "var(--uui-color-selected, #3879ff)", Ue = "rgba(56, 121, 255, 0.08)", O = (() => {
  const t = document.createElement("div");
  return t.id = "esatto-tree-dnd-indicator", t.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `, document.body.appendChild(t), t;
})();
function I(t, e) {
  const n = $(t), r = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${n.left}px;
    width: ${n.width}px;
  `;
  if (e === "outline") {
    O.style.cssText = `${r}
      top: ${n.top}px;
      height: ${n.height}px;
      border: 2px solid ${F};
      background: ${Ue};
    `;
    return;
  }
  O.style.cssText = `${r}
    top: ${(e === "line-above" ? n.top : n.bottom) - 1}px;
    height: 2px;
    background: ${F};
  `;
}
function c() {
  O.style.display = "none";
}
function Y(t, e, n, r) {
  if (t.kind === "noop") {
    c();
    return;
  }
  if (t.kind === "reorder" || t.kind === "move+sort") {
    I(n, r === "before" ? "line-above" : "line-below");
    return;
  }
  if (r === "into" || e.sort) {
    I(n, "outline");
    return;
  }
  if (t.parentUnique !== null) {
    const i = T(t.parentUnique, e.entityType);
    i ? I(i, "outline") : c();
    return;
  }
  const o = N(e.entityType).find((i) => v(i) === null);
  o ? I(o, "line-above") : c();
}
const y = (() => {
  const t = document.createElement("div");
  return t.id = "esatto-tree-dnd-spinner", t.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 100000;
    display: none;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
    color: var(--uui-color-selected, #3879ff);
  `, t.innerHTML = '<uui-loader-circle style="font-size: 1.2em;"></uui-loader-circle>', document.body.appendChild(t), t;
})();
function K(t) {
  const e = $(t);
  y.style.left = `${e.left}px`, y.style.top = `${e.top}px`, y.style.width = `${Math.min(e.height, 28)}px`, y.style.height = `${e.height}px`, y.style.display = "flex";
}
function Ee() {
  y.style.display = "none";
}
function X(t, e, n) {
  let r = t + e;
  for (; r >= 0 && r < n.length; ) {
    if (!n[r].blocked) return r;
    r += e;
  }
  return t;
}
function xe(t, e, n) {
  switch (e) {
    case "ArrowDown":
      return { type: "none", state: { ...t, targetIndex: X(t.targetIndex, 1, n), zone: "before" } };
    case "ArrowUp":
      return { type: "none", state: { ...t, targetIndex: X(t.targetIndex, -1, n), zone: "before" } };
    case "ArrowRight":
      return { type: "none", state: { ...t, zone: "into" } };
    case "ArrowLeft": {
      const r = n[t.targetIndex]?.parentUnique ?? null;
      if (!r) return { type: "none", state: t };
      const o = n.findIndex((i) => i.unique === r);
      return o < 0 ? { type: "none", state: t } : { type: "none", state: { ...t, targetIndex: o, zone: "after" } };
    }
    case " ":
    case "Enter": {
      const r = n[t.targetIndex];
      return r ? r.blocked ? { type: "blocked", state: t } : { type: "commit", state: t } : { type: "none", state: t };
    }
    case "Escape":
      return { type: "cancel" };
    default:
      return { type: "none", state: t };
  }
}
const W = (() => {
  const t = document.createElement("div");
  return t.id = "esatto-tree-dnd-live", t.setAttribute("aria-live", "polite"), t.setAttribute("aria-atomic", "true"), t.style.cssText = `
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0;
    overflow: hidden; clip: rect(0 0 0 0); clip-path: inset(50%); border: 0;
  `, document.body.appendChild(t), t;
})();
function p(t) {
  W.textContent = "", W.textContent = t;
}
const Ie = /* @__PURE__ */ new Set([
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
function ke(t) {
  return t.tagName !== void 0 && Ie.has(t.tagName) || t.id === "caret-button" || t.isContentEditable === !0;
}
function Ae(t, e) {
  for (const n of t) {
    if (n === e) return !0;
    if (ke(n)) return !1;
  }
  return !1;
}
function j(t, e) {
  return t.some((n) => f(n) && U(n) === e);
}
const Ce = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Enter", "Escape"];
class Re extends oe(re) {
  #a;
  #c;
  #e = null;
  #t = null;
  #r = !1;
  #o = null;
  #i = null;
  #l = !1;
  constructor() {
    super(), this.consumeContext(ie, (e) => {
      this.#a = e;
    }), this.consumeContext(se, (e) => {
      this.#c = e;
    });
  }
  connectedCallback() {
    super.connectedCallback(), B(this), this.#b();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), B(null), this.#s();
  }
  attachItem(e) {
    const n = e;
    if (n.dataset[L]) return;
    n.dataset[L] = "1", e.setAttribute("draggable", "true"), C(e), new MutationObserver(() => C(e)).observe(e, { childList: !0, subtree: !0 });
  }
  #b() {
    this.#l || (this.#l = !0, document.addEventListener("dragstart", (e) => {
      const n = this.#n(e);
      n && this.#g(e, n);
    }, !0), document.addEventListener("dragenter", (e) => {
      !this.#n(e) || !this.#e || e.preventDefault();
    }, !0), document.addEventListener("dragover", (e) => {
      const n = this.#n(e);
      if (!n) {
        c();
        return;
      }
      this.#w(e, n);
    }, !0), document.addEventListener("dragleave", (e) => {
      const n = this.#n(e);
      n && this.#v(e, n);
    }, !0), document.addEventListener("drop", (e) => {
      const n = this.#n(e);
      n && this.#q(e, n);
    }, !0), document.addEventListener("dragend", () => this.#U(), !0), document.addEventListener("keydown", (e) => this.#x(e), !0), document.addEventListener("pointerdown", (e) => {
      this.#t && this.#e && !j(e.composedPath(), this.#e.adapter.entityType) && (this.#u(), p("Move cancelled."));
    }, !0));
  }
  #n(e) {
    for (const n of e.composedPath?.() ?? [])
      if (f(n)) return n;
    return null;
  }
  #f(e) {
    const n = A(e), r = m(e);
    return !n || !r ? null : {
      adapter: n,
      sourceUnique: r,
      sourceParentUnique: v(e),
      descendantUniques: ge(e)
    };
  }
  // What dropping on `el` in `zone` would do, or null if `el` is not a legal
  // target for the current drag (other tree, itself, or one of its descendants).
  #d(e, n) {
    const r = this.#e;
    if (!r || A(e) !== r.adapter) return null;
    const o = m(e);
    if (!o || G(o, r.sourceUnique, r.descendantUniques)) return null;
    const i = {
      zone: n,
      sourceParentUnique: r.sourceParentUnique,
      targetUnique: o,
      targetParentUnique: v(e)
    };
    if (!r.adapter.sort) return we(i);
    const s = n === "into" ? [] : fe(i.targetParentUnique, r.adapter.entityType);
    return Te({ ...i, sourceUnique: r.sourceUnique, siblings: s });
  }
  #g(e, n) {
    if (this.#r) {
      e.preventDefault();
      return;
    }
    const r = this.#f(n);
    if (!r) {
      e.preventDefault();
      return;
    }
    e.dataTransfer.effectAllowed = "move", e.dataTransfer.setData(pe, r.sourceUnique), this.#e = r;
  }
  #w(e, n) {
    if (!this.#e) return;
    const r = z(n, e.clientY), o = this.#d(n, r);
    if (o === null) {
      c();
      return;
    }
    if (this.#T(n), o.kind === "noop") {
      c();
      return;
    }
    e.preventDefault(), e.dataTransfer.dropEffect = "move", Y(o, this.#e.adapter, n, r);
  }
  #T(e) {
    this.#i !== e && (this.#s(), this.#i = e, this.#o = setTimeout(() => de(e), he));
  }
  #s() {
    this.#o && clearTimeout(this.#o), this.#o = null, this.#i = null;
  }
  #v(e, n) {
    e.relatedTarget && n.contains(e.relatedTarget) || (c(), this.#i === n && this.#s());
  }
  async #q(e, n) {
    const r = z(n, e.clientY), o = this.#d(n, r);
    H(o) && (e.preventDefault(), c(), await this.#h(o, n, r));
  }
  #U() {
    this.#e = null, c(), this.#s();
  }
  // Run a plan for the current #dragState. Returns whether it fully succeeded.
  //   move       → move.
  //   reorder    → optimistic DOM move first (the sort API is slow), then sort
  //                with the parent's full server order.
  //   move+sort  → move, then sort with the new parent's full server order. If
  //                only the sort fails the item stays moved (a completed move
  //                beats a rollback) and a warning says so.
  // Both branches are always reloaded afterwards: chevrons come from the data
  // store, Dictionary re-sorts alphabetically, and after a failure the tree
  // must match the server again (e.g. a cycle through a collapsed branch).
  async #h(e, n, r) {
    const o = this.#e;
    if (!o || this.#r) return !1;
    this.#r = !0;
    const { adapter: i, sourceUnique: s, sourceParentUnique: a } = o, g = m(n), u = T(s, i.entityType);
    u && (u.style.opacity = "0.4", K(u));
    const w = u?.parentNode ?? null, R = u?.nextSibling ?? null, h = () => {
      u && w && w.insertBefore(u, R);
    };
    let d = !1;
    try {
      if (e.kind === "move")
        await i.move(this, s, e.parentUnique), d = !0;
      else if (e.kind === "reorder")
        qe(u, n, r), u && K(u), await this.#p(i, e.parentUnique, s, g, r), d = !0;
      else {
        await i.move(this, s, e.parentUnique);
        try {
          await this.#p(i, e.parentUnique, s, g, r), d = !0;
        } catch (l) {
          this.#I(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${l?.message ?? l})`);
        }
      }
    } catch (l) {
      this.#k(l?.message ?? String(l));
    } finally {
      h(), this.#y(i, a), e.parentUnique !== a && this.#y(i, e.parentUnique), Ee(), u && (u.style.opacity = ""), this.#r = !1;
    }
    return d;
  }
  // Sort `parentUnique`'s children so the source sits above/below the target.
  // The sort endpoint treats each sortOrder as an ABSOLUTE index, and the
  // sidebar may have rendered only a window of a large parent, so the order is
  // built from the parent's full child list on the server — never from the
  // rendered rows. If that order can't be built, nothing is sorted.
  async #p(e, n, r, o, i) {
    if (!e.sort || !e.childOrder) throw new Error("This tree cannot be sorted");
    const s = await e.childOrder(this, n), a = ee(s, r, o, i);
    if (!a) throw new Error("The drop target is no longer under that parent");
    await e.sort(this, n, a);
  }
  // --- Keyboard "grab & place" ------------------------------------------------
  // All rendered rows of the grabbed item's tree, in visual order, with a
  // parallel element array so a reducer index maps back to a DOM row.
  #m() {
    const e = this.#e, n = [], r = [];
    if (!e) return { candidates: n, els: r };
    for (const o of N(e.adapter.entityType)) {
      const i = m(o);
      i && (n.push({
        unique: i,
        parentUnique: v(o),
        blocked: G(i, e.sourceUnique, e.descendantUniques)
      }), r.push(o));
    }
    return { candidates: n, els: r };
  }
  #E(e, n, r) {
    const o = this.#e.adapter, i = e.kind === "move" && e.parentUnique !== null ? T(e.parentUnique, o.entityType) : null;
    return ve(e, n, E(r), i ? E(i) : null);
  }
  #u() {
    c(), this.#t = null, this.#e = null;
  }
  #x(e) {
    if (this.#t === null) {
      if (e.key !== " " || this.#r) return;
      const h = this.#n(e);
      if (!h || !Ae(e.composedPath(), h)) return;
      const d = this.#f(h);
      if (!d) return;
      e.preventDefault(), e.stopPropagation(), this.#e = d;
      const { candidates: l } = this.#m(), te = l.findIndex((ne) => ne.unique === d.sourceUnique);
      this.#t = { sourceUnique: d.sourceUnique, targetIndex: Math.max(te, 0), zone: "before" }, c(), p(`Grabbed ${E(h)}. Use arrow keys to choose where to move it, space to drop, escape to cancel.`);
      return;
    }
    if (!Ce.includes(e.key)) return;
    if (!j(e.composedPath(), this.#e.adapter.entityType)) {
      this.#u(), p("Move cancelled.");
      return;
    }
    e.preventDefault(), e.stopPropagation();
    const { candidates: n, els: r } = this.#m();
    if (n.length === 0) {
      this.#u();
      return;
    }
    const o = Math.min(Math.max(this.#t.targetIndex, 0), n.length - 1), i = xe({ ...this.#t, targetIndex: o }, e.key, n);
    if (i.type === "cancel") {
      this.#u(), p("Move cancelled.");
      return;
    }
    if (i.type === "blocked") {
      p("Can't move an item into itself or its own children. Choose another position, or press escape to cancel.");
      return;
    }
    const s = r[i.state.targetIndex], a = s ? this.#d(s, i.state.zone) : null;
    if (i.type === "none") {
      if (this.#t = i.state, !s || !a) {
        c();
        return;
      }
      Y(a, this.#e.adapter, s, i.state.zone), p(this.#E(a, i.state.zone, s));
      return;
    }
    if (!H(a)) {
      p("Nothing to move here. Choose another position, or press escape to cancel.");
      return;
    }
    const { sourceUnique: g, adapter: u } = this.#e, w = E(T(g, u.entityType)), R = i.state.zone;
    this.#t = null, c(), (async () => {
      const h = await this.#h(a, s, R);
      p(h ? `Moved ${w}.` : `Could not move ${w}.`);
      const d = T(g, u.entityType), l = d?.querySelector("[tabindex],a,button");
      l?.focus ? l.focus() : d?.focus?.(), this.#e = null;
    })();
  }
  // --- Server sync -------------------------------------------------------------
  #y(e, n) {
    if (!this.#c) {
      console.warn(`${k} reload: action-event context not available — tree won't auto-refresh`);
      return;
    }
    this.#c.dispatchEvent(
      new ue({
        unique: n,
        entityType: n ? e.entityType : e.rootEntityType
      })
    );
  }
  #I(e) {
    console.warn(k, e), this.#a?.peek("warning", { data: { message: e } });
  }
  #k(e) {
    console.error(k, e), this.#a?.peek("danger", { data: { message: e } });
  }
  render() {
    return null;
  }
}
function Le(t) {
  ae(t), me(), De(), Q();
}
function De() {
  if (customElements.get(x) || customElements.define(x, Re), document.querySelector(x)) return;
  const t = document.createElement(x);
  t.style.display = "none";
  const e = document.querySelector("umb-app");
  if (e) {
    e.appendChild(t);
    return;
  }
  document.body.appendChild(t);
  const n = new MutationObserver(() => {
    const r = document.querySelector("umb-app");
    r && (r.appendChild(t), n.disconnect());
  });
  n.observe(document.body, { childList: !0 });
}
async function _e(t, e) {
  const { error: n } = await V(t, e, { disableNotifications: !0 });
  if (n) throw n;
}
async function Be(t, e) {
  const { data: n, error: r } = await V(t, e, { disableNotifications: !0 });
  if (r) throw r;
  if (n == null) throw new Error("Empty response from the server");
  return n;
}
export {
  Be as a,
  Le as b,
  _e as r
};
//# sourceMappingURL=core-B7lI1kZv.js.map
