import { LitElement as J } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as Q } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as tt } from "@umbraco-cms/backoffice/notification";
import { UMB_ACTION_EVENT_CONTEXT as et } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadChildrenOfEntityEvent as nt } from "@umbraco-cms/backoffice/entity-action";
import { DocumentService as L } from "@umbraco-cms/backoffice/external/backend-api";
import { tryExecute as N } from "@umbraco-cms/backoffice/resources";
function $(n) {
  const e = (n.querySelector(":scope > umb-document-tree-item, :scope > umb-default-tree-item") ?? n.shadowRoot?.querySelector("umb-document-tree-item, umb-default-tree-item"))?.shadowRoot?.querySelector("uui-menu-item"), o = e?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (o) return o.getBoundingClientRect();
  if (e) {
    const i = e.getBoundingClientRect();
    return new DOMRect(i.left, i.top, i.width, Math.min(i.height, 32));
  }
  const r = n.getBoundingClientRect();
  return new DOMRect(r.left, r.top, r.width, Math.min(r.height, 32));
}
function ot(n, t, e) {
  const o = e - n, r = t / 3;
  return o < r ? "before" : o > t - r ? "after" : "into";
}
function O(n, t) {
  const e = $(n);
  return ot(e.top, e.height, t);
}
const q = (() => {
  const n = document.createElement("div");
  return n.id = "backoffice-content-tree-dnd-indicator", n.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `, document.body.appendChild(n), n;
})();
function D(n, t) {
  const e = $(n), o = "var(--uui-color-selected, #3879ff)", r = "rgba(56, 121, 255, 0.08)", i = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${e.left}px;
    width: ${e.width}px;
  `;
  t === "before" ? q.style.cssText = `${i}
      top: ${e.top - 1}px;
      height: 2px;
      background: ${o};
    ` : t === "after" ? q.style.cssText = `${i}
      top: ${e.bottom - 1}px;
      height: 2px;
      background: ${o};
    ` : q.style.cssText = `${i}
      top: ${e.top}px;
      height: ${e.height}px;
      border: 2px solid ${o};
      background: ${r};
    `;
}
function f() {
  q.style.display = "none";
}
function v(n) {
  if (!n) return null;
  if (n.parentElement) return n.parentElement;
  const t = n.getRootNode?.();
  return t && t.host ? t.host : null;
}
function b(n, t) {
  if (!n || n.nodeType !== 1) return;
  const e = n;
  t(e);
  for (const o of e.children)
    b(o, t);
  if (e.shadowRoot)
    for (const o of e.shadowRoot.children)
      b(o, t);
}
const P = "backofficeContentTreeDndAttached", rt = 700, X = "document", it = "application/x-backoffice-content-tree-dnd", B = "__backofficeContentTreeDndPatched";
function d(n) {
  const t = n;
  return !t || t.nodeType !== 1 || t.tagName !== "UMB-TREE-ITEM" ? !1 : (t.getAttribute("entitytype") ?? t.getAttribute("entity-type")) === X;
}
function a(n) {
  return n.props?.item?.unique ?? n.api?.unique ?? n._item?.unique ?? n.getAttribute?.("data-unique") ?? null;
}
function x(n) {
  const t = n.props?.item?.parent;
  if (t !== void 0)
    return t.unique ?? null;
  let e = v(n);
  for (; e; ) {
    if (d(e)) return a(e);
    e = v(e);
  }
  return null;
}
function k(n) {
  function t(e) {
    if (!(!e || e.nodeType !== 1)) {
      if (e.tagName !== "SLOT")
        try {
          e.setAttribute("draggable", "true");
        } catch {
        }
      for (const o of e.children) t(o);
      if (e.shadowRoot)
        for (const o of e.shadowRoot.children) t(o);
    }
  }
  for (const e of n.children) t(e);
  if (n.shadowRoot)
    for (const e of n.shadowRoot.children) t(e);
}
function _(n) {
  let t = v(n);
  for (; t; ) {
    if (d(t)) return t;
    t = v(t);
  }
  return null;
}
function st(n) {
  let t = n;
  for (; t; ) {
    if (d(t)) return t;
    t = v(t);
  }
  return null;
}
function R(n) {
  let t = null;
  return b(document.documentElement, (e) => {
    t || d(e) && a(e) === n && (t = e);
  }), t;
}
function ct() {
  const n = [];
  return b(document.documentElement, (t) => {
    d(t) && n.push(t);
  }), n;
}
const C = /* @__PURE__ */ new WeakSet(), Z = [];
let I = null;
function z(n) {
  if (I = n, n)
    for (const t of Z)
      S(t);
}
function S(n) {
  if (!n) return;
  const t = n.nodeType === 9 ? n.documentElement : n;
  b(t, (e) => {
    d(e) && (I?.attachItem(e), k(e)), e.shadowRoot && !C.has(e.shadowRoot) && M(e.shadowRoot);
  });
}
function M(n) {
  if (!n || C.has(n)) return;
  C.add(n), Z.push(n), new MutationObserver((e) => {
    for (const o of e) {
      if (o.type === "attributes" && d(o.target)) {
        I?.attachItem(o.target);
        continue;
      }
      for (const r of o.addedNodes) {
        if (!(r instanceof Element)) continue;
        d(r) && I?.attachItem(r), S(r);
        const i = st(r);
        i && k(i);
      }
    }
  }).observe(n, {
    childList: !0,
    subtree: !0,
    attributes: !0,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ["entitytype", "entity-type"]
  }), S(n);
}
const F = window;
if (!F[B]) {
  F[B] = !0;
  const n = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function(t) {
    const e = n.call(this, t);
    try {
      M(e);
    } catch (o) {
      console.warn("[backoffice-content-tree-dnd] observeRoot failed:", o);
    }
    return e;
  }, M(document);
}
function G(n) {
  const t = /* @__PURE__ */ new Set();
  return b(n, (e) => {
    if (e !== n && d(e)) {
      const o = a(e);
      o && t.add(o);
    }
  }), t;
}
function A(n, t, e) {
  return n === t || e.has(n);
}
function H(n) {
  const t = n ? a(n) : null, e = [];
  return b(n ?? document, (r) => {
    if (r !== n && d(r) && x(r) === t) {
      const c = a(r);
      c && e.push(c);
    }
  }), e;
}
function K(n, t, e, o) {
  const r = n.filter((s) => s !== t), i = r.indexOf(e);
  if (i === -1) return null;
  const c = o === "before" ? i : i + 1;
  return [...r.slice(0, c), t, ...r.slice(c)];
}
function ut(n, t, e) {
  if (!n || !t || n === t) return;
  const o = t.parentNode;
  o && (e === "before" ? o.insertBefore(n, t) : o.insertBefore(n, t.nextSibling));
}
const y = (() => {
  const n = document.createElement("div");
  return n.id = "backoffice-content-tree-dnd-spinner", n.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 100000;
    display: none;
    align-items: center;
    justify-content: center;
    box-sizing: border-box;
    color: var(--uui-color-selected, #3879ff);
  `, n.innerHTML = '<uui-loader-circle style="font-size: 1.2em;"></uui-loader-circle>', document.body.appendChild(n), n;
})();
function W(n) {
  const t = $(n);
  y.style.left = `${t.left}px`, y.style.top = `${t.top}px`, y.style.width = `${Math.min(t.height, 28)}px`, y.style.height = `${t.height}px`, y.style.display = "flex";
}
function at() {
  y.style.display = "none";
}
function Y(n, t, e) {
  let o = n + t;
  for (; o >= 0 && o < e.length; ) {
    if (!e[o].blocked) return o;
    o += t;
  }
  return n;
}
function dt(n, t, e) {
  switch (t) {
    case "ArrowDown":
      return { type: "none", state: { ...n, targetIndex: Y(n.targetIndex, 1, e), zone: "before" } };
    case "ArrowUp":
      return { type: "none", state: { ...n, targetIndex: Y(n.targetIndex, -1, e), zone: "before" } };
    case "ArrowRight":
      return { type: "none", state: { ...n, zone: "into" } };
    case "ArrowLeft": {
      const o = e[n.targetIndex]?.parentUnique ?? null;
      if (!o) return { type: "none", state: n };
      const r = e.findIndex((i) => i.unique === o);
      return r < 0 ? { type: "none", state: n } : { type: "none", state: { ...n, targetIndex: r, zone: "after" } };
    }
    case " ":
    case "Enter": {
      const o = e[n.targetIndex];
      return !o || o.blocked ? { type: "none", state: n } : { type: "commit", state: n };
    }
    case "Escape":
      return { type: "cancel" };
    default:
      return { type: "none", state: n };
  }
}
const V = (() => {
  const n = document.createElement("div");
  return n.id = "backoffice-content-tree-dnd-live", n.setAttribute("aria-live", "polite"), n.setAttribute("aria-atomic", "true"), n.style.cssText = `
    position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0;
    overflow: hidden; clip: rect(0 0 0 0); clip-path: inset(50%); border: 0;
  `, document.body.appendChild(n), n;
})();
function E(n) {
  V.textContent = "", V.textContent = n;
}
class ft extends Q(J) {
  #c;
  #u;
  #t = null;
  #o = null;
  #i = !1;
  #e = null;
  #s = null;
  #d = !1;
  constructor() {
    super(), this.consumeContext(tt, (t) => {
      this.#c = t;
    }), this.consumeContext(et, (t) => {
      this.#u = t;
    });
  }
  connectedCallback() {
    super.connectedCallback(), z(this), this.#b();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), z(null), this.#e && clearTimeout(this.#e);
  }
  attachItem(t) {
    const e = t;
    if (e.dataset[P]) return;
    e.dataset[P] = "1", t.setAttribute("draggable", "true"), k(t), new MutationObserver(() => k(t)).observe(t, { childList: !0, subtree: !0 });
  }
  #b() {
    this.#d || (this.#d = !0, document.addEventListener("dragstart", (t) => {
      const e = this.#r(t);
      e && this.#g(t, e);
    }, !0), document.addEventListener("dragenter", (t) => {
      !this.#r(t) || !this.#t || t.preventDefault();
    }, !0), document.addEventListener("dragover", (t) => {
      const e = this.#r(t);
      if (!e) {
        f();
        return;
      }
      this.#y(t, e);
    }, !0), document.addEventListener("dragleave", (t) => {
      const e = this.#r(t);
      e && this.#w(t, e);
    }, !0), document.addEventListener("drop", (t) => {
      const e = this.#r(t);
      e && this.#x(t, e);
    }, !0), document.addEventListener("dragend", () => this.#v(), !0), document.addEventListener("keydown", (t) => this.#T(t), !0));
  }
  #r(t) {
    const e = t.composedPath?.() ?? [];
    for (const o of e)
      if (d(o)) return o;
    return null;
  }
  #g(t, e) {
    if (this.#i) {
      t.preventDefault();
      return;
    }
    const o = a(e);
    if (!o) {
      t.preventDefault();
      return;
    }
    const r = x(e), i = G(e);
    t.dataTransfer.effectAllowed = "move", t.dataTransfer.setData(it, o), this.#t = { sourceUnique: o, sourceParentUnique: r, descendantUniques: i };
  }
  #y(t, e) {
    if (!this.#t) return;
    const o = a(e);
    if (!o) return;
    if (A(o, this.#t.sourceUnique, this.#t.descendantUniques)) {
      f();
      return;
    }
    const r = O(e, t.clientY);
    t.preventDefault(), t.dataTransfer.dropEffect = "move", D(e, r), this.#s !== e && (this.#e && clearTimeout(this.#e), this.#s = e, this.#e = setTimeout(() => {
      !e.hasAttribute("show-children") && !e.hasAttribute("is-expanded") && !e.hasAttribute("open") && (typeof e.toggleChildren == "function" ? e.toggleChildren() : e.shadowRoot?.querySelector('[name="caret"], [data-mark="chevron"]')?.click?.());
    }, rt));
  }
  #w(t, e) {
    t.relatedTarget && e.contains(t.relatedTarget) || (f(), this.#s === e && this.#e && (clearTimeout(this.#e), this.#e = null, this.#s = null));
  }
  async #x(t, e) {
    if (!this.#t) return;
    const o = a(e);
    if (!o || A(o, this.#t.sourceUnique, this.#t.descendantUniques)) return;
    const r = O(e, t.clientY);
    t.preventDefault(), f(), await this.#f(e, r);
  }
  // Shared move/sort/reload/optimistic/rollback/spinner logic, driven by a
  // resolved (targetEl, zone). Used by pointer-drop (#onDrop) and reusable by a
  // future keyboard-commit path. Reads source info from #dragState.
  async #f(t, e) {
    if (!this.#t) return;
    const o = a(t);
    if (!o || A(o, this.#t.sourceUnique, this.#t.descendantUniques) || this.#i) return;
    this.#i = !0;
    const r = this.#t.sourceUnique, i = this.#t.sourceParentUnique, c = x(t), s = R(r);
    s && (s.style.opacity = "0.4", W(s));
    try {
      if (e === "into") {
        if (i === o) return;
        await this.#h(r, o), await this.#n(i), await this.#n(o);
        return;
      }
      if (i === c) {
        const h = _(t), g = H(h), m = K(g, r, o, e);
        if (!m)
          throw new Error("Target sibling not found in parent's rendered children");
        const T = s?.nextSibling ?? null, w = s?.parentNode ?? null;
        ut(s, t, e), s && W(s);
        try {
          await this.#p(c, m);
        } catch (U) {
          throw s && w && w.insertBefore(s, T), U;
        }
        return;
      }
      await this.#h(r, c);
      const u = _(t), l = H(u), p = K(l, r, o, e);
      if (!p) {
        await this.#n(i), await this.#n(c), this.#m("Moved, but couldn't set the position — it's at the bottom of the new parent.");
        return;
      }
      try {
        await this.#p(c, p), await this.#n(i), await this.#n(c);
      } catch (h) {
        await this.#n(i), await this.#n(c), this.#m(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${h?.message ?? h})`);
      }
    } catch (u) {
      console.error("[backoffice-content-tree-dnd] drop failed", u), this.#E(u?.message ?? String(u)), await this.#n(i).catch(() => {
      }), await this.#n(c).catch(() => {
      });
    } finally {
      at(), s && (s.style.opacity = ""), this.#i = !1;
    }
  }
  #v() {
    this.#t = null, f(), this.#e && clearTimeout(this.#e), this.#e = null, this.#s = null;
  }
  // --- Keyboard "grab & place" reordering -----------------------------------
  // Enumerate all visible document tree-items in visual order as KbCandidates,
  // keeping a parallel element array so a resolved targetIndex maps back to a
  // DOM element. Elements without a readable unique are skipped. Requires a
  // grab in progress (uses #dragState for the source/descendant blocked check).
  #l() {
    const t = [], e = [], o = this.#t?.sourceUnique ?? null, r = this.#t?.descendantUniques ?? /* @__PURE__ */ new Set();
    for (const i of ct()) {
      const c = a(i);
      c && (t.push({
        unique: c,
        parentUnique: x(i),
        blocked: c === o || r.has(c)
      }), e.push(i));
    }
    return { candidates: t, els: e };
  }
  #a(t) {
    return t?.props?.item?.name ?? "item";
  }
  #T(t) {
    if (this.#o === null) {
      if (t.key !== " ") return;
      const u = this.#r(t);
      if (!u) return;
      const l = a(u);
      if (!l || this.#i) return;
      t.preventDefault(), t.stopPropagation();
      const p = x(u), h = G(u);
      this.#t = { sourceUnique: l, sourceParentUnique: p, descendantUniques: h };
      const { candidates: g, els: m } = this.#l(), T = g.findIndex((j) => j.unique === l), w = T >= 0 ? T : 0;
      this.#o = { sourceUnique: l, targetIndex: w, zone: "before" };
      const U = m[w] ?? u;
      D(U, "before"), E(
        `Grabbed ${this.#a(u)}. Use arrow keys to choose a position, space to drop, escape to cancel.`
      );
      return;
    }
    const { candidates: e, els: o } = this.#l();
    if (e.length === 0) {
      t.key === "Escape" && (t.preventDefault(), t.stopPropagation()), f(), this.#o = null, this.#t = null;
      return;
    }
    const r = Math.min(Math.max(this.#o.targetIndex, 0), e.length - 1), i = { ...this.#o, targetIndex: r };
    if (!["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Enter", "Escape"].includes(t.key)) return;
    t.preventDefault(), t.stopPropagation();
    const s = dt(i, t.key, e);
    if (s.type === "none") {
      this.#o = s.state;
      const u = o[s.state.targetIndex];
      if (!u) {
        f();
        return;
      }
      D(u, s.state.zone), E(`${s.state.zone} ${this.#a(u)}`);
      return;
    }
    if (s.type === "commit") {
      const u = o[s.state.targetIndex], l = s.state.zone, p = s.state.sourceUnique, h = this.#a(R(p) ?? void 0);
      if (f(), !u) {
        this.#o = null, this.#t = null;
        return;
      }
      this.#o = null, (async () => {
        await this.#f(u, l), E(`Moved ${h}.`);
        const g = R(p), m = g?.querySelector("[tabindex],a,button");
        m?.focus ? m.focus() : g?.focus?.(), this.#t = null;
      })();
      return;
    }
    f(), E("Move cancelled."), this.#o = null, this.#t = null;
  }
  async #h(t, e) {
    const { error: o } = await N(
      this,
      L.putDocumentByIdMove({
        path: { id: t },
        body: { target: e ? { id: e } : null }
      }),
      { disableNotifications: !0 }
    );
    if (o) throw o;
  }
  async #p(t, e) {
    const { error: o } = await N(
      this,
      L.putDocumentSort({
        body: {
          parent: t ? { id: t } : null,
          sorting: e.map((r, i) => ({ id: r, sortOrder: i }))
        }
      }),
      { disableNotifications: !0 }
    );
    if (o) throw o;
  }
  async #n(t) {
    if (!this.#u) {
      console.warn("[backoffice-content-tree-dnd] reload: action-event context not available — tree won't auto-refresh");
      return;
    }
    const e = t ? X : "document-root";
    this.#u.dispatchEvent(
      new nt({
        unique: t ?? null,
        entityType: e
      })
    );
  }
  #E(t) {
    console.error("[backoffice-content-tree-dnd]", t), this.#c?.peek("danger", { data: { message: t } });
  }
  #m(t) {
    console.warn("[backoffice-content-tree-dnd]", t), this.#c?.peek("warning", { data: { message: t } });
  }
  render() {
    return null;
  }
}
customElements.get("backoffice-content-tree-dnd") || customElements.define("backoffice-content-tree-dnd", ft);
(() => {
  if (document.querySelector("backoffice-content-tree-dnd")) return;
  const n = document.createElement("backoffice-content-tree-dnd");
  n.style.display = "none";
  function t(r) {
    n.parentElement !== r && r.appendChild(n);
  }
  const e = document.querySelector("umb-app");
  if (e) {
    t(e);
    return;
  }
  document.body.appendChild(n);
  const o = new MutationObserver(() => {
    const r = document.querySelector("umb-app");
    r && (t(r), o.disconnect());
  });
  o.observe(document.body, { childList: !0 });
})();
//# sourceMappingURL=content-tree-drag-drop.js.map
