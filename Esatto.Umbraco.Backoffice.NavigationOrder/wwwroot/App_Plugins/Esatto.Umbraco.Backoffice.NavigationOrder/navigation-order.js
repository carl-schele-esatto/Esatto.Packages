import { umbHttpClient as k } from "@umbraco-cms/backoffice/http-client";
import { tryExecute as _ } from "@umbraco-cms/backoffice/resources";
import { UmbControllerBase as M } from "@umbraco-cms/backoffice/class-api";
import { UMB_CURRENT_USER_CONTEXT as P } from "@umbraco-cms/backoffice/current-user";
import { UmbLocalizationController as U } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as B } from "@umbraco-cms/backoffice/notification";
import { pathFolderName as g } from "@umbraco-cms/backoffice/utils";
import { umbExtensionsRegistry as f } from "@umbraco-cms/backoffice/extension-registry";
const S = "/umbraco/management/api/v1/esatto/navigation-order", L = () => [{ scheme: "bearer", type: "http" }], K = (i) => ({ sections: i.sections, tabs: i.tabs });
async function C(i, t) {
  const { data: e, error: n } = await _(t, i, { disableNotifications: !0 });
  if (n || !e) throw n ?? new Error("The navigation order response was empty.");
  return K(e);
}
const $ = (i) => C(k.get({ url: S, security: L() }), i), q = (i, t) => C(k.put({ url: S, body: t, security: L() }), i), D = "esatto.navigationOrder.v1", E = (i) => Array.isArray(i) && i.every((t) => typeof t == "string");
function F(i) {
  if (typeof i != "object" || i === null) return !1;
  const { sections: t, tabs: e } = i;
  return (t === null || E(t)) && Array.isArray(e) && e.every((n) => typeof n == "object" && n !== null && typeof n.section == "string" && E(n.aliases));
}
function j(i) {
  try {
    const t = (i ?? globalThis.localStorage).getItem(D);
    if (!t) return null;
    const e = JSON.parse(t);
    return F(e) ? { sections: e.sections, tabs: e.tabs } : null;
  } catch {
    return null;
  }
}
function R(i, t) {
  try {
    (t ?? globalThis.localStorage).setItem(D, JSON.stringify({ sections: i.sections, tabs: i.tabs }));
  } catch {
  }
}
const z = "Umb.Condition.SectionAlias";
function W(i) {
  const t = /* @__PURE__ */ new Set();
  for (const e of i.conditions ?? [])
    if (e.alias === z) {
      e.match && t.add(e.match);
      for (const n of e.oneOf ?? []) t.add(n);
    }
  return t.size > 0 ? [...t] : "all";
}
function p(i, t) {
  return t.kind === "sections" ? i.filter((e) => e.type === "section") : i.filter((e) => {
    if (e.type !== t.type) return !1;
    const n = W(e);
    return n === "all" || n.includes(t.section);
  });
}
const Y = ["dashboard", "sectionView"];
function V(i, t) {
  return i.map((e, n) => ({ alias: e.alias, weight: t(e.alias), index: n })).sort((e, n) => n.weight - e.weight || e.index - n.index).map((e) => e.alias);
}
function b(i, t, e) {
  const n = new Set(i.map((r) => r.alias)), s = [...new Set(t ?? [])].filter((r) => n.has(r)), o = new Set(s);
  return [...s, ...V(i, e).filter((r) => !o.has(r))];
}
function y(i, t) {
  return t.kind === "sections" ? i.sections : i.tabs.find((e) => e.section === t.section)?.aliases ?? null;
}
function G(i, t, e) {
  const n = /* @__PURE__ */ new Map(), s = (r, a) => r.forEach((u, h) => {
    (a || !n.has(u)) && n.set(u, (r.length - h) * 10);
  }), o = b(p(i, { kind: "sections" }), t.sections, e);
  t.sections && s(o, !0);
  for (const r of o) {
    const a = y(t, { kind: "tabs", section: r });
    if (a)
      for (const u of Y)
        s(b(p(i, { kind: "tabs", section: r, type: u }), a, e), !1);
  }
  for (const r of i)
    n.has(r.alias) || n.set(r.alias, e(r.alias));
  return n;
}
function X(i, t, e, n) {
  if (t === e || !i.includes(t) || !i.includes(e)) return [...i];
  const s = i.filter((r) => r !== t), o = s.indexOf(e) + (n === "after" ? 1 : 0);
  return [...s.slice(0, o), t, ...s.slice(o)];
}
function x(i, t) {
  const e = [...t];
  return i && i.forEach((n, s) => {
    if (e.includes(n)) return;
    const o = s === 0 ? -1 : e.indexOf(i[s - 1]);
    e.splice(o + 1, 0, n);
  }), e;
}
function H(i, t, e, n = []) {
  if (t.kind === "sections") return { ...i, sections: x(i.sections, e) };
  const s = x(y(i, t), t.type === "dashboard" ? [...e, ...n] : [...n, ...e]), o = i.tabs.findIndex((a) => a.section === t.section), r = i.tabs.map((a) => ({ ...a, aliases: [...a.aliases] }));
  return o >= 0 ? r[o] = { section: t.section, aliases: s } : r.push({ section: t.section, aliases: s }), { ...i, tabs: r };
}
const J = ["section", "dashboard", "sectionView"], Q = { sections: null, tabs: [] };
class Z {
  // The default calls queueMicrotask unbound: called as this.#schedule(...) browsers throw "Illegal invocation", which left
  // #scheduled stuck and every section registered after the first apply (Umbraco Forms, for one) at its own weight.
  constructor(t, e = (n) => queueMicrotask(n)) {
    this.#r = /* @__PURE__ */ new Map(), this.#t = Q, this.#s = !1, this.#e = !1, this.originalWeight = (n) => this.#r.get(n) ?? this.#i.current().find((s) => s.alias === n)?.weight ?? 0, this.#i = t, this.#n = e;
  }
  #i;
  #n;
  #r;
  #t;
  #s;
  #e;
  #o;
  /** Starts following the registry, so manifests registered later get their weight too. Idempotent. */
  start() {
    this.#o || (this.#o = this.#i.subscribe(() => {
      this.#s || this.#e || (this.#e = !0, this.#n(() => {
        this.#e = !1, this.#a();
      }));
    }), this.#a());
  }
  get order() {
    return this.#t;
  }
  /** Makes `order` the current order and applies it now. */
  setOrder(t) {
    this.#t = t, this.#a();
  }
  /** Every member of a bar in the order it shows, including members the current user can't see. */
  effectiveList(t) {
    return b(p(this.manifests(), t), y(this.#t, t), this.originalWeight);
  }
  manifests() {
    return this.#i.current();
  }
  #a() {
    if (this.#s) return;
    const t = this.#i.current();
    for (const s of t)
      this.#r.has(s.alias) || this.#r.set(s.alias, s.weight ?? 0);
    const e = G(t, this.#t, this.originalWeight), n = t.filter((s) => (s.weight ?? 0) !== e.get(s.alias));
    if (n.length !== 0) {
      this.#s = !0;
      try {
        for (const s of n) this.#i.replace(s, e.get(s.alias));
      } finally {
        this.#s = !1;
      }
    }
  }
}
function tt(i, t) {
  return i.weight === void 0 && t?.weight !== void 0 ? { ...i, weight: t.weight } : i;
}
const et = (i) => J.includes(i.type);
function it() {
  let i = [];
  const t = /* @__PURE__ */ new Set();
  return f.extensions.subscribe((e) => {
    i = e.filter(et).map((n) => n.kind ? tt(n, f.getByAlias(n.alias)) : n), t.forEach((n) => n());
  }), {
    current: () => i,
    subscribe(e) {
      return t.add(e), () => t.delete(e);
    },
    replace(e, n) {
      f.unregister(e.alias), f.register({ ...e, weight: n });
    }
  };
}
const d = new Z(it());
function nt(i) {
  i.draggable = !0;
  const t = i.shadowRoot?.querySelector("#button");
  t && (t.draggable = !1);
}
function st(i) {
  return i.shadowRoot?.querySelector("#button") ?? i;
}
const A = "var(--uui-color-focus, #3879ff)";
class rt {
  #i;
  #n;
  #r;
  /** A vertical line at x, as tall as the tab it sits beside. */
  showLine(t, e, n) {
    const s = this.#i ??= this.#t("div", `position:fixed;z-index:10000;width:3px;border-radius:2px;pointer-events:none;background:${A};`);
    Object.assign(s.style, { left: `${Math.round(t - 1.5)}px`, top: `${Math.round(e + 6)}px`, height: `${Math.max(0, Math.round(n - 12))}px`, display: "block" });
  }
  hideLine() {
    this.#i && (this.#i.style.display = "none");
  }
  /** A small spinner at the tab's top-right corner, labelled for screen readers. */
  showSpinner(t, e) {
    const n = this.#n ??= this.#t("uui-loader-circle", `position:fixed;z-index:10000;font-size:12px;pointer-events:none;color:${A};`);
    n.setAttribute("aria-label", e), Object.assign(n.style, { left: `${Math.round(t.right - 14)}px`, top: `${Math.round(t.top + 4)}px`, display: "block" });
  }
  hideSpinner() {
    this.#n && (this.#n.style.display = "none");
  }
  /** Read out by screen readers (polite). */
  announce(t) {
    const e = this.#r ??= this.#t(
      "div",
      "position:fixed;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;border:0;"
    );
    e.setAttribute("role", "status"), e.setAttribute("aria-live", "polite"), e.textContent = "", requestAnimationFrame(() => e.textContent = t);
  }
  #t(t, e) {
    const n = document.createElement(t);
    return n.style.cssText = e, document.body.appendChild(n), n;
  }
}
class ot {
  #i;
  #n;
  #r;
  #t = null;
  #s = null;
  #e = null;
  constructor(t, e, n) {
    this.#i = t, this.#n = e, this.#r = n;
  }
  /** True while a save is running. */
  get busy() {
    return this.#e !== null;
  }
  /**
   * Applies `next` and queues it for saving. `current` is the order before this move; it becomes
   * the fallback when nothing is saving yet. Resolves when the queue is idle again.
   */
  submit(t, e) {
    return this.#e || (this.#t = e), this.#n(t), this.#s = t, this.#e ??= this.#o().finally(() => {
      this.#e = null;
    }), this.#e;
  }
  async #o() {
    for (; this.#s; ) {
      const t = this.#s;
      this.#s = null;
      try {
        this.#t = await this.#i(t);
      } catch (e) {
        this.#s = null, this.#t && this.#n(this.#t), this.#r(e);
        return;
      }
    }
  }
}
const T = "section-link:", at = /\/section\/([^/]+)\/(dashboard|view)\/([^/?#]+)\/?$/, ct = { dashboards: "dashboard", views: "sectionView" };
function m(i, t, e) {
  const n = i.findIndex((c) => c.localName === "uui-tab");
  if (n < 0) return null;
  const s = i[n], o = i.slice(n + 1).find((c) => c.localName === "uui-tab-group");
  if (!o) return null;
  if (o.id === "tabs") {
    const c = s.getAttribute?.("data-mark") ?? "", w = c.startsWith(T) ? c.slice(T.length) : "";
    return t.some((O) => O.type === "section" && O.alias === w) ? { tab: s, group: o, bar: { kind: "sections" }, alias: w } : null;
  }
  const r = o.id ? ct[o.id] : void 0, a = at.exec(s.getAttribute?.("href") ?? "");
  if (!r || !a || a[2] === "dashboard" != (r === "dashboard")) return null;
  const u = decodeURIComponent(a[1]), h = t.find((c) => c.type === "section" && c.meta?.pathname === u);
  if (!h) return null;
  const l = { kind: "tabs", section: h.alias, type: r }, I = decodeURIComponent(a[3]), v = p(t, l).find((c) => (c.meta?.pathname || e(c.meta?.label ?? c.name ?? c.alias)) === I);
  return v ? { tab: s, group: o, bar: l, alias: v.alias } : null;
}
const ut = (i, t) => i.kind === "sections" ? t.kind === "sections" : t.kind === "tabs" && i.section === t.section && i.type === t.type;
function dt(i, t) {
  return i ? t && t.alias !== i.alias && ut(t.bar, i.bar) ? "move" : "none" : "ignore";
}
function ht(i, t) {
  return i < t.left + t.width / 2 ? "before" : "after";
}
function lt(i) {
  return !i.altKey || !i.shiftKey || i.ctrlKey || i.metaKey ? 0 : i.key === "ArrowLeft" ? -1 : i.key === "ArrowRight" ? 1 : 0;
}
const ft = "application/x-esatto-navigation-order", N = () => new Promise((i) => requestAnimationFrame(() => requestAnimationFrame(() => i())));
class pt extends M {
  #i;
  #n = new rt();
  #r;
  #t = !1;
  #s;
  #e;
  #o;
  constructor(t) {
    super(t, "Esatto.NavigationOrder.Drag"), this.#i = new U(t), this.#r = new ot(
      (e) => q(t, e).then((n) => (R(n), n)),
      (e) => d.setOrder(e),
      (e) => {
        console.warn("[Esatto NavigationOrder] Could not save the order.", e), this.#s?.peek("danger", { data: { message: this.#i.term("esattoNavigationOrder_saveFailed") } });
      }
    ), this.consumeContext(P, (e) => {
      this.observe(e?.isAdmin, (n) => this.#t = n === !0, "isAdmin");
    }), this.consumeContext(B, (e) => this.#s = e), document.addEventListener("pointerdown", this.#c, !0), document.addEventListener("dragstart", this.#u, !0), document.addEventListener("dragover", this.#d, !0), document.addEventListener("drop", this.#h, !0), document.addEventListener("dragend", this.#l, !0), document.addEventListener("keydown", this.#p, !0);
  }
  destroy() {
    document.removeEventListener("pointerdown", this.#c, !0), document.removeEventListener("dragstart", this.#u, !0), document.removeEventListener("dragover", this.#d, !0), document.removeEventListener("drop", this.#h, !0), document.removeEventListener("dragend", this.#l, !0), document.removeEventListener("keydown", this.#p, !0), super.destroy();
  }
  #a(t) {
    return m(t.composedPath(), d.manifests(), g);
  }
  // Before the browser decides what the press drags.
  #c = (t) => {
    if (!this.#t) return;
    const e = this.#a(t);
    e && nt(e.tab);
  };
  #u = (t) => {
    if (!this.#t || !t.dataTransfer) return;
    const e = this.#a(t);
    if (!e) return;
    t.dataTransfer.effectAllowed = "move", t.dataTransfer.setData(ft, e.alias);
    const n = e.tab;
    n.style.opacity = "0.4", this.#e = { source: e, element: n };
  };
  #d = (t) => {
    const e = this.#e ? this.#a(t) : null, n = dt(this.#e?.source, e);
    if (n === "ignore") return;
    if (t.preventDefault(), t.dataTransfer && (t.dataTransfer.dropEffect = n), n === "none" || !e) {
      this.#o = void 0, this.#n.hideLine();
      return;
    }
    const s = e.tab.getBoundingClientRect(), o = ht(t.clientX, s);
    this.#n.showLine(o === "before" ? s.left : s.right, s.top, s.height), this.#o = { target: e, position: o };
  };
  #h = (t) => {
    if (!this.#e) return;
    t.preventDefault(), t.stopPropagation();
    const { source: e } = this.#e, n = this.#o;
    this.#f(), n && this.#g(e, n.target.alias, n.position);
  };
  #l = () => this.#f();
  #f() {
    this.#e && (this.#e.element.style.opacity = ""), this.#e = void 0, this.#o = void 0, this.#n.hideLine();
  }
  #p = (t) => {
    const e = lt(t);
    if (e === 0 || !this.#t) return;
    const n = this.#a(t);
    if (!n) return;
    const s = this.#m(n.group), o = s.indexOf(n.tab), r = s[o + e], a = r && m([r, n.group], d.manifests(), g)?.alias;
    if (o < 0 || !a) return;
    t.preventDefault(), t.stopPropagation(), this.#g(n, a, e < 0 ? "before" : "after");
    const u = n.tab.getAttribute("label") ?? n.alias;
    this.#n.announce(this.#i.term("esattoNavigationOrder_moved", u, o + e + 1, s.length)), N().then(() => {
      const h = this.#b(n);
      h && st(h).focus();
    });
  };
  #g(t, e, n) {
    const { bar: s } = t, o = d.effectiveList(s), r = X(o, t.alias, e, n);
    if (r.join(`
`) === o.join(`
`)) return;
    const a = s.kind === "tabs" ? d.effectiveList({ ...s, type: s.type === "dashboard" ? "sectionView" : "dashboard" }) : [], u = d.order, h = this.#r.submit(H(u, s, r, a), u);
    N().then(() => {
      const l = this.#b(t);
      l && this.#r.busy && this.#n.showSpinner(l.getBoundingClientRect(), this.#i.term("esattoNavigationOrder_saving"));
    }), h.finally(() => this.#n.hideSpinner());
  }
  /** The tabs of a group, in display order (they're its light-DOM children). */
  #m(t) {
    return Array.from(t.children ?? []).filter((e) => e.localName === "uui-tab");
  }
  /** The tab now showing `source`'s alias in its group (bars redraw after a move). */
  #b(t) {
    return this.#m(t.group).find(
      (e) => m([e, t.group], d.manifests(), g)?.alias === t.alias
    );
  }
}
function gt(i) {
  new pt(i);
}
const At = (i) => {
  d.start();
  const t = j();
  t && d.setOrder(t), $(i).then((e) => {
    d.setOrder(e), R(e);
  }).catch((e) => console.warn("[Esatto NavigationOrder] Could not load the saved order.", e)), gt(i);
};
export {
  At as onInit
};
//# sourceMappingURL=navigation-order.js.map
