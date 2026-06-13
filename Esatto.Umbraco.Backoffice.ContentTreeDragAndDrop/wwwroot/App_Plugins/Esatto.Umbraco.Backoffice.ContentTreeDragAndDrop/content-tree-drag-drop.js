import { LitElement as H } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin as W } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as Y } from "@umbraco-cms/backoffice/notification";
import { UMB_ACTION_EVENT_CONTEXT as V } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadChildrenOfEntityEvent as X } from "@umbraco-cms/backoffice/entity-action";
import { DocumentService as q } from "@umbraco-cms/backoffice/external/backend-api";
import { tryExecute as C } from "@umbraco-cms/backoffice/resources";
function B(n) {
  const e = (n.querySelector(":scope > umb-document-tree-item, :scope > umb-default-tree-item") ?? n.shadowRoot?.querySelector("umb-document-tree-item, umb-default-tree-item"))?.shadowRoot?.querySelector("uui-menu-item"), o = e?.shadowRoot?.querySelector('#menu-item, [id="menu-item"], button, .menu-item-body');
  if (o) return o.getBoundingClientRect();
  if (e) {
    const i = e.getBoundingClientRect();
    return new DOMRect(i.left, i.top, i.width, Math.min(i.height, 32));
  }
  const r = n.getBoundingClientRect();
  return new DOMRect(r.left, r.top, r.width, Math.min(r.height, 32));
}
function Z(n, t, e) {
  const o = e - n, r = t / 3;
  return o < r ? "before" : o > t - r ? "after" : "into";
}
function D(n, t) {
  const e = B(n);
  return Z(e.top, e.height, t);
}
const b = (() => {
  const n = document.createElement("div");
  return n.id = "backoffice-content-tree-dnd-indicator", n.style.cssText = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: none;
    box-sizing: border-box;
  `, document.body.appendChild(n), n;
})();
function j(n, t) {
  const e = B(n), o = "var(--uui-color-selected, #3879ff)", r = "rgba(56, 121, 255, 0.08)", i = `
    position: fixed;
    pointer-events: none;
    z-index: 99999;
    display: block;
    box-sizing: border-box;
    left: ${e.left}px;
    width: ${e.width}px;
  `;
  t === "before" ? b.style.cssText = `${i}
      top: ${e.top - 1}px;
      height: 2px;
      background: ${o};
    ` : t === "after" ? b.style.cssText = `${i}
      top: ${e.bottom - 1}px;
      height: 2px;
      background: ${o};
    ` : b.style.cssText = `${i}
      top: ${e.top}px;
      height: ${e.height}px;
      border: 2px solid ${o};
      background: ${r};
    `;
}
function l() {
  b.style.display = "none";
}
function m(n) {
  if (!n) return null;
  if (n.parentElement) return n.parentElement;
  const t = n.getRootNode?.();
  return t && t.host ? t.host : null;
}
function f(n, t) {
  if (!n || n.nodeType !== 1) return;
  const e = n;
  t(e);
  for (const o of e.children)
    f(o, t);
  if (e.shadowRoot)
    for (const o of e.shadowRoot.children)
      f(o, t);
}
const S = "backofficeContentTreeDndAttached", J = 700, P = "document", K = "application/x-backoffice-content-tree-dnd", A = "__backofficeContentTreeDndPatched";
function a(n) {
  const t = n;
  return !t || t.nodeType !== 1 || t.tagName !== "UMB-TREE-ITEM" ? !1 : (t.getAttribute("entitytype") ?? t.getAttribute("entity-type")) === P;
}
function d(n) {
  return n.props?.item?.unique ?? n.api?.unique ?? n._item?.unique ?? n.getAttribute?.("data-unique") ?? null;
}
function y(n) {
  const t = n.props?.item?.parent;
  if (t !== void 0)
    return t.unique ?? null;
  let e = m(n);
  for (; e; ) {
    if (a(e)) return d(e);
    e = m(e);
  }
  return null;
}
function w(n) {
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
function I(n) {
  let t = m(n);
  for (; t; ) {
    if (a(t)) return t;
    t = m(t);
  }
  return null;
}
function Q(n) {
  let t = n;
  for (; t; ) {
    if (a(t)) return t;
    t = m(t);
  }
  return null;
}
function tt(n) {
  let t = null;
  return f(document.documentElement, (e) => {
    t || a(e) && d(e) === n && (t = e);
  }), t;
}
const T = /* @__PURE__ */ new WeakSet(), _ = [];
let g = null;
function U(n) {
  if (g = n, n)
    for (const t of _)
      v(t);
}
function v(n) {
  if (!n) return;
  const t = n.nodeType === 9 ? n.documentElement : n;
  f(t, (e) => {
    a(e) && (g?.attachItem(e), w(e)), e.shadowRoot && !T.has(e.shadowRoot) && E(e.shadowRoot);
  });
}
function E(n) {
  if (!n || T.has(n)) return;
  T.add(n), _.push(n), new MutationObserver((e) => {
    for (const o of e) {
      if (o.type === "attributes" && a(o.target)) {
        g?.attachItem(o.target);
        continue;
      }
      for (const r of o.addedNodes) {
        if (!(r instanceof Element)) continue;
        a(r) && g?.attachItem(r), v(r);
        const i = Q(r);
        i && w(i);
      }
    }
  }).observe(n, {
    childList: !0,
    subtree: !0,
    attributes: !0,
    // Lit reflects `entityType` to lowercase `entitytype` (no hyphen) in 17.0.0.
    // Watch both forms in case of version drift.
    attributeFilter: ["entitytype", "entity-type"]
  }), v(n);
}
const M = window;
if (!M[A]) {
  M[A] = !0;
  const n = Element.prototype.attachShadow;
  Element.prototype.attachShadow = function(t) {
    const e = n.call(this, t);
    try {
      E(e);
    } catch (o) {
      console.warn("[backoffice-content-tree-dnd] observeRoot failed:", o);
    }
    return e;
  }, E(document);
}
function et(n) {
  const t = /* @__PURE__ */ new Set();
  return f(n, (e) => {
    if (e !== n && a(e)) {
      const o = d(e);
      o && t.add(o);
    }
  }), t;
}
function O(n, t, e) {
  return n === t || e.has(n);
}
function N(n) {
  const t = n ? d(n) : null, e = [];
  return f(n ?? document, (r) => {
    if (r !== n && a(r) && y(r) === t) {
      const s = d(r);
      s && e.push(s);
    }
  }), e;
}
function L(n, t, e, o) {
  const r = n.filter((c) => c !== t), i = r.indexOf(e);
  if (i === -1) return null;
  const s = o === "before" ? i : i + 1;
  return [...r.slice(0, s), t, ...r.slice(s)];
}
function nt(n, t, e) {
  if (!n || !t || n === t) return;
  const o = t.parentNode;
  o && (e === "before" ? o.insertBefore(n, t) : o.insertBefore(n, t.nextSibling));
}
class ot extends W(H) {
  #i;
  #s;
  #t = null;
  #e = null;
  #o = null;
  #c = !1;
  constructor() {
    super(), this.consumeContext(Y, (t) => {
      this.#i = t;
    }), this.consumeContext(V, (t) => {
      this.#s = t;
    });
  }
  connectedCallback() {
    super.connectedCallback(), U(this), this.#f();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), U(null), this.#e && clearTimeout(this.#e);
  }
  attachItem(t) {
    const e = t;
    if (e.dataset[S]) return;
    e.dataset[S] = "1", t.setAttribute("draggable", "true"), w(t), new MutationObserver(() => w(t)).observe(t, { childList: !0, subtree: !0 });
  }
  #f() {
    this.#c || (this.#c = !0, document.addEventListener("dragstart", (t) => {
      const e = this.#r(t);
      e && this.#h(t, e);
    }, !0), document.addEventListener("dragenter", (t) => {
      !this.#r(t) || !this.#t || t.preventDefault();
    }, !0), document.addEventListener("dragover", (t) => {
      const e = this.#r(t);
      if (!e) {
        l();
        return;
      }
      this.#l(t, e);
    }, !0), document.addEventListener("dragleave", (t) => {
      const e = this.#r(t);
      e && this.#m(t, e);
    }, !0), document.addEventListener("drop", (t) => {
      const e = this.#r(t);
      e && this.#p(t, e);
    }, !0), document.addEventListener("dragend", () => this.#b(), !0));
  }
  #r(t) {
    const e = t.composedPath?.() ?? [];
    for (const o of e)
      if (a(o)) return o;
    return null;
  }
  #h(t, e) {
    const o = d(e);
    if (!o) {
      t.preventDefault();
      return;
    }
    const r = y(e), i = et(e);
    t.dataTransfer.effectAllowed = "move", t.dataTransfer.setData(K, o), this.#t = { sourceUnique: o, sourceParentUnique: r, descendantUniques: i };
  }
  #l(t, e) {
    if (!this.#t) return;
    const o = d(e);
    if (!o) return;
    if (O(o, this.#t.sourceUnique, this.#t.descendantUniques)) {
      l();
      return;
    }
    const r = D(e, t.clientY);
    t.preventDefault(), t.dataTransfer.dropEffect = "move", j(e, r), this.#o !== e && (this.#e && clearTimeout(this.#e), this.#o = e, this.#e = setTimeout(() => {
      !e.hasAttribute("show-children") && !e.hasAttribute("is-expanded") && !e.hasAttribute("open") && (typeof e.toggleChildren == "function" ? e.toggleChildren() : e.shadowRoot?.querySelector('[name="caret"], [data-mark="chevron"]')?.click?.());
    }, J));
  }
  #m(t, e) {
    t.relatedTarget && e.contains(t.relatedTarget) || (l(), this.#o === e && this.#e && (clearTimeout(this.#e), this.#e = null, this.#o = null));
  }
  async #p(t, e) {
    if (!this.#t) return;
    const o = d(e);
    if (!o || O(o, this.#t.sourceUnique, this.#t.descendantUniques)) return;
    const r = D(e, t.clientY);
    t.preventDefault(), l();
    const i = this.#t.sourceUnique, s = this.#t.sourceParentUnique, c = y(e), u = tt(i);
    u && (u.style.opacity = "0.4");
    try {
      if (r === "into") {
        if (s === o) return;
        await this.#a(i, o), await this.#n(s), await this.#n(o);
        return;
      }
      if (s === c) {
        const p = I(e), z = N(p), x = L(z, i, o, r);
        if (!x)
          throw new Error("Target sibling not found in parent's rendered children");
        const F = u?.nextSibling ?? null, R = u?.parentNode ?? null;
        nt(u, e, r);
        try {
          await this.#u(c, x);
        } catch (G) {
          throw u && R && R.insertBefore(u, F), G;
        }
        return;
      }
      await this.#a(i, c);
      const h = I(e), $ = N(h), k = L($, i, o, r);
      if (!k) {
        await this.#n(s), await this.#n(c), this.#d("Moved, but couldn't set the position — it's at the bottom of the new parent.");
        return;
      }
      try {
        await this.#u(c, k), await this.#n(s), await this.#n(c);
      } catch (p) {
        await this.#n(s), await this.#n(c), this.#d(`Moved, but couldn't set the position — it's at the bottom of the new parent. (${p?.message ?? p})`);
      }
    } catch (h) {
      console.error("[backoffice-content-tree-dnd] drop failed", h), this.#w(h?.message ?? String(h)), await this.#n(s).catch(() => {
      }), await this.#n(c).catch(() => {
      });
    } finally {
      u && (u.style.opacity = "");
    }
  }
  #b() {
    this.#t = null, l(), this.#e && clearTimeout(this.#e), this.#e = null, this.#o = null;
  }
  async #a(t, e) {
    const { error: o } = await C(
      this,
      q.putDocumentByIdMove({
        path: { id: t },
        body: { target: e ? { id: e } : null }
      }),
      { disableNotifications: !0 }
    );
    if (o) throw o;
  }
  async #u(t, e) {
    const { error: o } = await C(
      this,
      q.putDocumentSort({
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
    if (!this.#s) {
      console.warn("[backoffice-content-tree-dnd] reload: action-event context not available — tree won't auto-refresh");
      return;
    }
    const e = t ? P : "document-root";
    this.#s.dispatchEvent(
      new X({
        unique: t ?? null,
        entityType: e
      })
    );
  }
  #w(t) {
    console.error("[backoffice-content-tree-dnd]", t), this.#i?.peek("danger", { data: { message: t } });
  }
  #d(t) {
    console.warn("[backoffice-content-tree-dnd]", t), this.#i?.peek("warning", { data: { message: t } });
  }
  render() {
    return null;
  }
}
customElements.get("backoffice-content-tree-dnd") || customElements.define("backoffice-content-tree-dnd", ot);
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
