import { LitElement as te } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as ne } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as re } from "@umbraco-cms/backoffice/notification";
import { UMB_ACTION_EVENT_CONTEXT as oe } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadChildrenOfEntityEvent as ie } from "@umbraco-cms/backoffice/entity-action";
import { tryExecute as se } from "@umbraco-cms/backoffice/resources";
const V = /* @__PURE__ */ new Map();
function ue(t) {
  V.set(t.entityType, t);
}
function Z(t) {
  return t ? V.get(t) : void 0;
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
function l(t) {
  const e = t;
  return !e || e.nodeType !== 1 || e.tagName !== "UMB-TREE-ITEM" || e.props?.isMenu !== !0 || e.props?.item?.isTrashed === !0 ? !1 : Z(U(e)) !== void 0;
}
function ae(t) {
  return t.getClientRects().length > 0;
}
function ce(t) {
  const e = R(t)?.innerTag ?? "umb-default-tree-item";
  t.querySelector(`:scope > ${e}`)?.api?.showChildren?.();
}
function R(t) {
  return Z(U(t));
}
function y(t) {
  return t.props?.item?.unique ?? t.api?.unique ?? t._item?.unique ?? t.getAttribute?.("data-unique") ?? null;
}
function v(t) {
  const e = t.props?.item?.parent;
  if (e !== void 0)
    return e.unique ?? null;
  let n = D(t);
  for (; n; ) {
    if (l(n)) return y(n);
    n = D(n);
  }
  return null;
}
function x(t) {
  return t?.props?.item?.name ?? "item";
}
function A(t) {
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
function de(t) {
  let e = t;
  for (; e; ) {
    if (l(e)) return e;
    e = D(e);
  }
  return null;
}
function w(t, e) {
  let n = null;
  return b(document.documentElement, (r) => {
    n || l(r) && U(r) === e && y(r) === t && (n = r);
  }), n;
}
function O(t) {
  const e = [];
  return b(document.documentElement, (n) => {
    l(n) && U(n) === t && ae(n) && e.push(n);
  }), e;
}
function le(t, e) {
  const n = [];
  for (const r of O(e)) {
    if (v(r) !== t) continue;
    const i = y(r);
    i && n.push(i);
  }
  return n;
}
const L = "esattoTreeDndAttached", fe = 700, he = "application/x-esatto-tree-dnd", I = "esatto-tree-dnd", C = "[esatto-tree-dnd]", _ = "__esattoTreeDndPatched", S = /* @__PURE__ */ new WeakSet();
let q = null;
function z(t) {
  q = t, t && J();
}
function J() {
  P(document);
}
function P(t) {
  if (!t) return;
  const e = t.nodeType === 9 ? t.documentElement : t;
  b(e, (n) => {
    l(n) && (q?.attachItem(n), A(n)), n.shadowRoot && !S.has(n.shadowRoot) && M(n.shadowRoot);
  });
}
function M(t) {
  if (!t || S.has(t)) return;
  S.add(t), new MutationObserver((n) => {
    for (const r of n) {
      if (r.type === "attributes" && l(r.target)) {
        q?.attachItem(r.target);
        continue;
      }
      for (const i of r.addedNodes) {
        if (!(i instanceof Element)) continue;
        l(i) && q?.attachItem(i), P(i);
        const o = de(i);
        o && (q?.attachItem(o), A(o));
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
function pe() {
  const t = window;
  if (t[_]) return;
  t[_] = !0;
  const e = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function(n) {
    const r = e.call(this, n);
    try {
      M(r);
    } catch (i) {
      console.warn(`${C} observeRoot failed:`, i);
    }
    return r;
  }, M(document);
}
const me = "umb-default-tree-item";
function $(t) {
  const e = [...new Set([R(t)?.innerTag, me].filter((s) => !!s))], r = (t.querySelector(e.map((s) => `:scope > ${s}`).join(", ")) ?? t.shadowRoot?.querySelector(e.join(", ")))?.shadowRoot?.querySelector("uui-menu-item"), i = r?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (i) return i.getBoundingClientRect();
  if (r) {
    const s = r.getBoundingClientRect();
    return new DOMRect(s.left, s.top, s.width, Math.min(s.height, 32));
  }
  const o = t.getBoundingClientRect();
  return new DOMRect(o.left, o.top, o.width, Math.min(o.height, 32));
}
function ye(t, e, n) {
  const r = n - t, i = e / 3;
  return r < i ? "before" : r > e - i ? "after" : "into";
}
function B(t, e) {
  const n = $(t);
  return ye(n.top, n.height, e);
}
function ge(t) {
  const e = /* @__PURE__ */ new Set();
  return b(t, (n) => {
    if (n !== t && l(n)) {
      const r = y(n);
      r && e.add(r);
    }
  }), e;
}
function G(t, e, n) {
  return t === e || n.has(t);
}
function be(t) {
  const e = t.zone === "into" ? t.targetUnique : t.targetParentUnique;
  return e === t.sourceParentUnique ? { kind: "noop" } : { kind: "move", parentUnique: e };
}
function Te(t) {
  const e = t.targetParentUnique === t.sourceParentUnique;
  if (t.zone === "into")
    return t.targetUnique === t.sourceParentUnique ? { kind: "noop" } : { kind: "move", parentUnique: t.targetUnique };
  const n = t.siblings.filter((c) => c !== t.sourceUnique), r = n.indexOf(t.targetUnique);
  if (r === -1)
    return e ? { kind: "noop" } : { kind: "move", parentUnique: t.targetParentUnique };
  const i = t.zone === "before" ? r : r + 1, o = [...n.slice(0, i), t.sourceUnique, ...n.slice(i)];
  return e ? o.length === t.siblings.length && o.every((c, u) => c === t.siblings[u]) ? { kind: "noop" } : { kind: "reorder", parentUnique: t.targetParentUnique, order: o } : { kind: "move+sort", parentUnique: t.targetParentUnique, order: o };
}
function H(t) {
  return t !== null && t.kind !== "noop";
}
function we(t, e, n, r) {
  return t.kind === "noop" ? "No change: already here" : e === "into" ? `Move into ${n}` : t.kind === "reorder" || t.kind === "move+sort" ? e === "before" ? `Move above ${n}` : `Move below ${n}` : r === null ? `Move to root (next to ${n})` : `Move next to ${n} (under ${r})`;
}
function ve(t, e, n) {
  if (!t || !e || t === e) return;
  const r = e.parentNode;
  r && r.insertBefore(t, n === "before" ? e : e.nextSibling);
}
const F = "var(--uui-color-selected, #3879ff)", qe = "rgba(56, 121, 255, 0.08)", N = (() => {
  const t = document.createElement("div");
  return t.id = "esatto-tree-dnd-indicator", t.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `, document.body.appendChild(t), t;
})();
function k(t, e) {
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
    N.style.cssText = `${r}
      top: ${n.top}px;
      height: ${n.height}px;
      border: 2px solid ${F};
      background: ${qe};
    `;
    return;
  }
  N.style.cssText = `${r}
    top: ${(e === "line-above" ? n.top : n.bottom) - 1}px;
    height: 2px;
    background: ${F};
  `;
}
function a() {
  N.style.display = "none";
}
function Y(t, e, n, r) {
  if (t.kind === "noop") {
    a();
    return;
  }
  if (t.kind === "reorder" || t.kind === "move+sort") {
    k(n, r === "before" ? "line-above" : "line-below");
    return;
  }
  if (r === "into" || e.sort) {
    k(n, "outline");
    return;
  }
  if (t.parentUnique !== null) {
    const o = w(t.parentUnique, e.entityType);
    o ? k(o, "outline") : a();
    return;
  }
  const i = O(e.entityType).find((o) => v(o) === null);
  i ? k(i, "line-above") : a();
}
const g = (() => {
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
  g.style.left = `${e.left}px`, g.style.top = `${e.top}px`, g.style.width = `${Math.min(e.height, 28)}px`, g.style.height = `${e.height}px`, g.style.display = "flex";
}
function Ue() {
  g.style.display = "none";
}
function X(t, e, n) {
  let r = t + e;
  for (; r >= 0 && r < n.length; ) {
    if (!n[r].blocked) return r;
    r += e;
  }
  return t;
}
function Ee(t, e, n) {
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
      const i = n.findIndex((o) => o.unique === r);
      return i < 0 ? { type: "none", state: t } : { type: "none", state: { ...t, targetIndex: i, zone: "after" } };
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
const xe = /* @__PURE__ */ new Set([
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
function Ie(t) {
  return t.tagName !== void 0 && xe.has(t.tagName) || t.id === "caret-button" || t.isContentEditable === !0;
}
function ke(t, e) {
  for (const n of t) {
    if (n === e) return !0;
    if (Ie(n)) return !1;
  }
  return !1;
}
function j(t, e) {
  return t.some((n) => l(n) && U(n) === e);
}
const Ce = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Enter", "Escape"];
class Re extends ne(te) {
  #a;
  #c;
  #e = null;
  #t = null;
  #r = !1;
  #o = null;
  #i = null;
  #l = !1;
  constructor() {
    super(), this.consumeContext(re, (e) => {
      this.#a = e;
    }), this.consumeContext(oe, (e) => {
      this.#c = e;
    });
  }
  connectedCallback() {
    super.connectedCallback(), z(this), this.#y();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), z(null), this.#s();
  }
  attachItem(e) {
    const n = e;
    if (n.dataset[L]) return;
    n.dataset[L] = "1", e.setAttribute("draggable", "true"), A(e), new MutationObserver(() => A(e)).observe(e, { childList: !0, subtree: !0 });
  }
  #y() {
    this.#l || (this.#l = !0, document.addEventListener("dragstart", (e) => {
      const n = this.#n(e);
      n && this.#g(e, n);
    }, !0), document.addEventListener("dragenter", (e) => {
      !this.#n(e) || !this.#e || e.preventDefault();
    }, !0), document.addEventListener("dragover", (e) => {
      const n = this.#n(e);
      if (!n) {
        a();
        return;
      }
      this.#b(e, n);
    }, !0), document.addEventListener("dragleave", (e) => {
      const n = this.#n(e);
      n && this.#w(e, n);
    }, !0), document.addEventListener("drop", (e) => {
      const n = this.#n(e);
      n && this.#v(e, n);
    }, !0), document.addEventListener("dragend", () => this.#q(), !0), document.addEventListener("keydown", (e) => this.#E(e), !0), document.addEventListener("pointerdown", (e) => {
      this.#t && this.#e && !j(e.composedPath(), this.#e.adapter.entityType) && (this.#u(), p("Move cancelled."));
    }, !0));
  }
  #n(e) {
    for (const n of e.composedPath?.() ?? [])
      if (l(n)) return n;
    return null;
  }
  #f(e) {
    const n = R(e), r = y(e);
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
    if (!r || R(e) !== r.adapter) return null;
    const i = y(e);
    if (!i || G(i, r.sourceUnique, r.descendantUniques)) return null;
    const o = {
      zone: n,
      sourceParentUnique: r.sourceParentUnique,
      targetUnique: i,
      targetParentUnique: v(e)
    };
    if (!r.adapter.sort) return be(o);
    const s = n === "into" ? [] : le(o.targetParentUnique, r.adapter.entityType);
    return Te({ ...o, sourceUnique: r.sourceUnique, siblings: s });
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
    e.dataTransfer.effectAllowed = "move", e.dataTransfer.setData(he, r.sourceUnique), this.#e = r;
  }
  #b(e, n) {
    if (!this.#e) return;
    const r = B(n, e.clientY), i = this.#d(n, r);
    if (i === null) {
      a();
      return;
    }
    if (this.#T(n), i.kind === "noop") {
      a();
      return;
    }
    e.preventDefault(), e.dataTransfer.dropEffect = "move", Y(i, this.#e.adapter, n, r);
  }
  #T(e) {
    this.#i !== e && (this.#s(), this.#i = e, this.#o = setTimeout(() => ce(e), fe));
  }
  #s() {
    this.#o && clearTimeout(this.#o), this.#o = null, this.#i = null;
  }
  #w(e, n) {
    e.relatedTarget && n.contains(e.relatedTarget) || (a(), this.#i === n && this.#s());
  }
  async #v(e, n) {
    const r = B(n, e.clientY), i = this.#d(n, r);
    H(i) && (e.preventDefault(), a(), await this.#h(i, n, r));
  }
  #q() {
    this.#e = null, a(), this.#s();
  }
  // Run a plan for the current #dragState. Returns whether it fully succeeded.
  //   move       → move; reload both branches (chevrons come from the data
  //                store; Dictionary re-sorts alphabetically).
  //   reorder    → optimistic DOM move first (the sort API is slow), then sort;
  //                roll the DOM back if it fails. No reload on success.
  //   move+sort  → move, then sort. If only the sort fails the item stays moved
  //                (a completed move beats a rollback) and a warning says so.
  // Any failure reloads both branches so the tree matches the server again
  // (e.g. a cycle through a collapsed branch the client couldn't see).
  async #h(e, n, r) {
    const i = this.#e;
    if (!i || this.#r) return !1;
    this.#r = !0;
    const { adapter: o, sourceUnique: s, sourceParentUnique: c } = i, u = w(s, o.entityType);
    u && (u.style.opacity = "0.4", K(u));
    let m = !1, T = !0;
    try {
      if (e.kind === "move")
        await o.move(this, s, e.parentUnique), m = !0;
      else if (e.kind === "reorder") {
        const d = u?.parentNode ?? null, h = u?.nextSibling ?? null;
        ve(u, n, r), u && K(u);
        try {
          await o.sort(this, e.parentUnique, e.order);
        } catch (f) {
          throw u && d && d.insertBefore(u, h), f;
        }
        m = !0, T = !1;
      } else {
        await o.move(this, s, e.parentUnique);
        try {
          await o.sort(this, e.parentUnique, e.order), m = !0;
        } catch (d) {
          this.#x(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${d?.message ?? d})`);
        }
      }
    } catch (d) {
      this.#I(d?.message ?? String(d));
    } finally {
      T && (this.#m(o, c), this.#m(o, e.parentUnique)), Ue(), u && (u.style.opacity = ""), this.#r = !1;
    }
    return m;
  }
  // --- Keyboard "grab & place" ------------------------------------------------
  // All rendered rows of the grabbed item's tree, in visual order, with a
  // parallel element array so a reducer index maps back to a DOM row.
  #p() {
    const e = this.#e, n = [], r = [];
    if (!e) return { candidates: n, els: r };
    for (const i of O(e.adapter.entityType)) {
      const o = y(i);
      o && (n.push({
        unique: o,
        parentUnique: v(i),
        blocked: G(o, e.sourceUnique, e.descendantUniques)
      }), r.push(i));
    }
    return { candidates: n, els: r };
  }
  #U(e, n, r) {
    const i = this.#e.adapter, o = e.kind === "move" && e.parentUnique !== null ? w(e.parentUnique, i.entityType) : null;
    return we(e, n, x(r), o ? x(o) : null);
  }
  #u() {
    a(), this.#t = null, this.#e = null;
  }
  #E(e) {
    if (this.#t === null) {
      if (e.key !== " " || this.#r) return;
      const h = this.#n(e);
      if (!h || !ke(e.composedPath(), h)) return;
      const f = this.#f(h);
      if (!f) return;
      e.preventDefault(), e.stopPropagation(), this.#e = f;
      const { candidates: E } = this.#p(), Q = E.findIndex((ee) => ee.unique === f.sourceUnique);
      this.#t = { sourceUnique: f.sourceUnique, targetIndex: Math.max(Q, 0), zone: "before" }, a(), p(`Grabbed ${x(h)}. Use arrow keys to choose where to move it, space to drop, escape to cancel.`);
      return;
    }
    if (!Ce.includes(e.key)) return;
    if (!j(e.composedPath(), this.#e.adapter.entityType)) {
      this.#u(), p("Move cancelled.");
      return;
    }
    e.preventDefault(), e.stopPropagation();
    const { candidates: n, els: r } = this.#p();
    if (n.length === 0) {
      this.#u();
      return;
    }
    const i = Math.min(Math.max(this.#t.targetIndex, 0), n.length - 1), o = Ee({ ...this.#t, targetIndex: i }, e.key, n);
    if (o.type === "cancel") {
      this.#u(), p("Move cancelled.");
      return;
    }
    if (o.type === "blocked") {
      p("Can't move an item into itself or its own children. Choose another position, or press escape to cancel.");
      return;
    }
    const s = r[o.state.targetIndex], c = s ? this.#d(s, o.state.zone) : null;
    if (o.type === "none") {
      if (this.#t = o.state, !s || !c) {
        a();
        return;
      }
      Y(c, this.#e.adapter, s, o.state.zone), p(this.#U(c, o.state.zone, s));
      return;
    }
    if (!H(c)) {
      p("Nothing to move here. Choose another position, or press escape to cancel.");
      return;
    }
    const { sourceUnique: u, adapter: m } = this.#e, T = x(w(u, m.entityType)), d = o.state.zone;
    this.#t = null, a(), (async () => {
      const h = await this.#h(c, s, d);
      p(h ? `Moved ${T}.` : `Could not move ${T}.`);
      const f = w(u, m.entityType), E = f?.querySelector("[tabindex],a,button");
      E?.focus ? E.focus() : f?.focus?.(), this.#e = null;
    })();
  }
  // --- Server sync -------------------------------------------------------------
  #m(e, n) {
    if (!this.#c) {
      console.warn(`${C} reload: action-event context not available — tree won't auto-refresh`);
      return;
    }
    this.#c.dispatchEvent(
      new ie({
        unique: n,
        entityType: n ? e.entityType : e.rootEntityType
      })
    );
  }
  #x(e) {
    console.warn(C, e), this.#a?.peek("warning", { data: { message: e } });
  }
  #I(e) {
    console.error(C, e), this.#a?.peek("danger", { data: { message: e } });
  }
  render() {
    return null;
  }
}
function $e(t) {
  ue(t), pe(), Ae(), J();
}
function Ae() {
  if (customElements.get(I) || customElements.define(I, Re), document.querySelector(I)) return;
  const t = document.createElement(I);
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
async function Le(t, e) {
  const { error: n } = await se(t, e, { disableNotifications: !0 });
  if (n) throw n;
}
export {
  $e as a,
  Le as r
};
//# sourceMappingURL=core-CrszW97q.js.map
