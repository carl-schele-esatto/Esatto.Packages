import { UmbLocalizationController as T, umbLocalizationManager as L } from "@umbraco-cms/backoffice/localization-api";
import { umbHttpClient as O } from "@umbraco-cms/backoffice/http-client";
const f = 100, z = /* @__PURE__ */ new Set(["ar", "he", "fa", "ur", "ps", "syr", "dv"]);
function _(e) {
  return /[.\-]/.test(e) ? e.replace(/[.\-]/g, "_") : null;
}
function p(e) {
  return z.has(b(e)) ? "rtl" : "ltr";
}
function g(e) {
  return /[-_]/.test(e);
}
function b(e) {
  return e.split(/[-_]/)[0]?.toLowerCase() ?? "";
}
function h(e, o) {
  for (const [t, r] of Object.entries(o ?? {})) {
    if (typeof r != "string" || r.length === 0)
      continue;
    e[t] = r;
    const s = _(t);
    s && s !== t && (e[s] = r);
  }
}
function A(e) {
  const o = e.cultures ?? {}, t = [], r = new Set(
    Object.keys(o).filter((n) => !g(n)).map((n) => n.toLowerCase())
  ), s = /* @__PURE__ */ new Map();
  for (const [n, c] of Object.entries(o)) {
    const i = {
      $code: n,
      $dir: p(n),
      $weight: f
    };
    h(i, c), t.push(i);
    const a = b(n);
    if (g(n) && !r.has(a)) {
      const u = s.get(a) ?? {};
      for (const [w, l] of Object.entries(c ?? {}))
        typeof l != "string" || l.length === 0 || (u[w] = l);
      s.set(a, u);
    }
  }
  for (const [n, c] of s) {
    const i = {
      $code: n,
      $dir: p(n),
      $weight: f
    };
    h(i, c), t.push(i);
  }
  return t;
}
let d = !1;
async function D(e, o) {
  if (d)
    return !1;
  const { data: t, error: r } = await o();
  if (r || !t)
    return console.warn("[Esatto.DictionaryLocalization] fetch failed; #Key labels will not resolve until next reload.", r), !1;
  const n = A(t);
  return e.registerManyLocalizations(n), d = !0, !0;
}
const $ = "/umbraco/management/api/v1/backoffice/dictionary-localization/all";
function k() {
  return O.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: $
  });
}
const C = /#\w[\w.-]*/g;
function E(e) {
  const o = [];
  let t = e;
  for (; t.length > 0; ) {
    o.push(t);
    const r = Math.max(t.lastIndexOf("."), t.lastIndexOf("-"));
    if (r <= 0) break;
    t = t.slice(0, r);
  }
  return o;
}
function I(e, o) {
  return typeof e != "string" ? "" : e.replace(C, (t) => {
    const r = t.slice(1);
    for (const s of E(r)) {
      const n = o(s);
      if (n !== null)
        return n + r.slice(s.length);
    }
    return t;
  });
}
const y = "__esattoDottedTokenPatch";
function P(e) {
  const o = e.prototype;
  o[y] || (o.string = function(t, ...r) {
    return I(t, (s) => {
      const n = this.term(s, ...r);
      return n === s ? null : n;
    });
  }, o[y] = !0);
}
const R = new RegExp("(?<![\\w{#])#(\\w[\\w.-]*)", "g");
function x(e, o) {
  return typeof e != "string" ? e : e.replace(R, (t, r) => {
    const s = r.replace(/[.-]+$/, ""), n = r.slice(s.length);
    return o(s) ? `{#${s}}${n}` : t;
  });
}
const m = "__esattoUfmHashPatch";
function v(e) {
  const o = e.prototype;
  if (o[m])
    return;
  const t = Object.getOwnPropertyDescriptor(o, "markdown");
  if (!t || typeof t.get != "function" || typeof t.set != "function")
    return;
  const r = t.get, s = t.set;
  Object.defineProperty(o, "markdown", {
    configurable: !0,
    enumerable: t.enumerable,
    get() {
      const n = r.call(this);
      if (typeof n != "string")
        return n;
      const c = this.localize;
      return c ? x(n, (i) => c.term(i) !== i) : n;
    },
    set(n) {
      s.call(this, n);
    }
  }), o[m] = !0;
}
const j = async () => {
  P(T), customElements.whenDefined("umb-ufm-render").then((e) => v(e)).catch(() => {
  }), await D(
    L,
    k
  );
};
export {
  j as onInit
};
//# sourceMappingURL=esatto-umbraco-backoffice-dictionary-localization.js.map
