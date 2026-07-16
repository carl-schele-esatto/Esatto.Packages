import { UmbLocalizationController as L, umbLocalizationManager as S } from "@umbraco-cms/backoffice/localization-api";
import { umbHttpClient as K } from "@umbraco-cms/backoffice/http-client";
const f = 100, k = /* @__PURE__ */ new Set(["ar", "he", "fa", "ur", "ps", "syr", "dv"]);
function z(t) {
  return /[.\-]/.test(t) ? t.replace(/[.\-]/g, "_") : null;
}
function g(t) {
  return k.has(b(t)) ? "rtl" : "ltr";
}
function p(t) {
  return /[-_]/.test(t);
}
function b(t) {
  return t.split(/[-_]/)[0]?.toLowerCase() ?? "";
}
function y(t, o) {
  for (const [e, n] of Object.entries(o ?? {})) {
    if (typeof n != "string" || n.length === 0)
      continue;
    t[e] = n;
    const s = z(e);
    s && s !== e && (t[s] = n);
  }
}
function _(t) {
  const o = t.cultures ?? {}, e = [], n = new Set(
    Object.keys(o).filter((r) => !p(r)).map((r) => r.toLowerCase())
  ), s = /* @__PURE__ */ new Map();
  for (const [r, i] of Object.entries(o)) {
    const c = {
      $code: r,
      $dir: g(r),
      $weight: f
    };
    y(c, i), e.push(c);
    const a = b(r);
    if (p(r) && !n.has(a)) {
      const l = s.get(a) ?? {};
      for (const [O, u] of Object.entries(i ?? {}))
        typeof u != "string" || u.length === 0 || (l[O] = u);
      s.set(a, l);
    }
  }
  for (const [r, i] of s) {
    const c = {
      $code: r,
      $dir: g(r),
      $weight: f
    };
    y(c, i), e.push(c);
  }
  return e;
}
function A(t) {
  const o = /* @__PURE__ */ new Set();
  for (const e of t)
    for (const n of Object.keys(e))
      n.startsWith("$") || o.add(n);
  return [...o];
}
const T = /* @__PURE__ */ new Set();
function D(t) {
  for (const o of t)
    T.add(o);
}
function C(t) {
  return T.has(t);
}
let h = !1;
async function $(t, o) {
  if (h)
    return !1;
  const { data: e, error: n } = await o();
  if (n || !e)
    return console.warn("[Esatto.DictionaryLocalization] fetch failed; #Key labels will not resolve until next reload.", n), !1;
  const r = _(e);
  return t.registerManyLocalizations(r), D(A(r)), h = !0, !0;
}
const I = "/umbraco/management/api/v1/backoffice/dictionary-localization/all";
function E() {
  return K.get({
    security: [{ scheme: "bearer", type: "http" }],
    url: I
  });
}
const P = /#\w[\w.-]*/g;
function R(t) {
  const o = [];
  let e = t;
  for (; e.length > 0; ) {
    o.push(e);
    const n = Math.max(e.lastIndexOf("."), e.lastIndexOf("-"));
    if (n <= 0) break;
    e = e.slice(0, n);
  }
  return o;
}
function x(t, o) {
  return typeof t != "string" ? "" : t.replace(P, (e) => {
    const n = e.slice(1);
    for (const s of R(n)) {
      const r = o(s);
      if (r !== null)
        return r + n.slice(s.length);
    }
    return e;
  });
}
const d = "__esattoDottedTokenPatch";
function j(t, o) {
  const e = t.prototype;
  e[d] || (e.string = function(n, ...s) {
    return x(n, (r) => {
      if (o.isOurKey(r) && !o.isTranslatingSurface())
        return null;
      const i = this.term(r, ...s);
      return i === r ? null : i;
    });
  }, e[d] = !0);
}
const v = new RegExp("(?<![\\w{#])#(\\w[\\w.-]*)", "g");
function N(t, o) {
  return typeof t != "string" ? t : t.replace(v, (e, n) => {
    const s = n.replace(/[.-]+$/, ""), r = n.slice(s.length);
    return o(s) ? `{#${s}}${r}` : e;
  });
}
const m = "__esattoUfmHashPatch";
function H(t, o) {
  const e = t.prototype;
  if (e[m])
    return;
  const n = Object.getOwnPropertyDescriptor(e, "markdown");
  if (!n || typeof n.get != "function" || typeof n.set != "function")
    return;
  const s = n.get, r = n.set;
  Object.defineProperty(e, "markdown", {
    configurable: !0,
    enumerable: n.enumerable,
    get() {
      const i = s.call(this);
      if (typeof i != "string")
        return i;
      const c = this.localize;
      return !c || !o.isTranslatingSurface() ? i : N(i, (a) => c.term(a) !== a);
    },
    set(i) {
      r.call(this, i);
    }
  }), e[m] = !0;
}
function M(t) {
  return /\/section\/content(?![\w-])/i.test(t);
}
function w() {
  try {
    return M(globalThis.location?.href ?? "");
  } catch {
    return !1;
  }
}
const B = async () => {
  j(L, {
    isOurKey: C,
    isTranslatingSurface: w
  }), customElements.whenDefined("umb-ufm-render").then(
    (t) => H(t, { isTranslatingSurface: w })
  ).catch(() => {
  }), await $(
    S,
    E
  );
};
export {
  B as onInit
};
//# sourceMappingURL=esatto-umbraco-backoffice-dictionary-localization.js.map
